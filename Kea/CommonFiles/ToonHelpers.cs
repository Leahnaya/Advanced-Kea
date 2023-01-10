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

namespace Kea.CommonFiles
{
	class ToonHelpers
	{
		//Helpers that require toon structures.
		
		public static void createBundledFile(string saveAs, string episodeSavePath, List<Structures.downloadedToonChapterFileInfo> downloadedFiles )
		{
			Structures.downloadedToonChapterFileInfo[] files = downloadedFiles.ToArray();
			string bundleExtenstion = GetBundleExtension(saveAs);
			
			if (saveAs == "PDF file")  //bundle images into PDF
			{
				Document doc = new Document();
				try
				{
					PdfWriter.GetInstance(doc, new FileStream($"{episodeSavePath}{bundleExtenstion}", FileMode.Create));
					doc.Open();
					for (int j = 0; j < files.Length; j++)
					{
						iTextSharp.text.Image img = iTextSharp.text.Image.GetInstance(files[j].filePath);
						img.SetAbsolutePosition(0, 0);
						doc.SetPageSize(new iTextSharp.text.Rectangle(img.Width, img.Height));
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
						resizedImage.Save($"{episodeSavePath}{bundleExtenstion}");
					}
					else bm.Save($"{episodeSavePath}{bundleExtenstion}");
				}
				foreach (Bitmap image in images)
				{
					image.Dispose();
				}
			}
			else if (saveAs == "CBZ file")
			{
				using (FileStream zipToOpen = new FileStream($"{episodeSavePath}{bundleExtenstion}", FileMode.Create))
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
		
		public static string GetBundleExtension(string saveAs)
		{
			if( saveAs == "PDF file" )
				return ".pdf";
			if( saveAs == "one image (may be lower in quality)" )
				return ".png";
			if( saveAs == "CBZ file" )
				return ".cbz";
			
			return "";
		}
		
		public static string GetToonSavePath(Structures.ToonListEntryInfo toonInfo)
		{
			//Must return without any slashes at the end
			string sanitizedTitleName = Helpers.SanitizeStringForFilePath(toonInfo.toonTitleName);
			return $"{sanitizedTitleName}[{toonInfo.titleNo.ToString("D6")}]";
		}
		
		public static string GetToonEpisodeSavePath(Structures.EpisodeListEntry episodeInfo,string suffix)
		{
			//Must return without any slashes at the end
			//string BasePath = GetToonSavePath(episodeInfo.toonInfo); // commented for Refactor.
			return $"({episodeInfo.episodeNo}) {episodeInfo.episodeTitle}{suffix}";
		}
		
		public static void DrawAndSaveNotFoundImage(int imageNumber, string savePath)
		{
			GraphicsPath gp = new GraphicsPath();

            Bitmap bm = new Bitmap(400, 200);
            int radius = 25;
            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            System.Drawing.Rectangle bounds = new System.Drawing.Rectangle(5,5,390,190);
            System.Drawing.Rectangle arc = new System.Drawing.Rectangle(bounds.Location, size);

            // top left arc  
            gp.AddArc(arc, 180, 90);

            // top right arc  
            arc.X = bounds.Right - diameter;
            gp.AddArc(arc, 270, 90);

            // bottom right arc  
            arc.Y = bounds.Bottom - diameter;
            gp.AddArc(arc, 0, 90);

            // bottom left arc 
            arc.X = bounds.Left;
            gp.AddArc(arc, 90, 90);

            gp.CloseFigure();

            LinearGradientBrush brush = new LinearGradientBrush(new Point(5, 5), new Point(395, 195), Color.OrangeRed, Color.DarkRed);
            LinearGradientBrush brush2 = new LinearGradientBrush(new Point(0, 0), new Point(400, 200), Color.Black, Color.White);

            using (Graphics g = Graphics.FromImage(bm))
            {
                g.DrawPath(new Pen(Color.Black, 5), gp);
                g.FillRectangle(brush2, new System.Drawing.Rectangle(0, 0, 400, 200));
                g.FillPath(brush, gp);
                g.DrawString($"Image {imageNumber.ToString("D5")} not found!", new System.Drawing.Font(FontFamily.GenericSansSerif, 35, FontStyle.Bold | FontStyle.Strikeout), Brushes.White,
                    new System.Drawing.Rectangle(5, 5, 390, 190),
                    new StringFormat { Alignment = StringAlignment.Center,LineAlignment = StringAlignment.Center });
                g.Save();
            }
            bm.Save(savePath);
		}
	}
}
