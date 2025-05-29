using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace rakuten_scraper
{
    /// <summary>
    /// 本の情報
    /// </summary>
    internal class BookItem
    {
        [DisplayName("予約")]
        public bool isChecked { get; set; } = false; // DataGridViewのチェックボックス用
        public string? releaseDate { get; set; }
        [JsonIgnore]
        public Image? image { get; set; }
        public string? title { get; set; }
        public string? author { get; set; }
        public string? price { get; set; }

        [Browsable(false)]
        public string? link { get; set; }
        [Browsable(false)]
        public string? imageLink { get; set; }
    }

    /// <summary>
    /// 楽天Kobo専用スクレイパー
    /// </summary>
    internal class KoboScraper
    {
        /// <summary>
        /// DataGridViewに対応する本の情報リスト
        /// </summary>
        public BindingList<BookItem> books { get; set; } = new BindingList<BookItem>();

        /// <summary>
        /// AngelSharpの設定
        /// </summary>
        private IConfiguration config;
        private IBrowsingContext context;
        private DefaultHttpRequester requester;
        private IDocumentLoader loader;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public KoboScraper()
        {
            // AngelSharpの設定
            // よく分からん
            config = Configuration.Default
                                      .WithDefaultLoader()
                                      .WithDefaultCookies();
            context = BrowsingContext.New(config);

            // とりあえずUser-Agentは誤魔化す
            requester = context.GetService<DefaultHttpRequester>();
            requester.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/47.0.2526.106 Safari/537.36";

            // 画像取得用にloaderを作成しとく
            loader = context.GetService<IDocumentLoader>();
        }

        /// <summary>
        /// 新刊のページを日付ベースで取得
        /// </summary>
        /// <param name="date">基準日</param>
        public async void getPage(DateTime date)
        {
            // 取得する本のリストを初期化
            books.Clear();

            // ページコントロールがめんどくさいのでとりあえず1000ページくらい回す
            // 100で良いじゃろと思ったら200ページのパターンがあったわ
            for (int i = 0; i < 1000; i++)
            {
                // とりあえずこのURLなら今のところいける
                string urlstring = string.Format(@"https://books.rakuten.co.jp/calendar/101904/monthly/?tid={0}&s=14&p={1}#rclist", date.ToString("yyyy-MM-dd"), i);

                // ページ開く
                var address = Url.Create(urlstring);
                using (var document = await context.OpenAsync(address))
                {
                    // 本一覧
                    var booksElement = document.QuerySelector(".rb-items-list--list");

                    // Nullなら取得出来てないので100ページ回すまでもなく終わる
                    if (booksElement == null)
                        break;

                    // 本のリスト分回す
                    foreach (var book in booksElement.GetElementsByClassName("item"))
                    {
                        // 表示させるための本の情報作る
                        BookItem bookItem = new BookItem();

                        // パース
                        var releaseDate = book.GetElementsByClassName("item-release__date");
                        var title       = book.GetElementsByClassName("item-title");
                        var author      = book.GetElementsByClassName("item-author__name");
                        var price       = book.GetElementsByClassName("item-pricing__price");
                        var img         = book.GetElementsByTagName("img");
                        var imgLink     = (img.Length > 0) ? ((IHtmlImageElement)img[0]).Source?.Trim() : "";

                        // リリース日
                        bookItem.releaseDate= (releaseDate.Length > 0) ? releaseDate[0].TextContent.Trim() : "";
                        // タイトル
                        if (title.Length > 0)
                        {
                            bookItem.title  = title[0].GetElementsByClassName("item-title__text")[0]?.TextContent.Trim();
                            bookItem.link   = title[0].GetElementsByTagName("a")[0]?.GetAttribute("href")?.ToString().Trim();
                        }
                        // 作者
                        bookItem.author     = (author.Length > 0) ? author[0].TextContent.Trim() : "";
                        // 価格
                        bookItem.price      = (price.Length > 0)  ? price[0].TextContent.Trim() : "";
                        // 画像リンク
                        bookItem.imageLink = imgLink;

                        // 画像を取得する為の処理
                        var response = await loader.FetchAsync(new DocumentRequest(new Url(imgLink))).Task;
                        using (var ms = new MemoryStream())
                        {
                            await response.Content.CopyToAsync(ms);
                            var bytes = ms.ToArray();
                            bookItem.image = ByteArrayToImage(bytes);
                        }

                        // むかつく分冊版の排除
                        if (IsOneshotEpisode(bookItem))
                            continue;

                        // 本の情報を追加
                        books.Add(bookItem);
                    }
                }
            }
            SaveJson(date);
        }

        /// <summary>
        /// バイト配列をImageオブジェクトに変換
        /// </summary>
        /// <param name="b">変換前のバイト配列</param>
        /// <returns>Imageデータ</returns>
        public static Image ByteArrayToImage(byte[] b)
        {
            ImageConverter imgconv = new ImageConverter();
            Image img = (Image)imgconv.ConvertFrom(b);
            return img;
        }

        /// <summary>
        /// むかつく分冊版に含まれるキーワード
        /// これをすり抜けてくるやつ（通常のタイトルと見分けがつかない）
        /// が居るのは確認済みなのでどうすりゃええねん
        /// 正規表現でも頑張ってみるが、引っ掛けられないやつは居る
        /// もうちょっとワードの最適化したい気もするのでご意見求む
        /// </summary>
        string[] OneshotRegex =
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

        /// <summary>
        /// むかつく分冊版に含まれるキーワードがタイトルに含まれているかチェック
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        private bool IsOneshotEpisode(BookItem book)
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

        public async Task<bool>LoadJson(DateTime date)
        {
            string filename = date.ToString("yyyy-MM") + ".json";
            if (File.Exists(filename))
            {
                string json = File.ReadAllText(filename);
                books = JsonSerializer.Deserialize<BindingList<BookItem>>(json);
                if (books.Count > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        foreach (BookItem book in this.books)
                        {
                            // 画像を取得する為の処理
                            var response = await loader.FetchAsync(new DocumentRequest(new Url(book.imageLink))).Task;
                            using (var ms = new MemoryStream())
                            {
                                await response.Content.CopyToAsync(ms);
                                var bytes = ms.ToArray();
                                book.image = ByteArrayToImage(bytes);
                            }
                        }
                    }); // 画像ロードは非同期で行う
                    return true; // loaded.
                }
            }
            return false; // json not found.
        }

        public void SaveJson(DateTime date)
        {
            string json = JsonSerializer.Serialize(books, new JsonSerializerOptions { WriteIndented = true });

            // JSON をファイルに保存
            File.WriteAllText(date.ToString("yyyy-MM") + ".json", json);
        }
    }
}