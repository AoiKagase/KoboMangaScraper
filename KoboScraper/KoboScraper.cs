using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using KoboScraper;
using KoboScraper.models;
using StreamJsonRpc;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
		private int _countBookLoaded = 0;
		public int CountBookLoaded { get => _countBookLoaded; set => _countBookLoaded = value; }
		private int _countBookSkipped = 0;
		public int CountBookSkipped { get => _countBookSkipped; set => _countBookSkipped = value; }
		private int _countImageLoded = 0;
		public int CountImageLoaded { get => _countImageLoded; set => _countImageLoded = value; }
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
			"特装版",
			"単話",
			"分冊",
			"話売り",
			"ばら売り",
			"話】",
			"連載版",
			"【短編】",
			@"第\s*[0-9０-９]+話",
		};

		/// <summary>
		/// スレッド最大数
		/// </summary>
		private const int MAX_THREAD_COUNT = 4;
		/// <summary>
		/// 分冊版排除のスコア閾値
		/// </summary>
		private const int ONSHOT_SCORE = 3;
		/// <summary>
		/// MAXリトライ回数
		/// </summary>
		private const int MAX_RETRY_COUNT = 4;
		#endregion

		#region Class Method
		/// <summary>
		/// コンストラクタ
		/// </summary>
		public KoboScraper()
		{
			// AngelSharpの設定
			// よく分からん
			requester = CreateBrowserLikeRequester(this.UserAgent);

			config = Configuration.Default
					.With(requester)
					.WithDefaultLoader(new LoaderOptions { IsResourceLoadingEnabled = false })
					.WithDefaultCookies();
			context = BrowsingContext.New(config);

			// とりあえずUser-Agentは誤魔化す

			// 画像取得用にloaderを作成しとく
			loader = context.GetService<IDocumentLoader>();
		}

		private Url SetUrlParameter(DateTime date, string baseurl, int pageIndex)
		{
			var address = new Url(baseurl);

			// クエリパラメータを設定
			address.SearchParams.Set("tid", date.ToString("yyyy-MM-01"));   // 指定月の1日を指定する
			address.SearchParams.Set("s", "14");                            // 発売日順
			address.SearchParams.Set("p", pageIndex.ToString());            // ページ番号
			address.Fragment = "rclist";                                    // 表示形式(リスト表示)

			return address;
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

			CountBookLoaded = 0;
			CountBookSkipped = 0;

			var semaphore = new SemaphoreSlim(MAX_THREAD_COUNT); // 最大4並列
			var tempBooks = new List<BookRecord>();
			var tasks = new List<Task>();

			int currentPage = 1;
			bool stopFlag = false;
			object pageLock = new object();

			var localContext = BrowsingContext.New(config); // context を共有しない
			var document = null as IDocument;

			for (int t = 0; t < MAX_THREAD_COUNT; t++)
			{
				tasks.Add(Task.Run(async () =>
				{
					while (true)
					{
						await semaphore.WaitAsync();
						int pageIndex;
						lock (pageLock)
						{
							// ページコントロールがめんどくさいのでとりあえず1000ページくらい回す
							// 100で良いじゃろと思ったら200ページのパターンがあったわ
							if (stopFlag || currentPage >= 1000)
							{
								semaphore.Release();
								break;
							}
							pageIndex = currentPage++;
						}

						try
						{
							// とりあえずこのURLなら今のところいける
							string baseurl = $"https://books.rakuten.co.jp/calendar/101904/monthly/";
							var address = SetUrlParameter(date, baseurl, pageIndex);

							Logger.Log(Logger.LogLevel.Debug, $"Fetching page {pageIndex} for {date:yyyy-MM}");
							Logger.Log(Logger.LogLevel.Info, $"URL: {address}");

							var swStep = Stopwatch.StartNew();

							// ページを開く
							// 失敗したらリトライする
							for (int retry = 0; retry < MAX_THREAD_COUNT; retry++)
							{
								document = await localContext.OpenAsync(address);
								if (document != null || document?.StatusCode == HttpStatusCode.OK)
								{
									break;
								}
								else
								{
									await Task.Delay(1000 * (retry + 1)); // Exponential backoff
									Logger.Log(Logger.LogLevel.Warning, $"[{pageIndex}] Retry {retry + 1}/{MAX_RETRY_COUNT} for page load.");
									continue;
								}
							}

							// 楽天Koboのページを開く
							using (document)
							{
								swStep.Stop();
								Logger.Log(Logger.LogLevel.Debug, $"[{pageIndex}] OpenAsync: {swStep.ElapsedMilliseconds} ms");

								// ページから本のリストを取得
								var booksElement = document.QuerySelector(".rb-items-list--list");

								// Nullなら取得出来てないのでストップフラグを立てる
								if (booksElement == null || booksElement.GetElementsByClassName("item").Length == 0)
								{
									Logger.Log(Logger.LogLevel.Info, $"[{pageIndex}] Empty page detected. Stopping.");
									lock (pageLock)
									{
										stopFlag = true;
									}
									break;
								}

								// 取得した本リストを、ぶん回す
								foreach (var book in booksElement.GetElementsByClassName("item"))
								{
									// 表示させるための本の情報作る
									var bookRecord = new BookRecord();

									// パース
									var releaseDate = book.GetElementsByClassName("item-release__date");
									var title = book.GetElementsByClassName("item-title");
									var author = book.GetElementsByClassName("item-author__name");
									var price = book.GetElementsByClassName("item-pricing__price");
									var img = book.GetElementsByTagName("img");
									var imgLink = (img.Length > 0) ? ((IHtmlImageElement)img[0]).Source?.Trim() : "";

									// リリース日
									bookRecord.releaseDate = (releaseDate.Length > 0) ? releaseDate[0].TextContent.Trim() : "";

									// タイトル
									if (title.Length > 0)
									{
										bookRecord.title = title[0].GetElementsByClassName("item-title__text")[0]?.TextContent.Trim();
										bookRecord.link = title[0].GetElementsByTagName("a")[0]?.GetAttribute("href")?.Trim();
									}

									// 作者
									bookRecord.author = (author.Length > 0) ? author[0].TextContent.Trim() : "";
									// 価格
									bookRecord.price = (price.Length > 0) ? price[0].TextContent.Trim() : "";
									// 画像リンク
									bookRecord.imageLink = imgLink;

									Interlocked.Increment(ref _countBookLoaded);

									// むかつく分冊版の場合は排除するためスキップ
									if (IsOneshotEpisode(bookRecord))
									{
										Interlocked.Increment(ref _countBookSkipped);
										continue;
									}

									// 本の情報を追加
									lock (tempBooks)
									{
										tempBooks.Add(bookRecord);
									}
								}
							}
						}
						catch (Exception ex)
						{
							Logger.Log(Logger.LogLevel.Error, $"[{pageIndex}] Error: {ex.Message}");
						}
						finally
						{
							semaphore.Release();
						}
					}
				}));
			}

			await Task.WhenAll(tasks);

			// 取得した本の情報を画面用リストへ追加
			lock (books)
			{
				foreach (var b in tempBooks)
					books.Add(b);
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
			if (book == null || string.IsNullOrEmpty(book.title))
				return false;

			int score = 0;

			// LinQとか書けないのでとりあえず回す
			foreach (string pattern in OneshotRegex)
			{
				if (Regex.IsMatch(book.title, pattern))
					score += 2;
			}

			if (!string.IsNullOrEmpty(book.price))
			{
				// 値段が指定価格以下なら分冊版とみなす強硬手段
				if (int.TryParse(book.price.Replace("円", ""), out int parsedPrice))
				{
					// 500円以下なら1ポイント加算
					if (parsedPrice <= 500)
						score += 1;
					// 300円以下なら2ポイント加算
					if (parsedPrice <= 300)
						score += 2;
					// 100円以下なら3ポイント加算
					if (parsedPrice <= 100)
						score += 3;
				}
			}
			// 合本や完全版など、分冊ではないキーワードが含まれていれば減点
			if (book.title.Contains("合本") || book.title.Contains("完全版"))
				score -= 4;


			return score >= ONSHOT_SCORE;
		}

		/// <summary>
		/// 画像ロード処理
		/// </summary>
		private async Task ImageLoaderAsync()
		{
			IsImageLoading = true;

			var swGlobal = Stopwatch.StartNew();
			var semaphore = new SemaphoreSlim(MAX_THREAD_COUNT); // 最大並列数（調整可能）
			var tempBooks = this.books.ToList(); // スレッドセーフなコピー
			var tasks = new List<Task>();

			int index = 0;
			for (int i = 0; i < tempBooks.Count; i++)
			{
				var book = tempBooks[i];

				// 画像ダウンロード処理をスレッド化
				tasks.Add(Task.Run(async () =>
				{
					await semaphore.WaitAsync();
					var swStep = Stopwatch.StartNew();

					try
					{
						// キャンセルされてたら即終了
						cts.Token.ThrowIfCancellationRequested();

						// リクエストヘッダの生成
						var dcRequester = CreateBrowserLikeRequesterForImage(this.UserAgent, book.imageLink, book.imageEtag, book.imageLastModified);

						// DEBUG: ログ出力
						Logger.Log(Logger.LogLevel.Debug, $"[{index}] ----------------------------------------------");
						Logger.Log(Logger.LogLevel.Debug, $"[{index}] Initialize: {swStep.ElapsedMilliseconds} ms");
						swStep.Restart();

						if (loader == null)
							return;

						for (int retry = 0; retry < MAX_RETRY_COUNT; retry++)
						{
							// 画像のダウンロード
							var download = loader.FetchAsync(dcRequester);
							using (var response = await download.Task)
							{
								// DEBUG: ログ出力
								swStep.Stop();
								Logger.Log(Logger.LogLevel.Debug, $"[{index}] FetchAsync: {swStep.ElapsedMilliseconds} ms");
								Logger.Log(Logger.LogLevel.Debug, $"[{index}] StatusCode: {response.StatusCode}");
								switch (response?.StatusCode)
								{
									case HttpStatusCode.OK:
										swStep.Restart();

										if (response?.Content != null)
										{
											using (var ms = new MemoryStream())
											{
												await response.Content.CopyToAsync(ms);
												ms.Position = 0;
												string base64 = "";
												book.image ??= Common.MemoryToImage(ms, 0.5f, out base64);
												book.imgSrc = base64;
												book.imageEtag = response.Headers?["Etag"];
												book.imageLastModified = response.Headers?["Last-Modified"];
											}
										}
										swStep.Stop();
										Logger.Log(Logger.LogLevel.Debug, $"[{index}] Convert Image: {swStep.ElapsedMilliseconds} ms");
										Logger.Log(Logger.LogLevel.Info, $"[{index}] Image Loaded");
										Interlocked.Increment(ref index);
										setProgress(index);
										CountImageLoaded = index;
										return;
									case HttpStatusCode.NotModified:
										Logger.Log(Logger.LogLevel.Info, $"[{index}] Image Load Skipped (ETag matched)");
										Interlocked.Increment(ref index);
										setProgress(index);
										CountImageLoaded = index;
										return;
									default:
										Logger.Log(Logger.LogLevel.Error, $"[{index}] Image Load Failed: {response?.StatusCode}");
										if (retry < MAX_RETRY_COUNT)
										{
											await Task.Delay(1000 * (retry + 1)); // Exponential backoff
											Logger.Log(Logger.LogLevel.Error, $"[{index}] Max retries reached. Skipping image.");
											continue;
										}
										else
										{
											// 失敗した場合はスキップ
											Interlocked.Increment(ref index);
											setProgress(index);
											CountImageLoaded = index;
										}
										return;
								}
							}
						}
					}
					catch (Exception ex)
					{
						Logger.Log(Logger.LogLevel.Error, $"[{index}] Exception: {ex.Message}");
					}
					finally
					{
						semaphore.Release();
					}
				}));
			}

			await Task.WhenAll(tasks);
			IsImageLoading = false;
			Logger.Log(Logger.LogLevel.Info, $"All images loaded in {swGlobal.ElapsedMilliseconds} ms");
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

		private static DefaultHttpRequester CreateBrowserLikeRequester(string userAgent)
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

		private static DocumentRequest CreateBrowserLikeRequesterForImage(string userAgent, string? url, string? eTag, string? lastModified)
		{
			var dcRequester = new DocumentRequest(new Url(url));
			dcRequester.Headers["User-Agent"] = userAgent;
			dcRequester.Headers["DNT"] = "1";
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
			dcRequester.Headers["Referer"] = "https://books.rakuten.co.jp/";
			dcRequester.Headers["Connection"] = "keep-alive";
			dcRequester.Headers["If-None-Match"] = eTag ?? "";
			if (!string.IsNullOrEmpty(lastModified))
				dcRequester.Headers["If-Modified-Since"] = lastModified;

			return dcRequester;
		}
		#endregion
	}
}