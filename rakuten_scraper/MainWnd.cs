using System.ComponentModel;

namespace rakuten_scraper
{
    public partial class MainWnd : Form
    {
        private BindingList<BookItem> _dataList;

        public MainWnd()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                KoboScraper scraper = new();
                scraper.getPage(new DateTime(2025,06,01));
                _dataList = scraper.books;
                LoadDataIntoDataGridView(_dataList);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message,
                    "Error.",
                    MessageBoxButtons.AbortRetryIgnore,
                    MessageBoxIcon.Error);
            }
        }
        private void LoadDataIntoDataGridView(BindingList<BookItem> dataList)
        {
            // ÉfÅ[É^Çì«Ç›çûÇﬁ
            BookListGrid.DataSource = dataList;
        }
    }
}
