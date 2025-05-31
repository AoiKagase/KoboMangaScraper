namespace rakuten_scraper
{
    partial class MainWnd
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            imageList1 = new ImageList(components);
            BookListGrid = new DataGridView();
            CurrentMonthPicker = new DateTimePicker();
            BtnUpdate = new Button();
            BtnSave = new Button();
            statusStrip1 = new StatusStrip();
            ToolStripLabelStatusBook = new ToolStripStatusLabel();
            ToolStripLabelStatusImage = new ToolStripStatusLabel();
            ToolStripProgressBar = new ToolStripProgressBar();
            UpdateProgressTimer = new System.Windows.Forms.Timer(components);
            ((System.ComponentModel.ISupportInitialize)BookListGrid).BeginInit();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // imageList1
            // 
            imageList1.ColorDepth = ColorDepth.Depth32Bit;
            imageList1.ImageSize = new Size(16, 16);
            imageList1.TransparentColor = Color.Transparent;
            // 
            // BookListGrid
            // 
            BookListGrid.AllowUserToAddRows = false;
            BookListGrid.AllowUserToDeleteRows = false;
            BookListGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            BookListGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            BookListGrid.Location = new Point(24, 12);
            BookListGrid.Name = "BookListGrid";
            BookListGrid.RowHeadersVisible = false;
            BookListGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            BookListGrid.Size = new Size(899, 573);
            BookListGrid.TabIndex = 0;
            BookListGrid.CellContentDoubleClick += BookListGrid_CellContentDoubleClick;
            // 
            // CurrentMonthPicker
            // 
            CurrentMonthPicker.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            CurrentMonthPicker.Location = new Point(24, 593);
            CurrentMonthPicker.Name = "CurrentMonthPicker";
            CurrentMonthPicker.Size = new Size(101, 25);
            CurrentMonthPicker.TabIndex = 1;
            CurrentMonthPicker.ValueChanged += CurrentMonthPicker_ValueChanged;
            CurrentMonthPicker.DropDown += CurrentMonthPicker_DropDown;
            // 
            // BtnUpdate
            // 
            BtnUpdate.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            BtnUpdate.Location = new Point(848, 593);
            BtnUpdate.Name = "BtnUpdate";
            BtnUpdate.Size = new Size(75, 25);
            BtnUpdate.TabIndex = 2;
            BtnUpdate.Text = "更新";
            BtnUpdate.UseVisualStyleBackColor = true;
            BtnUpdate.Click += BtmUpdate_Click;
            // 
            // BtnSave
            // 
            BtnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            BtnSave.Location = new Point(767, 593);
            BtnSave.Name = "BtnSave";
            BtnSave.Size = new Size(75, 25);
            BtnSave.TabIndex = 3;
            BtnSave.Text = "保存";
            BtnSave.UseVisualStyleBackColor = true;
            BtnSave.Click += BtnSave_Click;
            // 
            // statusStrip1
            // 
            statusStrip1.Items.AddRange(new ToolStripItem[] { ToolStripLabelStatusBook, ToolStripLabelStatusImage, ToolStripProgressBar });
            statusStrip1.Location = new Point(0, 621);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(940, 22);
            statusStrip1.TabIndex = 4;
            statusStrip1.Text = "statusStrip1";
            // 
            // ToolStripLabelStatusBook
            // 
            ToolStripLabelStatusBook.Name = "ToolStripLabelStatusBook";
            ToolStripLabelStatusBook.Size = new Size(10, 17);
            ToolStripLabelStatusBook.Text = " ";
            ToolStripLabelStatusBook.TextAlign = ContentAlignment.BottomRight;
            // 
            // ToolStripLabelStatusImage
            // 
            ToolStripLabelStatusImage.Name = "ToolStripLabelStatusImage";
            ToolStripLabelStatusImage.Size = new Size(10, 17);
            ToolStripLabelStatusImage.Text = " ";
            // 
            // ToolStripProgressBar
            // 
            ToolStripProgressBar.Name = "ToolStripProgressBar";
            ToolStripProgressBar.Size = new Size(100, 16);
            // 
            // UpdateProgressTimer
            // 
            UpdateProgressTimer.Enabled = true;
            UpdateProgressTimer.Tick += UpdateProgressTimer_Tick;
            // 
            // MainWnd
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoScroll = true;
            ClientSize = new Size(940, 643);
            Controls.Add(statusStrip1);
            Controls.Add(BtnSave);
            Controls.Add(BtnUpdate);
            Controls.Add(CurrentMonthPicker);
            Controls.Add(BookListGrid);
            DoubleBuffered = true;
            Font = new Font("Noto Sans JP", 9F, FontStyle.Regular, GraphicsUnit.Point, 128);
            Name = "MainWnd";
            Text = "Kobo Manga Scraper";
            Load += MainWnd_LoadAsync;
            ((System.ComponentModel.ISupportInitialize)BookListGrid).EndInit();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ImageList imageList1;
        private DataGridView BookListGrid;
        private DateTimePicker CurrentMonthPicker;
        private Button BtnUpdate;
        private Button BtnSave;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel ToolStripLabelStatusBook;
        private ToolStripProgressBar ToolStripProgressBar;
        private System.Windows.Forms.Timer UpdateProgressTimer;
        private ToolStripStatusLabel ToolStripLabelStatusImage;
    }
}
