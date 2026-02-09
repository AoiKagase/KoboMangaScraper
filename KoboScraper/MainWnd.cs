using KoboScraper;
using KoboScraper.models;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

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

		// グローバル変数にフォント定義を移動（毎回newしない）
		private Font _fontBold11 = new Font("Noto Sans JP", 11, FontStyle.Bold);
		private Font _fontRegular11 = new Font("Noto Sans JP", 11, FontStyle.Regular);
		private Font _fontBold12 = new Font("Noto Sans JP", 12, FontStyle.Bold);
		// グローバル変数にBindingSourceを追加
		private BindingSource? _bindingSource;
		private HashSet<string> _previousCheckedTitles = new HashSet<string>();
		private const double TITLE_MATCH_THRESHOLD = 0.75;  // 75%以上一致で判定

		/// -----------------------------------------------------------
		/// <summary>
		/// Form上で利用するグローバル変数
		/// </summary>
		/// -----------------------------------------------------------
		// データバインド用のリスト
		private SortableBindingList<BookRecord>? _dataList;
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

			// BindingSourceを初期化
			_bindingSource = new BindingSource();
			BookListGrid.DataSource = _bindingSource;

			// 当月データをロード
			await LoadDataAsync(CurrentMonthPicker.Value);
			// 過去チェック済みタイトルを事前ロード
			_previousCheckedTitles = await Task.Run(() => LoadPreviousCheckedTitles());

			BookListGrid.EnableHeadersVisualStyles = false;
            BookListGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Noto Sans JP", 11, FontStyle.Bold);
            BookListGrid.ColumnHeadersDefaultCellStyle.Padding = new Padding(0, 6, 0, 6);
            BookListGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(64, 64, 64);
            BookListGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            BookListGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

            // 行の高さを画像サイズに合わせて最適化（80px程度）
            BookListGrid.RowTemplate.Height = 85;

            BookListGrid.DefaultCellStyle.Font = _fontRegular11;
			BookListGrid.DefaultCellStyle.Padding = new Padding(4, 4, 4, 4);
            BookListGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            BookListGrid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;

            BookListGrid.GridColor = Color.LightGray;
            BookListGrid.AdvancedCellBorderStyle.All = DataGridViewAdvancedCellBorderStyle.Single;

			// 列を自動的にコンテナ幅に合わせる（横スクロール防止）
			BookListGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

			// 固定列の幅を先に設定
			if (BookListGrid.Columns["image"] != null)
				BookListGrid.Columns["image"].Width = 60;
			BookListGrid.Columns["image"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;

			if (BookListGrid.Columns["isChecked"] != null)
				BookListGrid.Columns["isChecked"].Width = 50;
			BookListGrid.Columns["isChecked"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;

			if (BookListGrid.Columns["price"] != null)
				BookListGrid.Columns["price"].Width = 100;
			BookListGrid.Columns["price"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;

			if (BookListGrid.Columns["releaseDate"] != null)
				BookListGrid.Columns["releaseDate"].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;

			// title列は残りの幅を使う（自動調整）
			if (BookListGrid.Columns["title"] != null)
				BookListGrid.Columns["title"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

			var consoleForm = new FormConsole();
            consoleForm.Show(); // 非モーダルで表示（閉じてもアプリは終了しない）
            consoleForm.StartPosition = FormStartPosition.Manual;
            consoleForm.Location = new Point(100, 100);
            consoleForm.Size = new Size(600, 400);


			// ステータスバーの更新
			ToolStripLabelStatusBook.Text = "本一覧読込完了";
        }

        // 行の色分け（チェック状態を視覚的に表示）
        private void BookListGrid_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
			if (e.RowIndex >= 0 && e.RowIndex < _dataList?.Count)
			{
				var book = _dataList[e.RowIndex];

				if (book.isChecked)
				{
					using (Brush backBrush = new SolidBrush(Color.FromArgb(200, 230, 200)))
					{
						e.Graphics?.FillRectangle(backBrush, e.RowBounds);
					}
					e.PaintParts &= ~DataGridViewPaintParts.Background;
//					e.PaintParts |= DataGridViewPaintParts.Background;
				}
				else if (IsPreviouslyChecked(book.title))
				{
					// 過去チェック済みで今月未チェックは薄黄色（ハイライト）
					using (Brush backBrush = new SolidBrush(Color.FromArgb(255, 255, 200)))  // 薄黄色
					{
						e.Graphics?.FillRectangle(backBrush, e.RowBounds);
					}
					e.PaintParts &= ~DataGridViewPaintParts.Background;
//					e.PaintParts |= DataGridViewPaintParts.Background;
				}
			}
		}
        // 画像をサムネイル化して表示
        private void BookListGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex >= 0 && BookListGrid.Columns[e.ColumnIndex].Name == "image"
                && e.Value is Image img)
            {
                // 元の画像の縦横比を保ったままサムネイル化（60x80程度）
                e.Value = ResizeImage(img, 50, 75);
            }

			// タイトル列を複数行フォーマット
			if (e.ColumnIndex >= 0 && BookListGrid.Columns[e.ColumnIndex].Name == "title"
				&& e.Value is string title)
			{
				var book = (BookRecord)((DataGridViewRow)BookListGrid.Rows[e.RowIndex]).DataBoundItem;
				// タイトル\n著者 | 価格
				e.Value = $"{title}\n{book.author}";
				e.FormattingApplied = true;
			}
		}

        // 画像リサイズ用ヘルパーメソッド
        private Image ResizeImage(Image img, int width, int height)
        {
            var resized = new Bitmap(width, height);
            using (var g = Graphics.FromImage(resized))
            {
                g.DrawImage(img, 0, 0, width, height);
            }
            return resized;
        }

        /// <summary>
        /// 更新ボタン
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void BtmUpdate_Click(object sender, EventArgs e)
        {
            ToolStripLabelStatusBook.Text = "本一覧読込中";

            try
            {
                await scraper.getPageAsync(CurrentMonthPicker.Value);
                ToolStripLabelStatusBook.Text = "本一覧読込完了";
            }
            catch (Exception ex)
            {
                ToolStripLabelStatusBook.Text = "読込失敗";
                Logger.Log(Logger.LogLevel.Warning, $"getPage failed: {ex.Message}");
            }
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

            if (_dataList == null)
                return;

            if (e.RowIndex < 0 || e.RowIndex >= _dataList.Count)
                return;

            var row = _dataList[e.RowIndex];
            if (string.IsNullOrEmpty(row.link))
                return;

            // 選択行の本をブラウザで開く
            OpenUrl(row.link.ToString());
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
            var currentCell = dgv.CurrentCell;
            if (currentCell == null)
                return;

            if (currentCell.OwningRow == null)
                return;

            int rowIndex = currentCell.OwningRow.Index;

            // エンターキーを押した場合はダブルクリックと同じ挙動
            switch (e.KeyData)
            {
                case Keys.Enter:
                    // 選択行の本をブラウザで開く
                    if (_dataList == null)
                        return;

                    if (rowIndex < 0 || rowIndex >= _dataList.Count)
                        return;

                    var cell = _dataList[rowIndex];
                    if (cell != null && !string.IsNullOrEmpty(cell.link))
                        OpenUrl(cell.link.ToString());

                    break;
                case Keys.Space:
                    // スペースキーを押した場合はチェックボックスのトグル
                    var cell_check = dgv["IsChecked", rowIndex] as DataGridViewCheckBoxCell;
                    if (cell_check != null)
                    {
                        // チェック状態をトグル
                        bool current = (bool)(cell_check.Value ?? false);
                        cell_check.Value = !current;
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

                    if (e.Graphics == null)
                        return;

                    bool? formattedValue = (bool)(e.FormattedValue ?? false);
                    if (formattedValue != null)
                    {
                        ControlPaint.DrawCheckBox(e.Graphics, e.CellBounds.X + 2, e.CellBounds.Y + (e.CellBounds.Height - e.CellBounds.Width - 2) + 2,
                            e.CellBounds.Width - 4, e.CellBounds.Width - 4,
                            (bool)formattedValue ? (ButtonState.Checked | ButtonState.Flat) : (ButtonState.Normal | ButtonState.Flat));
                        e.Handled = true;
                    }
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
                    if (cell != null)
                    {
                        bool current = (bool)(cell.Value ?? false);
                        cell.Value = !current;
                        dgv.RefreshEdit();
                    }
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
            if (scraper.IsImageLoading)
            {
                ToolStripLabelStatusImage.Text = "画像読込中";
            }
            else
                ToolStripLabelStatusImage.Text = "画像読込完了";

            // 現在の進捗設定
            ToolStripProgressBar.Value = scraper.ImageLoadProgress;
            toolStripStatusCount.Text = $"Skip: {scraper.CountBookSkipped} / Books: {scraper.CountBookLoaded}";
            toolStripStatusImages.Text = $"Loaded: {scraper.CountImageLoaded} / Books: {scraper.CountBookLoaded - scraper.CountBookSkipped}";
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
                bool loaded = scraper.LoadJson(date);

                // Falseの場合は既存データが存在しない為、サーバーからスクレイプする
                if (!loaded)
                    await scraper.getPageAsync(date);

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
        private void LoadDataIntoDataGridView(SortableBindingList<BookRecord> dataList)
        {
			if (dataList == null || _bindingSource == null)
			{
				Logger.Log(Logger.LogLevel.Error, "LoadDataIntoDataGridView: dataList or _bindingSource is null");
				return;
			}
			try
			{
				// フィルターをリセット（前回の選択を削除）
				_bindingSource.Filter = "";

				// BindingSourceのDataSourceを更新
				_bindingSource.DataSource = dataList;

				// カラムの自動調整
				BookListGrid.AutoResizeColumns();

				// 発売日列だけ折り返しを無効化
				if (BookListGrid.Columns["releaseDate"] != null)
				{
					BookListGrid.Columns["releaseDate"].DefaultCellStyle.WrapMode = DataGridViewTriState.False;
				}

				if (BookListGrid.Columns != null)
				{
					var releaseDateColumn = BookListGrid.Columns["releaseDate"];
					if (releaseDateColumn != null)
						BookListGrid.Sort(releaseDateColumn, ListSortDirection.Descending);
				}
				BookListGrid.Refresh();
			}
			catch (Exception ex)
			{
				Logger.Log(Logger.LogLevel.Error, $"LoadDataIntoDataGridView failed: {ex.Message}");
			}
		}
        /// <summary>
        /// URLを既定のブラウザで開く
        /// </summary>
        /// <param name="url">URL</param>
        /// <returns>Process</returns>
        private Process? OpenUrl(string url)
        {
            ProcessStartInfo pi = new ProcessStartInfo()
            {
                FileName = url,
                UseShellExecute = true,
            };

            return Process.Start(pi);
        }
        #endregion



        private void toolStripStatusCount_Click(object sender, EventArgs e)
        {

        }

        private void BtnReloadImg_Click(object sender, EventArgs e)
        {
            if (scraper == null || scraper.books == null)
            {
                MessageBox.Show("本一覧が読み込まれていません。先に本一覧を読み込んでください。",
                    "Error.",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
            else
            {
                if (!scraper.IsImageLoading)
                {
                    // 画像再読込
                    scraper.StartLoadImageThread(CurrentMonthPicker.Value);
                }
                else
                {
                    MessageBox.Show("画像読込中です。しばらくお待ちください。",
                        "Info.",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }

        private void BtnShowUncheckedOnly_Click(object sender, EventArgs e)
        {
			if (_bindingSource == null) return;
			var button = (Button)sender;

			// 現在のスクロール位置を保存
			int currentScrollIndex = BookListGrid.FirstDisplayedScrollingRowIndex;
			if (currentScrollIndex < 0) currentScrollIndex = 0;

			if (button.Text == "未チェックのみ")
			{
				// 未チェック項目のみの新しいSortableBindingListを作成
				var filtered = new SortableBindingList<BookRecord>();
				foreach (var item in _dataList.Where(b => !b.isChecked))
				{
					filtered.Add(item);
				}
				_bindingSource.DataSource = filtered;
				button.Text = "全て表示";

				// スクロール位置を復元（フィルタ後の行数に調整）
				if (filtered.Count > 0)
				{
					int newScrollIndex = Math.Min(currentScrollIndex, filtered.Count - 1);
					BookListGrid.FirstDisplayedScrollingRowIndex = newScrollIndex;
				}
			}
			else
			{
				// 元のリストに戻す
				_bindingSource.DataSource = _dataList;
				button.Text = "未チェックのみ";

				// スクロール位置を復元
				if (_dataList.Count > 0)
				{
					int newScrollIndex = Math.Min(currentScrollIndex, _dataList.Count - 1);
					BookListGrid.FirstDisplayedScrollingRowIndex = newScrollIndex;
				}
			}

			BookListGrid.Refresh();
		}

		/// <summary>
		/// 2つのタイトルの類似度を計算（前方一致ベース）
		/// </summary>
		private double CalculateTitleSimilarity(string title1, string title2)
		{
			if (string.IsNullOrEmpty(title1) || string.IsNullOrEmpty(title2))
				return 0;

			// 短い方の長さを基準に、先頭からマッチする割合を計算
			int minLength = Math.Min(title1.Length, title2.Length);
			int maxLength = Math.Max(title1.Length, title2.Length);

			int matchCount = 0;
			for (int i = 0; i < minLength; i++)
			{
				if (title1[i] == title2[i])
					matchCount++;
				else
					break;  // 前方一致が途切れたら終了
			}

			// マッチした長さ / 長い方の長さ = 類似度
			return (double)matchCount / maxLength;
		}
		/// <summary>
		/// 過去にチェック済みかどうかを判定（類似度ベース）
		/// </summary>
		private bool IsPreviouslyChecked(string? title)
		{
			if (string.IsNullOrEmpty(title))
				return false;

			string normalizedTitle = Common.NormalizeTitle(title);

			// 完全一致チェック
			if (_previousCheckedTitles.Contains(normalizedTitle))
				return true;

			// 類似度チェック
			foreach (var previousTitle in _previousCheckedTitles)
			{
				double similarity = CalculateTitleSimilarity(normalizedTitle, previousTitle);
				if (similarity >= TITLE_MATCH_THRESHOLD)
					return true;
			}

			return false;
		}

		/// <summary>
		/// 過去のチェック済みタイトルを読み込む
		/// </summary>
		private HashSet<string> LoadPreviousCheckedTitles()
		{
			var checkedTitles = new HashSet<string>();

			string dataDir = "data";

			// dataディレクトリが存在しない場合は返す
			if (!Directory.Exists(dataDir))
				return checkedTitles;

			try
			{
				// 全ての_reservations.jsonファイルを取得
				var files = Directory.GetFiles(dataDir, "*_reservations.json");

				foreach (var file in files)
				{
					try
					{
						string json = File.ReadAllText(file);
						var reservations = JsonSerializer.Deserialize<SortableBindingList<BookReservation>>(json);

						if (reservations != null)
						{
							foreach (var reservation in reservations)
							{
								string normalized = Common.NormalizeTitle(reservation.title);
								if (!string.IsNullOrEmpty(normalized))
									checkedTitles.Add(normalized);
							}
						}
					}
					catch { }
				}
			}
			catch { }

			return checkedTitles;
		}
	}
}
