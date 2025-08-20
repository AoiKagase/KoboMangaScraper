using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Text.Json;
using System.Text.RegularExpressions;
using KoboScraper.models;
using KoboScraper;
using System.Diagnostics;

namespace rakuten_scraper
{
	/// <summary>
	/// 楽天Kobo専用スクレイパー
	/// </summary>
	internal class KoboScraper
	{
		#region グローバル変数
		/// <summary>
		/// DataGridViewに対応する本の情報リスト
		/// </summary>
		public SortableBindingList<BookRecord> books { get; set; } = new SortableBindingList<BookRecord>();
		/// <summary>
		/// プログレスバー用
		/// </summary>
		public int progress { get; set; }
		public int countLoaded { get; set; } = 0;
		public int countSkipped { get; set; } = 0;

		/// <summary>
		/// 予約情報
		/// </summary>
		private SortableBindingList<BookReservation> reservations { get; set; } = new SortableBindingList<BookReservation>();

		/// <summary>
		/// AngelSharpの設定
		/// </summary>
		private IConfiguration config;
		private IBrowsingContext context;
		private DefaultHttpRequester requester;
		private IDocumentLoader loader;

		/// <summary>
		/// 画像ロード用スレッド
		/// </summary>
		private Task imgLoader;
		private CancellationTokenSource cts = new CancellationTokenSource();
		private string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36 Edg/139.0.0.0";
		/// <summary>
		/// むかつく分冊版に含まれるキーワード
		/// これをすり抜けてくるやつ（通常のタイトルと見分けがつかない）
		/// が居るのは確認済みなのでどうすりゃええねん
		/// 正規表現でも頑張ってみるが、引っ掛けられないやつは居る
		/// もうちょっとワードの最適化したい気もするのでご意見求む
		/// </summary>
		private static string[] OneshotRegex =
		{
			"単話",
			"分冊",
			"話売り",
			"ばら売り",
			"話】",
			"連載版",
			"【短編】",
			"第[0-9|０-９]+話",
		};

		#endregion

		#region Class Method
		/// <summary>
		/// コンストラクタ
		/// </summary>
		public KoboScraper()
		{
			// AngelSharpの設定
			// よく分からん
			requester = CreateBrowserLikeRequester(this.UserAgent, false);

			config = Configuration.Default
					.With(requester)
					.WithDefaultLoader()
					.WithDefaultCookies();
			context = BrowsingContext.New(config);

			// とりあえずUser-Agentは誤魔化す

			// 画像取得用にloaderを作成しとく
			loader = context.GetService<IDocumentLoader>();
		}

		/// <summary>
		/// 新刊のページを日付ベースで取得
		/// </summary>
		/// <param name="date">基準日</param>
		public async void getPage(DateTime date)
		{
			// 画像を取得する為の処理が既に走ってる場合はキャンセルさせる
			if (imgLoader != null && imgLoader.Status == TaskStatus.Running)
				cts.Cancel();

			// 取得する本のリストを初期化
			books.Clear();

			// ページコントロールがめんどくさいのでとりあえず1000ページくらい回す
			// 100で良いじゃろと思ったら200ページのパターンがあったわ
			countLoaded = 0;
			countSkipped = 0;

			for (int i = 1; i < 1000; i++)
			{
				// とりあえずこのURLなら今のところいける
				string urlstring = string.Format(@"https://books.rakuten.co.jp/calendar/101904/monthly/?tid={0}&s=14&p={1}#rclist", date.ToString("yyyy-MM-01"), i);

				// 楽天Koboのページを開く
				var address = Url.Create(urlstring);
				using (var document = await context.OpenAsync(address))
				{
					// 本一覧を取得
					var booksElement = document.QuerySelector(".rb-items-list--list");

					// Nullなら取得出来てないので1000ページ回すまでもなく終わる
					if (booksElement == null)
						break;

					// 本のリスト分回す
					foreach (var book in booksElement.GetElementsByClassName("item"))
					{
						// 表示させるための本の情報作る
						BookRecord bookRecord = new BookRecord();

						// パース
						var releaseDate         = book.GetElementsByClassName("item-release__date");
						var title               = book.GetElementsByClassName("item-title");
						var author              = book.GetElementsByClassName("item-author__name");
						var price               = book.GetElementsByClassName("item-pricing__price");
						var img                 = book.GetElementsByTagName("img");
						var imgLink             = (img.Length > 0) ? ((IHtmlImageElement)img[0]).Source?.Trim() : "";

						// リリース日
						bookRecord.releaseDate  = (releaseDate.Length > 0) ? releaseDate[0].TextContent.Trim() : "";
						// タイトル
						if (title.Length > 0)
						{
							bookRecord.title      = title[0].GetElementsByClassName("item-title__text")[0]?.TextContent.Trim();
							bookRecord.link       = title[0].GetElementsByTagName("a")[0]?.GetAttribute("href")?.ToString().Trim();
						}
						// 作者
						bookRecord.author         = (author.Length > 0) ? author[0].TextContent.Trim() : "";
						// 価格
						bookRecord.price          = (price.Length > 0)  ? price[0].TextContent.Trim() : "";
						// 画像リンク
						bookRecord.imageLink      = imgLink;

						countLoaded++;

						// むかつく分冊版の場合は排除するためスキップ
						if (IsOneshotEpisode(bookRecord))
						{
							countSkipped++;
							continue;
						}

						await Task.Delay(10);
						// 本の情報を追加
						books.Add(bookRecord);
					}
				}
			}

			// 画像のロード処理
			// 非同期で行う
			StartLoadImageThread();

			// 予約情報の取得
			GetReservations(date);

			// 全ての情報を取得し終えたらデータを次回起動時の為に保存しておく
			SaveJson(date);
		}

		/// <summary>
		/// 既にJSONデータが存在する場合はロードする
		/// </summary>
		/// <param name="date"></param>
		/// <returns></returns>
		public async Task<bool> LoadJson(DateTime date)
		{
			// 画像を取得する為の処理が既に走ってる場合はキャンセルさせる
			CancelLoadImageThread();

			// 指定月で既に取得済みの本一覧データ
			string filename = "data/" + date.ToString("yyyy-MM") + ".json";

			// 存在する場合
			if (File.Exists(filename))
			{
				// 読み込んでリストへ展開する
				string json = File.ReadAllText(filename);
				books = JsonSerializer.Deserialize<SortableBindingList<BookRecord>>(json);

				// 0件超えるなら画像を読み込む
				if (books.Count > 0)
				{
					// 非同期でロードする
					// StartLoadImageThread();

					// ついでに予約情報を持っているか読み込む
					GetReservations(date);

					// 読み込んだら正常終了
					return true;
				}
			}

			// 既に取得済みの本一覧は無かった
			return false;
		}

		/// <summary>
		/// 指定月の本一覧データを保存する
		/// 予約チェックが入っている場合は予約情報データとして別ファイルへ保存
		/// </summary>
		/// <param name="date"></param>
		public void SaveJson(DateTime date)
		{
			// 本一覧データをJSON化
			string json = JsonSerializer.Serialize(books, new JsonSerializerOptions { WriteIndented = true });

			// JSON をファイルに保存
			File.WriteAllText("data/" + date.ToString("yyyy-MM") + ".json", json);

			// 予約情報を一旦クリア
			reservations.Clear();

			// 現在の本一覧データ分回す
			foreach (BookRecord bookRecord in books)
			{
				// 予約チェックが入っている場合は予約リストへ追加する
				if (bookRecord.isChecked)
				{
					BookReservation reservation = new BookReservation();
					reservation.title = bookRecord.title;
					reservation.releaseDate = bookRecord.releaseDate;
					reservations.Add(reservation);
				}
			}

			// 予約情報リストをJSON化
			json = JsonSerializer.Serialize(reservations, new JsonSerializerOptions { WriteIndented = true });

			// JSON をファイルに保存
			File.WriteAllText("data/" + date.ToString("yyyy-MM") + "_reservations.json", json);
		}

		/// <summary>
		/// プログレスバーの設定
		/// </summary>
		/// <param name="value"></param>
		public void setProgress(int value)
		{
			this.progress = Math.Min((int)Math.Round(((100.0f / books.Count) * value), 0, MidpointRounding.ToPositiveInfinity), 100);
		}
		#endregion

		#region Private Method


		/// <summary>
		/// むかつく分冊版に含まれるキーワードがタイトルに含まれているかチェック
		/// </summary>
		/// <param name="title"></param>
		/// <returns></returns>
		private bool IsOneshotEpisode(BookRecord book)
		{
			// LinQとか書けないのでとりあえず回す
			foreach (string regex in OneshotRegex)
			{
				if (Regex.IsMatch(book.title, regex))
				{
					return true;
				}
			}

			// 値段が300円以下なら分冊版とみなす強硬手段
			var price = int.Parse(book.price.Replace("円", ""), System.Globalization.NumberStyles.AllowThousands);
			if (price <= 300)
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// 画像ロード処理
		/// </summary>
		private async void ImageLoader()
		{
			int i = 0;
			//try
			{
				// ループ中にスレッドキャンセルが発生した場合に
				// 母体が消えるのでエラーが発生する為一時退避したものを利用する
				BindingList<BookRecord> tempBooks = this.books;

				// 既に取得済みの本一覧分回す
				foreach (BookRecord book in tempBooks)
				{
					DocumentRequest dcRequester = new DocumentRequest(new Url(book.imageLink));
					{
						// User-Agentを設定
						dcRequester.Headers["User-Agent"] = this.UserAgent;
						dcRequester.Headers["Upgrade-Insecure-Requests"] = "1";
						dcRequester.Headers["DNT"] = "1"; // Do Not Track
						dcRequester.Headers["Sec-Ch-Ua"] = "\"Not;A=Brand\"; v = \"99\", \"Microsoft Edge\"; v = \"139\", \"Chromium\"; v = \"139\"";
						dcRequester.Headers["Sec-Ch-Ua-Mobile"] = "?0";
						dcRequester.Headers["Sec-Ch-Ua-Platform"] = "Windows";

						// 画像URLへアクセス
						var response = await loader.FetchAsync(dcRequester).Task;
						if (response?.Content != null)
						{
							using (MemoryStream ms = new MemoryStream())
							{
								// Byte情報を取得してImage化する
								await response.Content.CopyToAsync(ms);
								var bytes = ms.ToArray();
								book.image ??= Common.ByteArrayToImage(bytes);
								//using (var image = Image.FromStream(ms))
								//{
								//	book.image = (Image)image.Clone(); // 必要ならクローン
								//}

							}
						}

					}
					// 途中でThreadキャンセルが発生した場合はここで止める
					cts.Token.ThrowIfCancellationRequested();

					// プログレスバー用のカウンタを設定
					setProgress(i);
					i++;
					await Task.Delay(100);
				}
			}
			//catch (Exception ex)
			//{
			//}
		}

		/// <summary>
		/// 予約情報の取得
		/// </summary>
		/// <param name="date"></param>
		private void GetReservations(DateTime date)
		{
			// 指定月の予約情報ファイル
			string filename = "data/" + date.ToString("yyyy-MM") + "_reservations.json";

			// 存在するなら
			if (File.Exists(filename))
			{
				// 読み込んでリスト展開する
				string json = File.ReadAllText(filename);
				reservations = JsonSerializer.Deserialize<SortableBindingList<BookReservation>>(json);

				// 本一覧リストとの突合せを行う
				foreach (BookRecord book in books)
				{
					foreach (BookReservation reservation in reservations)
					{
						// 該当の本と予約本が一致する場合は既に予約済みとみなしチェックさせる
						if (book.title == reservation.title && book.releaseDate == reservation.releaseDate)
						{
							book.isChecked = true;
						}
					}
				}
			}
		}

		/// <summary>
		/// 非同期の画像データ読み込み処理
		/// </summary>
		private void StartLoadImageThread()
		{
			// 画像ロードは非同期で行う
			imgLoader = Task.Run(() => { ImageLoader(); return Task.CompletedTask; }, cts.Token);
		}

		/// <summary>
		/// 非同期の画像データ読み込み処理をキャンセル
		/// </summary>
		private void CancelLoadImageThread()
		{
			// 画像を取得する為の処理が既に走ってる場合はキャンセルさせる
			if (imgLoader != null && imgLoader.Status == TaskStatus.Running)
				cts.Cancel();
		}

		private static DefaultHttpRequester CreateBrowserLikeRequester(string userAgent, bool image = false)
		{
			var requester = new DefaultHttpRequester();
			requester.Headers["User-Agent"] = userAgent;
			if (image)
				requester.Headers["Accept"] = "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8";
			else
				requester.Headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";
	
			requester.Headers["Accept-Language"] = "ja,en-US;q=0.7,en;q=0.3";
			requester.Headers["Accept-Encoding"] = "gzip, deflate, br";
			requester.Headers["Cache-Control"] = "no-cache";
			requester.Headers["Pragma"] = "no-cache";
			requester.Headers["Upgrade-Insecure-Requests"] = "1";
			requester.Headers["Sec-Fetch-Dest"] = "document";
			requester.Headers["Sec-Fetch-Mode"] = "navigate";
			requester.Headers["Sec-Fetch-Site"] = "cross-site";
			requester.Headers["Sec-Fetch-User"] = "?1";
			requester.Headers["DNT"] = "1";
			requester.Headers["Sec-Ch-Ua"] = "\"Not;A=Brand\";v=\"99\", \"Microsoft Edge\";v=\"139\", \"Chromium\";v=\"139\"";
			requester.Headers["Sec-Ch-Ua-Mobile"] = "?0";
			requester.Headers["Sec-Ch-Ua-Platform"] = "Windows";
			return requester;
		}

		#endregion
	}
}