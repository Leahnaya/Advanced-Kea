using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;
using HtmlAgilityPack;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Image = iTextSharp.text.Image;
using Rectangle = iTextSharp.text.Rectangle;
using Kea.CommonFiles;

namespace Kea
{
	public partial class Main : Form
	{
		public const int WM_NCLBUTTONDOWN = 0xA1;
		public const int HT_CAPTION = 0x2;

		public List<Structures.ToonListEntry> toonList;
		public string saveAs;

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
		[System.Runtime.InteropServices.DllImport("user32.dll")]
		public static extern bool ReleaseCapture();

		private const int CS_DROPSHADOW = 0x00020000;
		protected override CreateParams CreateParams
		{
			get
			{
				// add the drop shadow flag for automatically drawing
				// a drop shadow around the form
				CreateParams cp = base.CreateParams;
				cp.ClassStyle |= CS_DROPSHADOW;
				return cp;
			}
		}

		public Main()
		{
			InitializeComponent();
			QueueGrid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
			saveAsOption.DropDownStyle = ComboBoxStyle.DropDownList;
			//toolTips.SetToolTip(oneImagecb, "If the image of a chapter exceeds\n30,000 pixels it will be down scaled");
		}

		private void HandleBar_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)  //allow moving of the window
		{
			if (e.Button == MouseButtons.Left)
			{
				ReleaseCapture();
				SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
			}
		}

		private void addToQueueBtn_Click(object sender, EventArgs e)
		{
			List<string> lines = new List<string>();
			lines.AddRange(URLTextbox.Text.Split('\n'));
			foreach (string line in lines)
			{
				int nameEnd = 0;
				int nameStart = 0;
				if (!line.Contains("https://www.webtoons.com/") || !line.Contains("/list?title_no=")) { continue; } //doesn't support m.webtoons.com bc it would result in a 400 bad request and i'm too lazy to replace the m with www manually
				if (line.Length - line.Replace("/", "").Length != 6) { continue; }
				try
				{
					for (int i = 0; i < 6; i++)
					{
						nameStart = nameEnd;
						while (line[nameEnd] != '/') nameEnd++;
						nameEnd++;
					}
				}
				catch { continue; }
				string toonName = line.Substring(nameStart, nameEnd - nameStart - 1);
				
				Uri lineUri = new Uri(line);
				int titleNo = Convert.ToInt32(System.Web.HttpUtility.ParseQueryString(lineUri.Query).Get("title_no"));
				
				var items = QueueGrid.Rows.Cast<DataGridViewRow>().Where(row => row.Cells["titleName"].Value.ToString() == toonName && Convert.ToInt32(row.Cells["titleNo"].Value.ToString()) == titleNo );

				if (items.Count() != 0)
					continue;

				QueueGrid.Rows.Add(titleNo, toonName, "1", "end",line);
			}
			URLTextbox.Text = "";
		}

		private async void startBtn_Click(object sender, EventArgs e)
		{
            if (skipDownloadedChaptersCB.Checked && saveAs == "multiple images")
            {
                MessageBox.Show("Skipping downloaded chapters cannot be used while saving as \"multiple images\" ");
                return;
            }

			foreach (DataGridViewRow r in QueueGrid.Rows)
			{
				int end = 0, start = 0;
				try
				{
					start = int.Parse(r.Cells["titleEpBegin"].Value.ToString());
					if (start < 1) { MessageBox.Show("The start chapter must be greater than zero!"); return; }
				}
				catch { MessageBox.Show("The start chapter must be a number!"); return; }

				try
				{
					end = int.Parse(r.Cells["titleEpEnd"].Value.ToString());
					if (end < 1) { MessageBox.Show("The end chapter must be greater than zero!"); return; }
				}
				catch
				{
					if (r.Cells["titleEpEnd"].Value.ToString() != "end") { MessageBox.Show("The end chapter must be a number or the word 'end'!"); return; }
				}
				if (end != 0 && end < start) { MessageBox.Show("The start chapter must smaller than the end chapter!"); return; }
			}
			DisableAllControls(this);
			saveAs = saveAsOption.Text;
			EnableControls(HandleBar);
			EnableControls(exitBtn);
			EnableControls(minimizeBtn);
			await DownloadQueueAsync();
			EnableAllControls(this);
			if (saveAs != "multiple images") chapterFoldersCB.Enabled = false;
		}

		private async Task DownloadQueueAsync()
		{
			if (!savepathTB.Text.Contains('\\'))
			{
				savepathTB.Text = "please select a directory for saving";
				return;
			}
			if (QueueGrid.Rows.Count == 0) return;
			
			toonList = new List<Structures.ToonListEntry>();

			foreach (DataGridViewRow r in QueueGrid.Rows) //get all chapter links
			{
				await Task.Run(() => GetChapterAsync(r));
			}
			for (int t = 0; t < toonList.Count; t++)	//for each comic in queue...
			{
				await Task.Run(() => downloadComic(toonList[t]));
			}
			processInfo.Text = "done!";
			progressBar.Value = progressBar.Minimum;
		}

		private async Task GetChapterAsync(DataGridViewRow r)
		{
			string line = r.Cells["titleUrl"].Value.ToString();
			if (Helpers.IsStringEmptyNullOrWhiteSpace(line)) return;
			
			Structures.ToonListEntry currentToonEntry = new Structures.ToonListEntry();
			List<Structures.EpisodeListEntry> toonEpisodeList = new List<Structures.EpisodeListEntry>();
			
			int urlEnd = (line.IndexOf('&') == -1) ? line.Length : line.IndexOf('&');
			line = line.Substring(0, urlEnd);
			Uri baseUri = new Uri(line);
			string baseUrl = baseUri.GetLeftPart(UriPartial.Path);

			currentToonEntry.toonInfo.titleNo = Convert.ToInt32(r.Cells["titleNo"].Value.ToString());
			currentToonEntry.toonInfo.toonTitleName = r.Cells["titleName"].Value.ToString();
			currentToonEntry.toonInfo.startDownloadAtEpisode = r.Cells["titleEpBegin"].Value.ToString();
			currentToonEntry.toonInfo.stopDownloadAtEpisode = r.Cells["titleEpEnd"].Value.ToString();

			using (WebClient client = new WebClient())
			{
				int i = 0;
				
				string html = client.DownloadString(line + "&page=1");
				
				while (true)
				{
					i++;
					processInfo.Invoke((MethodInvoker)delegate { processInfo.Text = $"[ ({currentToonEntry.toonInfo.titleNo}) {currentToonEntry.toonInfo.toonTitleName} ] scoping tab {i}"; }); //run on the UI thread
					client.Headers.Add("Cookie", "pagGDPR=true;");  //add cookies to bypass age verification
					IWebProxy proxy = WebRequest.DefaultWebProxy;   //add default proxy
					client.Proxy = proxy;
					
					client.Encoding = System.Text.Encoding.UTF8;

					var htmlDoc = new HtmlAgilityPack.HtmlDocument();
					htmlDoc.LoadHtml(html);
					var episodeNodes = htmlDoc.DocumentNode.SelectNodes(Globals.episodeListItemHtmlXPath);

					if(episodeNodes != null)
					{
						foreach (var node in episodeNodes)
						{
							int episodeNo = -1;
							if (node.Attributes["data-episode-no"] != null)
							{
								episodeNo = Convert.ToInt32(node.Attributes["data-episode-no"].Value);
							}

							HtmlNode inner_a_node = node.SelectSingleNode("./a");
							string url = inner_a_node.Attributes["href"].Value;
							string episodeTitle = inner_a_node.SelectSingleNode("./span[@class='subj']/span").InnerHtml;
							string episodeSequence = inner_a_node.SelectSingleNode("./span[@class='tx']").InnerHtml;
							
							Structures.EpisodeListEntry currentEpisode = new Structures.EpisodeListEntry();
							
							currentEpisode.episodeSequence = episodeSequence;
							currentEpisode.episodeNo = episodeNo;
							currentEpisode.episodeTitle = Helpers.SanitizeStringForFilePath(episodeTitle);
							currentEpisode.url = url;
							
							toonEpisodeList.Add(currentEpisode);
						}
					}

					string nextPage = GetWebsiteNextPageUrl(htmlDoc);
					if (Helpers.IsStringEmptyNullOrWhiteSpace(nextPage))
						break;

					if (!Helpers.IsValidURL(nextPage))
						nextPage = baseUri.GetLeftPart(UriPartial.Authority) + nextPage;

					html = client.DownloadString(nextPage);
				}
			}
			
			//Toons are listed from last episode to first episode, so order needs to be reversed.
			toonEpisodeList.Reverse();

			currentToonEntry.episodeList = toonEpisodeList.ToArray();
			toonList.Add(currentToonEntry);
		}

		private void downloadComic(Structures.ToonListEntry currentToon)
		{
			string baseSavePath = savepathTB.Text + @"\";
			string comicSavePath = baseSavePath + ToonHelpers.GetToonSavePath(currentToon.toonInfo);

			if (cartoonFoldersCB.Checked)
			{
				//If checked, add the path separator & create new directory
				comicSavePath += @"\";
				Directory.CreateDirectory(comicSavePath); 
			}
			else
			{
				//separation isn't necessary here
				//comicSavePath += "_";
			}

			string suffix = "";
			if(HighestQualityCB.Checked)
			{
				suffix = "[HQ]";
			}

			//set start and end chapter
			float startNr = int.Parse(currentToon.toonInfo.startDownloadAtEpisode) - 1;
			float endNr = (currentToon.toonInfo.stopDownloadAtEpisode == "end") ? currentToon.episodeList.Length : int.Parse(currentToon.toonInfo.stopDownloadAtEpisode);

			if (endNr > currentToon.episodeList.Length) endNr = currentToon.episodeList.Length;
			processInfo.Invoke((MethodInvoker)delegate
			{
				progressBar.Minimum = (int)startNr * 100;
				progressBar.Maximum = (int)endNr * 100;
			});
			for (int i = (int)startNr; i < endNr; i++)	//...and for each chapter in that comic...
			{
				processInfo.Invoke((MethodInvoker)delegate { processInfo.Text = $"[ ({currentToon.toonInfo.titleNo}) {currentToon.toonInfo.toonTitleName} ] grabbing the html of {currentToon.episodeList[i].url}"; try { progressBar.Value = i * 100; } catch { } }); //run on the UI thread
				
				string episodeSavePath = comicSavePath + ToonHelpers.GetToonEpisodeSavePath(i,currentToon.episodeList[i],suffix);
				string archiveSavePath = episodeSavePath; // shouldn't end with /
				
				if( skipDownloadedChaptersCB.Checked )
				{
					string bundlePath = $"{archiveSavePath}{ToonHelpers.GetBundleExtension(saveAs)}";
					if (File.Exists(bundlePath))
					{
						processInfo.Invoke((MethodInvoker)delegate { processInfo.Text = $"[ ({currentToon.toonInfo.titleNo}) {currentToon.toonInfo.toonTitleName} ] Skipping the chapter";}); //run on the UI thread
						continue;
					}
				}
				
				bool chapterDirectoryWasCreated = false;
				if (chapterFoldersCB.Checked || saveAs != "multiple images")
				{
					//If checked, add the path separator & create new directory
					episodeSavePath += @"\";
					Directory.CreateDirectory(episodeSavePath);
					chapterDirectoryWasCreated = true;
				}
				else
				{
					episodeSavePath += "_"; // separate between episode name & image number
				}
				
				List<Structures.downloadedToonChapterFileInfo> downloadedImages = new List<Structures.downloadedToonChapterFileInfo>();
				
				using (WebClient client = new WebClient())
				{
					client.Headers.Add("Cookie", "pagGDPR=true;");  //add cookies to bypass age verification
					IWebProxy proxy = WebRequest.DefaultWebProxy;	//add default proxy
					client.Proxy = proxy;
					
					client.Encoding = System.Text.Encoding.UTF8;
					
					string html = client.DownloadString(currentToon.episodeList[i].url);
					HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
					doc.LoadHtml(html);
					
					var episodeImgs = doc.DocumentNode.SelectNodes(Globals.episodeImageHtmlXPath);
					//In case SelectNodes messes the order, sort by position in html document
					HtmlNode[] imgList = episodeImgs.OrderBy(node => node.StreamPosition).ToArray();
					int totalImgCount = imgList.Length;
					int imageNo = 0;
					
					foreach (HtmlNode imageNode in imgList)
					{
						processInfo.Invoke((MethodInvoker)delegate { processInfo.Text = $"[ ({currentToon.toonInfo.titleNo}) {currentToon.toonInfo.toonTitleName} ] downloading image {imageNo} of chapter {i + 1}!"; }); //run on the UI thread
						client.Headers.Add("Referer", currentToon.episodeList[i].url);	//refresh the referer for each request!

						string imgName = imageNo.ToString("D5");
						string imgUrl = imageNode.Attributes["data-url"].Value;
						if(HighestQualityCB.Checked)
						{
							//Remove the "?type=" query string from image url, this results in downloading the image with the same quality stored in the server.
							imgUrl = Helpers.RemoveQueryStringByKey(imgUrl, "type");
							imgName += "[HQ]";
						}
						
						Structures.downloadedToonChapterFileInfo fileInfo = new Structures.downloadedToonChapterFileInfo();
						
						string imgExtension = Helpers.GetFileExtensionFromUrl(imgUrl);
						
						string imgSaveName = $"{imgName}{imgExtension}";
						string imgSavePath = $"{episodeSavePath}{imgSaveName}";

						try{ client.DownloadFile(new Uri(imgUrl), imgSavePath); }
						catch(WebException ex) when ((ex.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
						{
							// handle file not found
							imgSaveName = $"{imgName}_failed.png";
							imgSavePath = $"{episodeSavePath}{imgSaveName}";
							ToonHelpers.DrawAndSaveNotFoundImage(imageNo,imgSavePath);
							archiveSavePath += "_failed";
						}

						fileInfo.filePath = imgSavePath;
						fileInfo.filePathInArchive = imgSaveName;
						downloadedImages.Add(fileInfo);
						
						processInfo.Invoke((MethodInvoker)delegate { try { progressBar.Value = i * 100 + (int)(imageNo / (float)totalImgCount * 100); } catch { } });
						imageNo++;
					}
				}
				
				ToonHelpers.createBundledFile(saveAs, archiveSavePath, downloadedImages );
				if( chapterDirectoryWasCreated && ( saveAs == "PDF file" || saveAs == "CBZ file" || saveAs == "one image (may be lower in quality)" ) )
				{
					Directory.Delete(episodeSavePath, true);
				}
			}
		}

		public static string GetWebsiteNextPageUrl(HtmlAgilityPack.HtmlDocument htmlDoc)
		{
			var pageNodes = htmlDoc.DocumentNode.SelectNodes(Globals.episodeListPaginatorXPath);
			var nextPageUrl = "";

			if (pageNodes == null)
				return "";

			bool bGetNextPage = false;
			foreach (var node in pageNodes)
			{
				if (node.Attributes["href"] != null)
				{
					if (bGetNextPage)
					{
						nextPageUrl = node.Attributes["href"].Value;
						bGetNextPage = false;
						break;
					}

					if (node.Attributes["href"].Value == "#")
						bGetNextPage = true;
				}
			}
			return nextPageUrl;
		}

		#region visuals
		private void exitBtn_Click(object sender, EventArgs e) { Application.Exit(); } //c'mon man, isn't this obvious
		private void exitBtn_MouseEnter(object sender, EventArgs e) { exitBtn.BackColor = Color.FromArgb(255, 20, 70, 34); }
		private void exitBtn_MouseLeave(object sender, EventArgs e) { exitBtn.BackColor = Color.FromArgb(255, 0, 30, 14); }

		private void minimizeBtn_Click(object sender, EventArgs e) { WindowState = FormWindowState.Minimized; } //c'mon man, isn't this obvious
		private void minimizeBtn_MouseEnter(object sender, EventArgs e) { minimizeBtn.BackColor = Color.FromArgb(255, 20, 70, 34); }
		private void minimizeBtn_MouseLeave(object sender, EventArgs e) { minimizeBtn.BackColor = Color.FromArgb(255, 0, 30, 14); }

		private void selectFolderBtn_Click(object sender, EventArgs e)
		{
			OpenFileDialog ofile = new OpenFileDialog
			{
				ValidateNames = false,
				CheckFileExists = false,
				CheckPathExists = true,
				FileName = "Folder Selection"
			};
			if (DialogResult.OK == ofile.ShowDialog())
			{
				savepathTB.Text = Path.GetDirectoryName(ofile.FileName);
			}
		}


		private void removeAllBtn_Click(object sender, EventArgs e)
		{
			QueueGrid.Rows.Clear();
		}

		private void removeSelectedBtn_Click(object sender, EventArgs e)
		{
			if (QueueGrid.Rows.Count == 0) return;

			QueueGrid.Rows.RemoveAt(QueueGrid.SelectedRows[0].Index);
		}

		private void DisableAllControls(Control con)
		{
			foreach (Control c in con.Controls)
			{
				DisableAllControls(c);
			}
			con.Enabled = false;
		}

		private void helpBtn_Click(object sender, EventArgs e)
		{
			Process.Start("https://github.com/RustingRobot/Kea#how-to-use");
		}

		private void saveAsOption_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (saveAsOption.Text == "multiple images")
			{
				chapterFoldersCB.Enabled = true;
				chapterFoldersCB.Checked = true;
			}
			else
			{
				chapterFoldersCB.Enabled = false;
				chapterFoldersCB.Checked = false;
			}
		}

		private void EnableAllControls(Control con)
		{
			foreach (Control c in con.Controls)
			{
				EnableAllControls(c);
			}
			con.Enabled = true;
		}

		private void EnableControls(Control con)
		{
			if (con != null)
			{
				con.Enabled = true;
				EnableControls(con.Parent);
			}
		}
		#endregion
	}
}
