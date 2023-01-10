using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Kea
{
	class Helpers
	{
		//General Helpers
		
		// Draw a rectangle in the indicated Rectangle & round the indicated corners.
		public static GraphicsPath MakeRoundedRect(
			RectangleF rect, float xradius, float yradius,
			bool round_ul, bool round_ur, bool round_lr, bool round_ll)
		{
			// Make a GraphicsPath to draw the rectangle.
			PointF point1, point2;
			GraphicsPath path = new GraphicsPath();

			// Upper left corner.
			if (round_ul)
			{
				RectangleF corner = new RectangleF( rect.X, rect.Y, 2 * xradius, 2 * yradius);
				path.AddArc(corner, 180, 90);
				point1 = new PointF(rect.X + xradius, rect.Y);
			}
			else point1 = new PointF(rect.X, rect.Y);

			// Top side.
			if (round_ur)
				point2 = new PointF(rect.Right - xradius, rect.Y);
			else
				point2 = new PointF(rect.Right, rect.Y);
			path.AddLine(point1, point2);

			// Upper right corner.
			if (round_ur)
			{
				RectangleF corner = new RectangleF( rect.Right - 2 * xradius, rect.Y, 2 * xradius, 2 * yradius);
				path.AddArc(corner, 270, 90);
				point1 = new PointF(rect.Right, rect.Y + yradius);
			}
			else point1 = new PointF(rect.Right, rect.Y);

			// Right side.
			if (round_lr)
				point2 = new PointF(rect.Right, rect.Bottom - yradius);
			else
				point2 = new PointF(rect.Right, rect.Bottom);
			path.AddLine(point1, point2);

			// Lower right corner.
			if (round_lr)
			{
				RectangleF corner = new RectangleF( rect.Right - 2 * xradius, rect.Bottom - 2 * yradius, 2 * xradius, 2 * yradius);
				path.AddArc(corner, 0, 90);
				point1 = new PointF(rect.Right - xradius, rect.Bottom);
			}
			else point1 = new PointF(rect.Right, rect.Bottom);

			// Bottom side.
			if (round_ll)
				point2 = new PointF(rect.X + xradius, rect.Bottom);
			else
				point2 = new PointF(rect.X, rect.Bottom);
			path.AddLine(point1, point2);

			// Lower left corner.
			if (round_ll)
			{
				RectangleF corner = new RectangleF( rect.X, rect.Bottom - 2 * yradius, 2 * xradius, 2 * yradius);
				path.AddArc(corner, 90, 90);
				point1 = new PointF(rect.X, rect.Bottom - yradius);
			}
			else point1 = new PointF(rect.X, rect.Bottom);

			// Left side.
			if (round_ul)
				point2 = new PointF(rect.X, rect.Y + yradius);
			else
				point2 = new PointF(rect.X, rect.Y);
			path.AddLine(point1, point2);

			// Join with the start point.
			path.CloseFigure();

			return path;
		}
		
		public static string GetFileExtensionFromUrl(string url)
		{
			url = url.Split('?')[0];
			url = url.Split('/').Last();
			return url.Contains('.') ? url.Substring(url.LastIndexOf('.')) : "";
		}
		
		public static bool IsStringEmptyNullOrWhiteSpace(string str)
		{
			return (String.IsNullOrEmpty(str) || String.IsNullOrWhiteSpace(str));
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
		
		public static string SanitizeStringForFilePath(string episodeTitle)
		{
			string newepisodeTitle = episodeTitle;
			string invalidChars = new string(System.IO.Path.GetInvalidFileNameChars()) + new string(System.IO.Path.GetInvalidPathChars());
			foreach (char c in invalidChars)
			{
				newepisodeTitle = newepisodeTitle.Replace(c.ToString(), "");
			}
			return newepisodeTitle;
		}
	}
}
