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
