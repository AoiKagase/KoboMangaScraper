using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace rakuten_scraper
{
    /// <summary>
    /// 本の情報
    /// </summary>
    internal class BookItem
    {
        public string? releaseDate { get; set; }
        public Image? image { get; set; }
        public string? title { get; set; }
        public string? author { get; set; }
        public string? price { get; set; }
        public Url? link { get; set; }
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
        /// コンストラクタ
        /// </summary>
        public KoboScraper()
        {
        }

        /// <summary>
        /// 新刊のページを日付ベースで取得
        /// </summary>
        /// <param name="date">基準日</param>
        public async void getPage(DateTime date)
        {
            // AngelSharpの設定
            // よく分からん
            var config = Configuration.Default
                                      .WithDefaultLoader()
                                      .WithDefaultCookies();
            var context = BrowsingContext.New(config);

            // とりあえずUser-Agentは誤魔化す
            var requester = context.GetService<DefaultHttpRequester>();
            requester.Headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/47.0.2526.106 Safari/537.36";

            // ページコントロールがめんどくさいのでとりあえず100ページくらい回す
            for (int i = 0; i < 100; i++)
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

                        // リリース日
                        bookItem.releaseDate = book.GetElementsByClassName("item-release__date")[0].TextContent;
                        // タイトル
                        var title = book.GetElementsByClassName("item-title")[0];
                        bookItem.title = title.GetElementsByClassName("item-title__text")[0].TextContent;
                        bookItem.link = new Url(title.GetElementsByTagName("a")[0]?.GetAttribute("href")?.ToString());
                        // 作者
                        bookItem.author = book.GetElementsByClassName("item-author__name")[0].TextContent;
                        // 価格
                        bookItem.price = book.GetElementsByClassName("item-pricing__price")[0].TextContent;

                        // 画像を取得する為の処理
                        var loader = context.GetService<IDocumentLoader>();
                        var response = await loader.FetchAsync(new DocumentRequest(new Url(((IHtmlImageElement)book.GetElementsByTagName("img")[0]).Source))).Task;
                        using (var ms = new MemoryStream())
                        {
                            await response.Content.CopyToAsync(ms);
                            var bytes = ms.ToArray();
                            bookItem.image = ByteArrayToImage(bytes);
                        }

                        // むかつく分冊版の排除
                        if (IsOneshotEpisode(bookItem.title))
                            continue;

                        // 本の情報を追加
                        books.Add(bookItem);
                    }
                }
            }
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
        private bool IsOneshotEpisode(string title)
        {
            // LinQとか書けないのでとりあえず回す
            foreach (string regex in OneshotRegex)
            {
                if (Regex.IsMatch(title, regex))
                {
                    return true;
                }
            }
            return false;
        }
    }
}