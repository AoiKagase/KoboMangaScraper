using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
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
		public static string ImageToBase64(Image image)
		{
			using (MemoryStream m = new MemoryStream())
			{
				// Fix: Use ImageFormat instead of PixelFormat 
				image.SaveAsJpeg(m);
				byte[] imageBytes = m.ToArray();
				return Convert.ToBase64String(imageBytes);
			}
		}
		public static string ImageToBase64(MemoryStream image)
		{
			Image img = Image.Load(image);

			// ImageSharpのIImageFormatを自動検出
			image.Position = 0;
			IImageFormat format = SixLabors.ImageSharp.Image.DetectFormat(image);
			image.Position = 0;
			return img.ToBase64String(format);
		}
		/// <summary>  
		/// https://yossy.penne.jp/wordpress/2024/02/05/c-base64/  
		/// </summary>  
		/// <param name="base64String"></param>  
		/// <returns></returns>  
		public static System.Drawing.Image? Base64ToImage(string base64String)
		{
			string[] imgstring = base64String.Split("base64,");
			byte[] imageBytes = Convert.FromBase64String(imgstring.Length > 1 ? imgstring[1] : imgstring[0]);
			using (MemoryStream ms = new MemoryStream(imageBytes))
			{
				var base64 = "";
				System.Drawing.Image? img = MemoryToImage(ms, 1.0f, out base64);
				if (img != null)
					return img;
				else
					Logger.Log(Logger.LogLevel.Debug, $"Base64ToImage failed {base64String}");
				return null;
				//					throw new Exception("Base64ToImage failed");
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
		/// ImargeSharpで画像をリサイズする
		/// </summary>
		/// <param name="img"></param>
		/// <param name="resize"></param>
		/// <returns></returns>
		public static Image ResizeImage(Image img, float resize)
		{
			int width = (int)(img.Width * resize);
			int height = (int)(img.Height * resize);
			img.Mutate(x => x.Resize(width, height));
			return img;
		}

		/// <summary>  
		/// MemoryStreamをImageオブジェクトに変換  
		/// </summary>  
		/// <param name="ms">変換前MemoryStream</param>  
		/// <returns>Imageデータ</returns>  
		public static System.Drawing.Image? MemoryToImage(MemoryStream ms, float resize, out string base64)
		{
			Image? img = null;
			base64 = "";
			try
			{
				using (MemoryStream outms = new MemoryStream())
				{
					img = Image.Load(ms);
					if (resize != 1.0f)
						img = ResizeImage(img, resize);
					img.SaveAsJpeg(outms);
					base64 = img.ToBase64String(SixLabors.ImageSharp.Formats.Jpeg.JpegFormat.Instance);

					// ① byte[] に一度書き出す（outmsはここで役目終了）
					byte[] imageBytes = outms.ToArray();

					// ② byte[] から新しい MemoryStream を作る
					//    → この ms2 は Dispose しない（Imageが参照し続けるため）
					var ms2 = new MemoryStream(imageBytes);
					return System.Drawing.Image.FromStream(ms2);
					//     ↑ ms2 は閉じられないので Image が安全に参照できる
				}
			}
			catch (Exception ex)
			{
				Logger.Log(Logger.LogLevel.Debug, $"Error converting byte array to image: {ex.Message}");
			}
			return null;
		}

		/// <summary>
		/// タイトルを正規化（スペースと数字を除去）
		/// </summary>
		public static string NormalizeTitle(string title)
		{
			if (string.IsNullOrEmpty(title))
				return "";

			string normalized = System.Text.RegularExpressions.Regex.Replace(title, @"[\s\d]+", "");
			return normalized;
		}
	}
}
