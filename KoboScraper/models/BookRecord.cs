using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Image = System.Drawing.Image;

namespace KoboScraper.models
{
	/// <summary>
	/// 本の情報
	/// </summary>
	internal class BookRecord
	{
		private Image? _image;
		private string? _imgSrc;
		private string? _releaseDate;

		[DisplayName("予約")]
		public bool isChecked { get; set; } = false; // DataGridViewのチェックボックス用
		[DisplayName("発売日")]
		[JsonIgnore]
		[ReadOnly(true)]
		public string ReleaseDate
		{
			get {
				if (DateTime.TryParse(_releaseDate, out DateTime date))
					return date.ToString("MM月dd日");
				else
					return "";
			}
		}
		[Browsable(false)]
		public string? releaseDate
		{
			get { return _releaseDate; }
			set { _releaseDate = !string.IsNullOrEmpty(value) ? value : ""; }
		}
		[DisplayName("画像")]
		[ReadOnly(true)]
		[JsonIgnore]
		public Image? image
		{
			get { return _image; }
			set
			{
				if (value != null)
				{
					// ロード時に1/2リサイズする
					_image = Common.ResizeImage(value, 0.2f);

					// 画像ロードしたら次回起動時用にBase64形式も確保
					_imgSrc = Common.ImageToBase64(_image);
				}
			}
		}

		[DisplayName("タイトル")]
		[ReadOnly(true)]
		public string? title { get; set; }
		[DisplayName("作者")]
		[ReadOnly(true)]
		public string? author { get; set; }
		[DisplayName("価格")]
		[ReadOnly(true)]
		public string? price { get; set; }

		[Browsable(false)]
		public string? imgSrc
		{
			get { return _imgSrc; }
			set
			{
				// Base64形式で持っているはずで、起動時にロードされる物
				_imgSrc = value;
				if (_imgSrc != null)
					_image = Common.Base64ToImage(_imgSrc);
			}
		}
		[Browsable(false)]
		public string? link { get; set; }
		[Browsable(false)]
		public string? imageLink { get; set; }
	}
}
