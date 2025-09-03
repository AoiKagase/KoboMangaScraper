using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using KoboScraper;
using KoboScraper.models;
using System.Buffers.Text;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

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
		public int ImageLoadProgress { get; set; }
		public int CountBookLoaded { get; set; } = 0;
		public int CountBookSkipped { get; set; } = 0;
		public int CountImageLoaded { get; set; } = 0;
		public bool IsImageLoading { get; private set; } = false;
		/// <summary>
		/// 予約情報
		/// </summary>
		private SortableBindingList<BookReservation>? reservations { get; set; } = new SortableBindingList<BookReservation>();

		/// <summary>
		/// AngelSharpの設定
		/// </summary>
		private IConfiguration config;
		private IBrowsingContext context;
		private DefaultHttpRequester requester;
		private IDocumentLoader? loader;

		/// <summary>
		/// 画像ロード用スレッド
		/// </summary>
		private volatile Task? imgLoader;
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
					.WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = false })
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
		public async Task getPageAsync(DateTime date)
		{
			// 画像を取得する為の処理が既に走ってる場合はキャンセルさせる
			if (IsImageLoading)
				await cts.CancelAsync();

			// 取得する本のリストを初期化
			books.Clear();

			// ページコントロールがめんどくさいのでとりあえず1000ページくらい回す
			// 100で良いじゃろと思ったら200ページのパターンがあったわ
			CountBookLoaded = 0;
			CountBookSkipped = 0;
			var addresses = Dns.GetHostAddressesAsync("books.rakuten.co.jp");

			for (int i = 1; i < 1000; i++)
			{ 
				try
				{
					// とりあえずこのURLなら今のところいける
					string baseurl = $"https://books.rakuten.co.jp/calendar/101904/monthly/";
					Url address = new Url(baseurl);

					// クエリパラメータを設定
					address.SearchParams.Set("tid", date.ToString("yyyy-MM-01"));	// 指定月の1日を指定する
					address.SearchParams.Set("s", "14");							// 発売日順
					address.SearchParams.Set("p", i.ToString());                    // ページ番号
					address.Fragment = "rclist";                                    // 表示形式(リスト表示)
					Debug.WriteLine($"URL: {address.ToString()}");
					var swStep = Stopwatch.StartNew();

					// 楽天Koboのページを開く
					using (var document = await context.OpenAsync(address))
					{
						swStep.Stop();
						Debug.WriteLine($"[{i}] OpenAsync: {swStep.ElapsedMilliseconds} ms");
						swStep.Restart();

						// 本一覧を取得
						var booksElement = document.QuerySelector(".rb-items-list--list");
						swStep.Stop();
						Debug.WriteLine($"[{i}] QuerySelector: {swStep.ElapsedMilliseconds} ms");
						swStep.Restart();
						// Nullなら取得出来てないので1000ページ回すまでもなく終わる
						if (booksElement == null)
							break;

						// 本のリスト分回す
						foreach (var book in booksElement.GetElementsByClassName("item"))
						{
							// 表示させるための本の情報作る
							BookRecord bookRecord = new BookRecord();

							// パース
							var releaseDate = book.GetElementsByClassName("item-release__date");
							var title = book.GetElementsByClassName("item-title");
							var author = book.GetElementsByClassName("item-author__name");
							var price = book.GetElementsByClassName("item-pricing__price");
							var img = book.GetElementsByTagName("img");
							var imgLink = (img.Length > 0) ? ((IHtmlImageElement)img[0]).Source?.Trim() : "";
							imgLink = imgLink?.Split('?')[0]; // 画像URLの後ろにパラメータが付いてる場合があるので除去
							// リリース日
							bookRecord.releaseDate = (releaseDate.Length > 0) ? releaseDate[0].TextContent.Trim() : "";
							// タイトル
							if (title.Length > 0)
							{
								bookRecord.title = title[0].GetElementsByClassName("item-title__text")[0]?.TextContent.Trim();
								bookRecord.link = title[0].GetElementsByTagName("a")[0]?.GetAttribute("href")?.ToString().Trim();
							}
							// 作者
							bookRecord.author = (author.Length > 0) ? author[0].TextContent.Trim() : "";
							// 価格
							bookRecord.price = (price.Length > 0) ? price[0].TextContent.Trim() : "";
							// 画像リンク
							bookRecord.imageLink = imgLink;

							CountBookLoaded++;

							// むかつく分冊版の場合は排除するためスキップ
							if (IsOneshotEpisode(bookRecord))
							{
								CountBookSkipped++;
								continue;
							}

							// 本の情報を追加
							books.Add(bookRecord);
						}
						swStep.Stop();
						Debug.WriteLine($"[{i}] Parse: {swStep.ElapsedMilliseconds} ms");
						swStep.Restart();
						await Task.Delay(1);
					}
				}
				catch (Exception e)
				{
					Debug.WriteLine(e.Message);
					break;
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
		public bool LoadJson(DateTime date)
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

				var deserialized = JsonSerializer.Deserialize<SortableBindingList<BookRecord>>(json);
				if (deserialized != null)
					books = deserialized;
				else
					books = new SortableBindingList<BookRecord>();

				// 0件超えるなら画像を読み込む
				if (books.Count > 0)
				{
					// 非同期でロードする
					// StartLoadImageThread();

					// ついでに予約情報を持っているか読み込む
					GetReservations(date);

					CountBookLoaded = books.Count;
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

			if (reservations == null)
				reservations = new SortableBindingList<BookReservation>();

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
			this.ImageLoadProgress = Math.Min((int)Math.Round(((100.0f / books.Count) * value), 0, MidpointRounding.ToPositiveInfinity), 100);
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
			if (book == null)
				return false;

			if (string.IsNullOrEmpty(book.title))
				return false;

			// LinQとか書けないのでとりあえず回す
			foreach (string regex in OneshotRegex)
			{
				if (Regex.IsMatch(book.title, regex))
				{
					return true;
				}
			}

			if (string.IsNullOrEmpty(book.price))
				return false;

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
		private async Task ImageLoaderAsync()
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
						dcRequester.Headers["User-Agent"] = this.UserAgent;
						dcRequester.Headers["DNT"] = "1"; // Do Not Track
						dcRequester.Headers["Sec-Ch-Ua"] = "\"Not)A;Brand\";v=\"8\", \"Chromium\";v=\"138\", \"Google Chrome\";v=\"138\"";
						dcRequester.Headers["Sec-Ch-Ua-Mobile"] = "?0";
						dcRequester.Headers["Sec-Ch-Ua-Platform"] = "Windows";
						dcRequester.Headers["Sec-Fetch-Dest"] = "image";
						dcRequester.Headers["Sec-Fetch-Mode"] = "no-cors";
						dcRequester.Headers["Sec-Fetch-Site"] = "cross-site";
						dcRequester.Headers["Sec-Fetch-Storage-Access"] = "active";
						dcRequester.Headers["Accept"] = "image/jpeg,image/*,*/*;q=0.8";
						dcRequester.Headers["Accept-Encoding"] = "gzip, deflate, br, zstd";
						dcRequester.Headers["Accept-Language"] = "ja-JP,ja;q=0.9,en-US;q=0.8,en;q=0.7";
						dcRequester.Headers["Cache-Control"] = "no-cache";
						dcRequester.Headers["Pragma"] = "no-cache";
						dcRequester.Headers["Priority"] = "i";
						dcRequester.Headers["Referer"] = "https://books.rakuten.co.jp/";

						if (loader == null)
							break;

						// 画像URLへアクセス
						var downlaod = loader.FetchAsync(dcRequester);

						using (var response = await downlaod.Task)
						{
							if (response?.Content != null)
							{
								using (MemoryStream ms = new MemoryStream())
								{
									// Byte情報を取得してImage化する
									await response.Content.CopyToAsync(ms);
									ms.Position = 0;
									book.image ??= Common.MemoryToImage(ms);
								}
							}
						}
					}
					// 途中でThreadキャンセルが発生した場合はここで止める
					cts.Token.ThrowIfCancellationRequested();

					// プログレスバー用のカウンタを設定
					setProgress(i);
					CountImageLoaded = i;
					i++;
					await Task.Delay(1);
				}
			}
			//catch (Exception ex)
			//{
			//}
			IsImageLoading = false;
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
					if (reservations == null)
						break;

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
		public void StartLoadImageThread()
		{
			IsImageLoading = true;

			// 画像ロードは非同期で行う
			imgLoader = Task.Run(async () => {
				try
				{
					await ImageLoaderAsync();
				}
				finally
				{
					IsImageLoading = false;
				}
			}, cts.Token);
		}

		/// <summary>
		/// 非同期の画像データ読み込み処理をキャンセル
		/// </summary>
		/// 
		public void CancelLoadImageThread()
		{
			// 画像を取得する為の処理が既に走ってる場合はキャンセルさせる
			if (imgLoader != null && imgLoader.Status == TaskStatus.Running)
				cts.Cancel();
		}

		private static DefaultHttpRequester CreateBrowserLikeRequester(string userAgent, bool image = false)
		{
			var requester = new DefaultHttpRequester();
			requester.Headers["User-Agent"] = userAgent;

			requester.Headers["DNT"] = "1"; // Do Not Track
			requester.Headers["Sec-Ch-Ua"] = "\"Not)A;Brand\";v=\"8\", \"Chromium\";v=\"138\", \"Google Chrome\";v=\"138\"";
			requester.Headers["Sec-Ch-Ua-Mobile"] = "?0";
			requester.Headers["Sec-Ch-Ua-Platform"] = "Windows";
			requester.Headers["Sec-Fetch-Dest"] = "document";
			requester.Headers["Sec-Fetch-Mode"] = "navigate";
			requester.Headers["Sec-Fetch-Site"] = "same-origin";
			requester.Headers["Connection"] = "keep-alive";
			requester.Headers["Sec-Fetch-Storage-Access"] = "active";
			requester.Headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
			requester.Headers["Accept-Encoding"] = "gzip, deflate";
			requester.Headers["Accept-Language"] = "ja-JP,ja;q=0.9,en-US;q=0.8,en;q=0.7";
			requester.Headers["Cache-Control"] = "no-cache";
			requester.Headers["Pragma"] = "no-cache";
			requester.Headers["Referer"] = "https://books.rakuten.co.jp/";
			return requester;
		}

		#endregion
	}
}