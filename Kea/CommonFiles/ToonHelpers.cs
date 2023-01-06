using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Image = iTextSharp.text.Image;
using Rectangle = iTextSharp.text.Rectangle;

namespace Kea.CommonFiles
{
	class ToonHelpers
	{
		//Helpers that require toon structures.
		
		public static void createBundledFile(string saveAs, string episodeSavePath, List<Structures.downloadedToonChapterFileInfo> downloadedFiles )
		{
			Structures.downloadedToonChapterFileInfo[] files = downloadedFiles.ToArray();
			
			if (saveAs == "PDF file")  //bundle images into PDF
			{
				Document doc = new Document();
				try
				{
					PdfWriter.GetInstance(doc, new FileStream($"{episodeSavePath}.pdf", FileMode.Create));
					doc.Open();
					for (int j = 0; j < files.Length; j++)
					{
						Image img = Image.GetInstance(files[j].filePath);
						img.SetAbsolutePosition(0, 0);
						doc.SetPageSize(new Rectangle(img.Width, img.Height));
						doc.NewPage();
						doc.Add(img);
					}
				}
				catch { Console.WriteLine("rip"); }
				finally { doc.Close(); }
			}
			else if (saveAs == "one image (may be lower in quality)") //bundle images into one long image
			{

				Bitmap[] images = new Bitmap[files.Length];
				int finalHeight = 0;
				for (int j = 0; j < images.Length; j++)
				{
					images[j] = new Bitmap(files[j].filePath);
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
					if (finalHeight > Globals.maxSingleImageHeight)
					{
						Bitmap resizedImage = Helpers.ResizeImage(bm, (int)(images[0].Width * (1.0 - (float)(finalHeight - Globals.maxSingleImageHeight) / finalHeight)), Globals.maxSingleImageHeight);
						resizedImage.Save($"{episodeSavePath}.png");
					}
					else bm.Save($"{episodeSavePath}.png");
				}
				foreach (Bitmap image in images)
				{
					image.Dispose();
				}
			}
			else if (saveAs == "CBZ file")
			{
				using (FileStream zipToOpen = new FileStream($"{episodeSavePath}.cbz", FileMode.Create))
				{
					using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
					{
						for (int j = 0; j < files.Length; j++)
						{
							archive.CreateEntryFromFile(files[j].filePath, files[j].filePathInArchive, CompressionLevel.Optimal);
						}
					}
				}
			}
		}
		
		public static string GetToonSavePath(Structures.ToonListEntryInfo toonInfo)
		{
			//Must return without any slashes at the end
			string sanitizedTitleName = Helpers.SanitizeStringForFilePath(toonInfo.toonTitleName);
			return $"{sanitizedTitleName}[{toonInfo.titleNo.ToString("D6")}]";
		}
		
		public static string GetToonEpisodeSavePath(int i,Structures.EpisodeListEntry episodeInfo,string suffix)
		{
			//Must return without any slashes at the end
			//string BasePath = GetToonSavePath(episodeInfo.toonInfo); // commented for Refactor.
			return $"({i + 1}) {episodeInfo.episodeTitle}{suffix}";
		}
		
	}
}
