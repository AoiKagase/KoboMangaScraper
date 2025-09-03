using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Image = SixLabors.ImageSharp.Image;
using Rectangle = System.Drawing.Rectangle;

namespace KoboScraper
{
	internal class Common
	{
		/// <summary>  
		/// https://yossy.penne.jp/wordpress/2024/02/05/c-base64/  
		/// </summary>  
		/// <param name="image"></param>  
		/// <returns></returns>  
		public static string ImageToBase64(System.Drawing.Image image)
		{
			using (MemoryStream m = new MemoryStream())
			{
				// Fix: Use ImageFormat instead of PixelFormat  
				image.Save(m, ImageFormat.Png);
				byte[] imageBytes = m.ToArray();
				return Convert.ToBase64String(imageBytes);
			}
		}

		/// <summary>  
		/// https://yossy.penne.jp/wordpress/2024/02/05/c-base64/  
		/// </summary>  
		/// <param name="base64String"></param>  
		/// <returns></returns>  
		public static System.Drawing.Image Base64ToImage(string base64String)
		{
			byte[] imageBytes = Convert.FromBase64String(base64String);
			using (MemoryStream ms = new MemoryStream(imageBytes))
			{
				System.Drawing.Image? img = MemoryToImage(ms);
				if (img != null)
					return img;
				else
					throw new Exception("Base64ToImage failed");
			}
		}

		/// <summary>  
		///  
		/// https://stackoverflow.com/questions/33691228/resize-image-with-percentage-value-in-c-sharp  
		/// </summary>  
		/// <param name="image"></param>  
		/// <param name="resize"></param>  
		/// <returns></returns>  
		public static System.Drawing.Image ResizeImage(System.Drawing.Image image, float resize)
		{
			int width = (int)(image.Width * resize);
			int height = (int)(image.Height * resize);

			var destRect = new Rectangle(0, 0, width, height);
			var destImage = new Bitmap(width, height);

			destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

			using (var graphics = Graphics.FromImage(destImage))
			{
				graphics.CompositingMode = CompositingMode.SourceCopy;
				graphics.CompositingQuality = CompositingQuality.HighQuality;
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

				using (var wrapMode = new ImageAttributes())
				{
					wrapMode.SetWrapMode(WrapMode.TileFlipXY);
					graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
				}
			}

			return destImage;
		}

		/// <summary>  
		/// MemoryStreamをImageオブジェクトに変換  
		/// </summary>  
		/// <param name="ms">変換前MemoryStream</param>  
		/// <returns>Imageデータ</returns>  
		public static System.Drawing.Image? MemoryToImage(MemoryStream ms)
		{
			Image? img = null;
			try
			{
				using (MemoryStream outms = new MemoryStream())
				{
					img = Image.Load(ms);
					img.SaveAsJpeg(outms);
					return System.Drawing.Image.FromStream(outms);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error converting byte array to image: " + ex.Message);
			}
			return null;
		}
	}
}
