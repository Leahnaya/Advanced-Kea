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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using HtmlAgilityPack;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Image = iTextSharp.text.Image;
using Rectangle = iTextSharp.text.Rectangle;

namespace Kea
{
    public partial class Main : Form
    {
		public struct EpisodeListEntry
		{
			public string episodeSequence;
			public int episodeNo;
			public string episodeTitle;
			public string url;
		}

		public struct ToonListEntryInfo
		{
			public int titleNo;
			public string toonTitleName;
			
			public string startDownloadAtEpisode;
			public string stopDownloadAtEpisode;
		}

		public struct ToonListEntry
		{
			public ToonListEntryInfo toonInfo;
			public EpisodeListEntry[] episodeList;
		}
		
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

		public List<ToonListEntry> toonList;
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
                var items = QueueGrid.Rows.Cast<DataGridViewRow>().Where(row => row.Cells["titleName"].Value.ToString() == toonName);

                if (items.Count() != 0)
                    continue;

                Uri lineUri = new Uri(line);
                int titleNo = Convert.ToInt32(System.Web.HttpUtility.ParseQueryString(lineUri.Query).Get("title_no"));

                QueueGrid.Rows.Add(titleNo, toonName, "1", "end",line);
            }
            URLTextbox.Text = "";
        }

        private async void startBtn_Click(object sender, EventArgs e)
        {
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
			
            toonList = new List<ToonListEntry>();

            foreach (DataGridViewRow r in QueueGrid.Rows) //get all chapter links
			{
                await Task.Run(() => GetChapterAsync(r));
            }
            for (int t = 0; t < toonList.Count; t++)    //for each comic in queue...
            {
				await Task.Run(() => downloadComic(toonList[t]));
            }
            processInfo.Text = "done!";
            progressBar.Value = progressBar.Minimum;
        }

        private async Task GetChapterAsync(DataGridViewRow r)
        {
			string line = r.Cells["titleUrl"].Value.ToString();
            if (String.IsNullOrEmpty(line) || String.IsNullOrWhiteSpace(line)) return;
			
			ToonListEntry currentToonEntry = new ToonListEntry();
            List<EpisodeListEntry> toonEpisodeList = new List<EpisodeListEntry>();
			
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
					var episodeNodes = htmlDoc.DocumentNode.SelectNodes("//body/div[@id='wrap']/div[@id='container']/div[@id='content']/div[@class='cont_box']/div[starts-with(@class,'detail_body')]/div[@class='detail_lst']/ul[@id='_listUl']/li[@class='_episodeItem']");

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
							
							EpisodeListEntry currentEpisode = new EpisodeListEntry();
							
							currentEpisode.episodeSequence = episodeSequence;
							currentEpisode.episodeNo = episodeNo;
							currentEpisode.episodeTitle = SanitizeEpisodeTitle(episodeTitle);
							currentEpisode.url = url;
							
							toonEpisodeList.Add(currentEpisode);
						}
					}

					string nextPage = GetWebsiteNextPageUrl(htmlDoc);
					if (String.IsNullOrEmpty(nextPage) || String.IsNullOrWhiteSpace(nextPage))
						break;

					if (!IsValidURL(nextPage))
						nextPage = baseUri.GetLeftPart(UriPartial.Authority) + nextPage;

					html = client.DownloadString(nextPage);
                }
            }
            
			//Toons are listed from last episode to first episode, so order needs to be reversed.
			toonEpisodeList.Reverse();

            currentToonEntry.episodeList = toonEpisodeList.ToArray();
            toonList.Add(currentToonEntry);
        }

        private void downloadComic(ToonListEntry currentToon)
        {
            string savePath = savepathTB.Text + @"\";
            string curName = currentToon.toonInfo.toonTitleName;
            if (cartoonFoldersCB.Checked) { Directory.CreateDirectory(savePath + curName); savePath += curName; }

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
            for (int i = (int)startNr; i < endNr; i++)    //...and for each chapter in that comic...
            {
                processInfo.Invoke((MethodInvoker)delegate { processInfo.Text = $"[ ({currentToon.toonInfo.titleNo}) {currentToon.toonInfo.toonTitleName} ] grabbing the html of {currentToon.episodeList[i].url}"; try { progressBar.Value = i * 100; } catch { } }); //run on the UI thread
				
				if (chapterFoldersCB.Checked || saveAs != "multiple images") { Directory.CreateDirectory(savePath + @"\" + $"({i + 1}) {currentToon.episodeList[i].episodeTitle}{suffix}"); }

				using (WebClient client = new WebClient())
                {
                    client.Headers.Add("Cookie", "pagGDPR=true;");  //add cookies to bypass age verification
                    IWebProxy proxy = WebRequest.DefaultWebProxy;    //add default proxy
                    client.Proxy = proxy;
					
					client.Encoding = System.Text.Encoding.UTF8;
					
                    string html = client.DownloadString(currentToon.episodeList[i].url);
                    HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
                    doc.LoadHtml(html);
					
					var episodeImgs = doc.DocumentNode.SelectNodes("//body/div[@id='wrap']/div[@id='container']/div[@id='content']/div[@class='cont_box']/div[@class='viewer_lst']/div[@id='_imageList']/img");
					//In case SelectNodes messes the order, sort by position in html document
					HtmlNode[] imgList = episodeImgs.OrderBy(node => node.StreamPosition).ToArray();
					int totalImgCount = imgList.Length;
					int imageNo = 0;

					foreach (HtmlNode imageNode in imgList)
					{
						processInfo.Invoke((MethodInvoker)delegate { processInfo.Text = $"[ ({currentToon.toonInfo.titleNo}) {currentToon.toonInfo.toonTitleName} ] downloading image {imageNo} of chapter {i + 1}!"; }); //run on the UI thread
						client.Headers.Add("Referer", currentToon.episodeList[i].url);    //refresh the referer for each request!

						string imgName = $"{curName} Ch{i + 1}.{imageNo}";
						string imgUrl = imageNode.Attributes["data-url"].Value;
						if(HighestQualityCB.Checked)
						{
							//Remove the "?type=" query string from image url, this results in downloading the image with the same quality stored in the server.
							imgUrl = RemoveQueryStringByKey(imgUrl, "type");
							imgName += "[HQ]";
						}

						if (chapterFoldersCB.Checked || saveAs != "multiple images") { client.DownloadFile(new Uri(imgUrl), $"{savePath}\\({i + 1}) {currentToon.episodeList[i].episodeTitle}{suffix}\\{imgName}.jpg"); }
						else { client.DownloadFile(new Uri(imgUrl), $"{savePath}\\{imgName}.jpg"); }
						
						processInfo.Invoke((MethodInvoker)delegate { try { progressBar.Value = i * 100 + (int)(imageNo / (float)totalImgCount * 100); } catch { } });
						imageNo++;
					}
                }
                if (saveAs == "PDF file")  //bundle images into PDF
                {
                    DirectoryInfo di = new DirectoryInfo($"{savePath}\\({i + 1}) {currentToon.episodeList[i].episodeTitle}{suffix}");
                    FileInfo[] fileInfos = di.GetFiles("*.jpg").OrderBy(fi => fi.CreationTime).ToArray();
                    string[] files = fileInfos.Select(o => o.FullName).ToArray();
                    Document doc = new Document();
                    try
                    {
                        PdfWriter.GetInstance(doc, new FileStream($"{savePath}\\({i + 1}) {currentToon.episodeList[i].episodeTitle}{suffix}.pdf", FileMode.Create));
                        doc.Open();
                        for (int j = 0; j < files.Length; j++)
                        {
                            Image img = Image.GetInstance(files[j]);
                            img.SetAbsolutePosition(0, 0);
                            doc.SetPageSize(new Rectangle(img.Width, img.Height));
                            doc.NewPage();
                            doc.Add(img);
                        }
                    }
                    catch { Console.WriteLine("rip"); }
                    finally { doc.Close(); }
                    Directory.Delete($"{savePath}\\({i + 1}) {currentToon.episodeList[i].episodeTitle}{suffix}", true);
                }
                else if (saveAs == "one image (may be lower in quality)") //bundle images into one long image
                {
                    DirectoryInfo di = new DirectoryInfo($"{savePath}\\({i + 1}) {currentToon.episodeList[i].episodeTitle}{suffix}");
                    FileInfo[] fileInfos = di.GetFiles("*.jpg").OrderBy(fi => fi.CreationTime).ToArray();
                    string[] files = fileInfos.Select(o => o.FullName).ToArray();

                    Bitmap[] images = new Bitmap[files.Length];
                    int finalHeight = 0;
                    for (int j = 0; j < images.Length; j++)
                    {
                        images[j] = new Bitmap(files[j]);
                        finalHeight += images[j].Height;
                    }

                    using (Bitmap bm = new Bitmap(images[0].Width, finalHeight))
                    {
                        int pointerHeight = 0;
                        using (Graphics g = Graphics.FromImage(bm))
                        {
                            for (int k = 0; k < images.Length; k++)
                            {
                                g.DrawImage(images[k], 0, pointerHeight);
                                pointerHeight += images[k].Height;
                            }
                        }
                        if (finalHeight > 30000)
                        {
                            Bitmap resizedImage = ResizeImage(bm, (int)(images[0].Width * (1.0 - (float)(finalHeight - 30000) / finalHeight)), 30000);
                            resizedImage.Save($"{savePath}\\({i + 1}) {currentToon.episodeList[i].episodeTitle}{suffix}.png");
                        }
                        else bm.Save($"{savePath}\\({i + 1}) {currentToon.episodeList[i].episodeTitle}{suffix}.png");
                    }
                    foreach (Bitmap image in images)
                    {
                        image.Dispose();
                    }
                    Directory.Delete($"{savePath}\\({i + 1}) {currentToon.episodeList[i].episodeTitle}{suffix}", true);
                }
                else if (saveAs == "CBZ file")
                {
                    ZipFile.CreateFromDirectory($"{savePath}\\({i + 1}) {currentToon.episodeList[i].episodeTitle}{suffix}", $"{savePath}\\({i + 1}) {currentToon.episodeList[i].episodeTitle}{suffix}.cbz");
                    Directory.Delete($"{savePath}\\({i + 1}) {currentToon.episodeList[i].episodeTitle}{suffix}", true);
                }
            }
        }

        public static Bitmap ResizeImage(System.Drawing.Image image, int width, int height)
        {
            System.Drawing.Rectangle destRect = new System.Drawing.Rectangle(0, 0, width, height);
            Bitmap destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (Graphics graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (ImageAttributes wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

		public static string GetWebsiteNextPageUrl(HtmlAgilityPack.HtmlDocument htmlDoc)
        {
            var pageNodes = htmlDoc.DocumentNode.SelectNodes("//body/div[@id='wrap']/div[@id='container']/div[@id='content']/div[@class='cont_box']/div[starts-with(@class,'detail_body')]/div[@class='detail_lst']/div[@class='paginate']/a");
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

        public static bool IsValidURL(string URL)
        {
            string Pattern = @"^(?:http(s)?:\/\/)?[\w.-]+(?:\.[\w\.-]+)+[\w\-\._~:/?#[\]@!\$&'\(\)\*\+,;=.]+$";
            Regex Rgx = new Regex(Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return Rgx.IsMatch(URL);
        }

		public static string RemoveQueryStringByKey(string url, string key)
        {
            var uri = new Uri(url);

            // this gets all the query string key value pairs as a collection
            var newQueryString = System.Web.HttpUtility.ParseQueryString(uri.Query);

            // this removes the key if exists
            newQueryString.Remove(key);

            // this gets the page path from root without QueryString
            string pagePathWithoutQueryString = uri.GetLeftPart(UriPartial.Path);

            return newQueryString.Count > 0
                ? String.Format("{0}?{1}", pagePathWithoutQueryString, newQueryString)
                : pagePathWithoutQueryString;
        }

		public static string SanitizeEpisodeTitle(string episodeTitle)
        {
            string newepisodeTitle = episodeTitle;
            string invalidChars = new string(System.IO.Path.GetInvalidFileNameChars()) + new string(System.IO.Path.GetInvalidPathChars());
            foreach (char c in invalidChars)
            {
                newepisodeTitle = newepisodeTitle.Replace(c.ToString(), "");
            }
            return newepisodeTitle;
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
