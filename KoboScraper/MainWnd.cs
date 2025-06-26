using KoboScraper.models;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace rakuten_scraper
{
	public partial class MainWnd : Form
	{
		/// -----------------------------------------------------------
		/// <summary>
		/// 気に入らないのでフォームの角丸を四角に戻す
		/// </summary>
		/// -----------------------------------------------------------
		public enum DWMWINDOWATTRIBUTE
		{
			DWMWA_WINDOW_CORNER_PREFERENCE = 33
		}

		public enum DWM_WINDOW_CORNER_PREFERENCE
		{
			DWMWCP_DEFAULT = 0,
			DWMWCP_DONOTROUND = 1,
			DWMWCP_ROUND = 2,
			DWMWCP_ROUNDSMALL = 3
		}

		[DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
		public static extern int DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref DWM_WINDOW_CORNER_PREFERENCE pvAttribute, uint cbAttribute);
		/// -----------------------------------------------------------
		/// <summary>
		/// 月指定ドロップダウン時に月までしか選択させないようにする為の宣言
		/// </summary>
		private const int DTM_GETMONTHCAL = 0x1000 + 8;
		private const int MCM_SETCURRENTVIEW = 0x1000 + 32;

		[System.Runtime.InteropServices.DllImport("user32.dll")]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);


		/// -----------------------------------------------------------
		/// <summary>
		/// Form上で利用するグローバル変数
		/// </summary>
		/// -----------------------------------------------------------
		// データバインド用のリスト
		private BindingList<BookRecord> _dataList;
		// スクレイパークラス
		private KoboScraper scraper = new KoboScraper();

		/// <summary>
		/// フォーム起動
		/// </summary>
		public MainWnd()
		{
			InitializeComponent();
		}

		#region Events
		/// <summary>
		/// フォームロード
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void MainWnd_LoadAsync(object sender, EventArgs e)
		{
			// 気に入らないので画面の角枠の丸みを消す
			var attribute = DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE;
			var preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
			DwmSetWindowAttribute(this.Handle, attribute, ref preference, sizeof(uint));

			// DatePickerのチェンジイベントが起動時も発火するため一時的に解除
			CurrentMonthPicker.ValueChanged -= CurrentMonthPicker_ValueChanged;
			// DatePickerのフォーマットをyyyy/MMへ変更し月で指定出来るように
			CurrentMonthPicker.Format = DateTimePickerFormat.Custom;
			CurrentMonthPicker.CustomFormat = "yyyy年 MM月";
			// とりあえず今日の日付で
			CurrentMonthPicker.Value = DateTime.Now;
			// 解除したイベント再登録
			CurrentMonthPicker.ValueChanged += CurrentMonthPicker_ValueChanged;

			// ステータスバー更新
			ToolStripLabelStatusBook.Text = "本一覧読込中";

			// 当月データをロード
			await LoadDataAsync(CurrentMonthPicker.Value);

			BookListGrid.EnableHeadersVisualStyles = false;
			BookListGrid.ColumnHeadersDefaultCellStyle.Font = new Font("メイリオ", 12, FontStyle.Bold);
			BookListGrid.ColumnHeadersDefaultCellStyle.Padding = new Padding(0, 8, 0, 2);
			BookListGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.Gray;
			BookListGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.Gray;
			BookListGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.White;
			BookListGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
			BookListGrid.DefaultCellStyle.Font = new Font("メイリオ", 12, FontStyle.Bold);
			BookListGrid.DefaultCellStyle.Padding = new Padding(0, 8, 0, 2);

			BookListGrid.AutoResizeColumns();


			// ステータスバーの更新
			ToolStripLabelStatusBook.Text = "本一覧読込完了";
		}

		/// <summary>
		/// 更新ボタン
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BtmUpdate_Click(object sender, EventArgs e)
		{
			// ステータスバー更新
			ToolStripLabelStatusBook.Text = "本一覧読込中";

			// 指定月の本一覧をスクレイプしなおす
			scraper.getPage(CurrentMonthPicker.Value);

			// ステータスバーの更新
			ToolStripLabelStatusBook.Text = "本一覧読込完了";
		}

		/// <summary>
		/// DataGridのダブルクリック処理
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BookListGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
		{
			// セルの内容がクリックされたときの処理
			if (e.RowIndex < 0 || e.ColumnIndex < 0)
			{
				return; // ヘッダー行やヘッダー列がクリックされた場合は何もしない
			}

			// 選択行の本をブラウザで開く
			OpenUrl(_dataList[e.RowIndex].link.ToString());
		}

		/// <summary>
		/// DataGridのキーダウン処理
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BookListGrid_KeyDown(object sender, KeyEventArgs e)
		{
			// エンターキーの場合は処理しない（自動的に次の行を選択させない）
			if (e.KeyData == Keys.Enter)
			{
				e.Handled = true;
			}
		}

		/// <summary>
		/// DataGridのキーアップ処理
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BookListGrid_KeyUp(object sender, KeyEventArgs e)
		{
			DataGridView dgv = (DataGridView)sender;
            int rowIndex = dgv.CurrentCell.OwningRow.Index;
            
			// エンターキーを押した場合はダブルクリックと同じ挙動
            switch (e.KeyData)
			{
				case Keys.Enter:
					// 選択行の本をブラウザで開く
					OpenUrl(_dataList[rowIndex].link.ToString());
					break;
				case Keys.Space:
					// スペースキーを押した場合はチェックボックスのトグル
					var cell = dgv["IsChecked", rowIndex] as DataGridViewCheckBoxCell;
					if (cell != null)
					{
						// チェック状態をトグル
						bool current = (bool)(cell.Value ?? false);
						cell.Value = !current;
						dgv.RefreshEdit();
					}
					break;
			}
		}

		/// <summary>
		/// DataGridの編集モード
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BookListGrid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
		{
			DataGridView dgv = (DataGridView)sender;

			// 編集対象が予約チェック出ない場合は編集不可
			if (dgv.Columns[e.ColumnIndex].Name != "isChecked")
			{
				//編集できないようにする
				e.Cancel = true;
			}
		}

		/// <summary>
		/// チェックボックスの表示サイズを大きくする
		/// https://stackoverflow.com/questions/36171250/how-to-change-checkbox-size-in-datagridviewcheckboxcell
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BookListGrid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
		{
			if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
			{
				DataGridView dgv = (DataGridView)sender;
				if (dgv.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn)
				{
					e.PaintBackground(e.CellBounds, true);
					ControlPaint.DrawCheckBox(e.Graphics, e.CellBounds.X + 2, e.CellBounds.Y + (e.CellBounds.Height - e.CellBounds.Width - 2) + 2,
						e.CellBounds.Width - 4, e.CellBounds.Width - 4,
						(bool)e.FormattedValue ? (ButtonState.Checked | ButtonState.Flat) : (ButtonState.Normal | ButtonState.Flat));
					e.Handled = true;
				}
			}
		}

		/// <summary>
		/// チェックボックスのポイントテスト範囲を広げる
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BookListGrid_CellClick(object sender, DataGridViewCellEventArgs e)
		{
			if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
			{
				DataGridView dgv = (DataGridView)sender;
				if (dgv.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn)
				{
					var cell = dgv[e.ColumnIndex, e.RowIndex] as DataGridViewCheckBoxCell;
					// チェック状態をトグル
					bool current = (bool)(cell.Value ?? false);
					cell.Value = !current;
					dgv.RefreshEdit();
				}
			}
		}

		/// <summary>
		/// 保存ボタン
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void BtnSave_Click(object sender, EventArgs e)
		{
			// ステータスバーの更新
			ToolStripLabelStatusBook.Text = "保存中";
			// 現在の表示データをJSONで保存する
			scraper.SaveJson(CurrentMonthPicker.Value);
			// ステータスバーの更新
			ToolStripLabelStatusBook.Text = "保存完了";
		}

		/// <summary>
		/// 月指定チェンジ
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void CurrentMonthPicker_ValueChanged(object sender, EventArgs e)
		{
			// ステータスバー更新
			ToolStripLabelStatusBook.Text = "本一覧読込中";
			// 指定した月でデータロード
			await LoadDataAsync(CurrentMonthPicker.Value);
			// ステータスバー更新
			ToolStripLabelStatusBook.Text = "本一覧読込完了";
		}

		/// <summary>
		/// 月指定ドロップダウン
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void CurrentMonthPicker_DropDown(object sender, EventArgs e)
		{
			DateTimePicker myDt = (DateTimePicker)sender;

			// 普通にカレンダー開くと日まで指定しないといけないので
			// 自動で月までカレンダーへ変更する
			IntPtr cal = SendMessage(CurrentMonthPicker.Handle, DTM_GETMONTHCAL, IntPtr.Zero, IntPtr.Zero);
			SendMessage(cal, MCM_SETCURRENTVIEW, IntPtr.Zero, (IntPtr)1);
		}

		/// <summary>
		/// プログレスバー更新用のタイマーイベント
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void UpdateProgressTimer_Tick(object sender, EventArgs e)
		{
			// 進捗によってステータスバー更新
			if (scraper.progress < 100)
				ToolStripLabelStatusImage.Text = "画像読込中";
			else
				ToolStripLabelStatusImage.Text = "画像読込完了";

			// 現在の進捗設定
			ToolStripProgressBar.Value = scraper.progress;

			// 念のため
			Application.DoEvents();
		}
		#endregion
		#region Functions
		/// <summary>
		/// 起動時のロード処理
		/// </summary>
		/// <param name="date"></param>
		/// <returns></returns>
		private async Task LoadDataAsync(DateTime date)
		{
			try
			{
				// 既存JSONデータからのロード(当月)
				bool loaded = await scraper.LoadJson(date);

				// Falseの場合は既存データが存在しない為、サーバーからスクレイプする
				if (!loaded)
					scraper.getPage(date);

				// バインド用変数への本一覧リストを格納
				// これは直接後続で直接渡しても良いかもしれない
				_dataList = scraper.books;
				LoadDataIntoDataGridView(_dataList);
			}
			catch (Exception ex)
			{
				// 念のためエラーが出たらメッセージボックス出して置く
				MessageBox.Show(ex.Message,
					"Error.",
					MessageBoxButtons.AbortRetryIgnore,
					MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// DataGridへのバインド
		/// </summary>
		/// <param name="dataList"></param>
		private void LoadDataIntoDataGridView(BindingList<BookRecord> dataList)
		{
			// データをグリッドへ読み込む
			BookListGrid.DataSource = dataList;
			// カラムの自動調整
			BookListGrid.AutoResizeColumns();
			BookListGrid.Sort(BookListGrid.Columns["releaseDate"], ListSortDirection.Descending);
			BookListGrid.Refresh();
		}
		/// <summary>
		/// URLを既定のブラウザで開く
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
		#endregion



	}
}
