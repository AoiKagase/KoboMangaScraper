using System.ComponentModel;
using System.Windows.Forms;

namespace rakuten_scraper
{
    public partial class MainWnd : Form
    {
        private BindingList<BookItem> _dataList;
        private KoboScraper scraper = new KoboScraper();

        public MainWnd()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                dateTimePicker1.Value = DateTime.Now;
                scraper.getPage(dateTimePicker1.Value);
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

        private void button1_Click(object sender, EventArgs e)
        {
            scraper.getPage(dateTimePicker1.Value);
        }
    }
}
