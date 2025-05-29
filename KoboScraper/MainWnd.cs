using System.ComponentModel;
using System.Diagnostics;
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

        private async void Form1_LoadAsync(object sender, EventArgs e)
        {
            dateTimePicker1.ValueChanged -= dateTimePicker1_ValueChanged;
            dateTimePicker1.Format = DateTimePickerFormat.Custom;
            dateTimePicker1.CustomFormat = "yyyy/MM";
            dateTimePicker1.Value = DateTime.Now;
            dateTimePicker1.ValueChanged += dateTimePicker1_ValueChanged;

            await LoadDataAsync();
            MessageBox.Show("�Ǎ�����", "Load", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task LoadDataAsync()
        {
            try
            {
                bool loaded = await scraper.LoadJson(dateTimePicker1.Value);
                if (!loaded)
                {
                    scraper.getPage(dateTimePicker1.Value);
                }
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
            // �f�[�^��ǂݍ���
            BookListGrid.DataSource = dataList;
            BookListGrid.AutoResizeColumns();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            scraper.getPage(dateTimePicker1.Value);
        }

        private void BookListGrid_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void BookListGrid_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            // �Z���̓��e���N���b�N���ꂽ�Ƃ��̏���
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return; // �w�b�_�[�s��w�b�_�[�񂪃N���b�N���ꂽ�ꍇ�͉������Ȃ�
            }
            OpenUrl(_dataList[e.RowIndex].link.ToString());
        }
        /// <summary>
        /// URL������̃u���E�U�ŊJ��
        /// </summary>
        /// <param name="url">URL</param>
        /// <returns>Process</returns>
        private Process OpenUrl(string url)
        {
            ProcessStartInfo pi = new ProcessStartInfo()
            {
                FileName = url,
                UseShellExecute = true,
            };

            return Process.Start(pi);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            scraper.SaveJson(dateTimePicker1.Value);
            MessageBox.Show("�ۑ�����", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {
            await LoadDataAsync();
            MessageBox.Show("�Ǎ�����", "Load", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
