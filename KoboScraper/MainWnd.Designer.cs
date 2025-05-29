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
            dateTimePicker1 = new DateTimePicker();
            button1 = new Button();
            button2 = new Button();
            ((System.ComponentModel.ISupportInitialize)BookListGrid).BeginInit();
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
            BookListGrid.Location = new Point(12, 14);
            BookListGrid.Name = "BookListGrid";
            BookListGrid.RowHeadersVisible = false;
            BookListGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            BookListGrid.Size = new Size(890, 570);
            BookListGrid.TabIndex = 0;
            BookListGrid.CellContentClick += BookListGrid_CellContentClick;
            BookListGrid.CellContentDoubleClick += BookListGrid_CellContentDoubleClick;
            // 
            // dateTimePicker1
            // 
            dateTimePicker1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            dateTimePicker1.Location = new Point(12, 590);
            dateTimePicker1.Name = "dateTimePicker1";
            dateTimePicker1.Size = new Size(101, 25);
            dateTimePicker1.TabIndex = 1;
            // 
            // button1
            // 
            button1.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            button1.Location = new Point(827, 591);
            button1.Name = "button1";
            button1.Size = new Size(75, 26);
            button1.TabIndex = 2;
            button1.Text = "更新";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // button2
            // 
            button2.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            button2.Location = new Point(746, 591);
            button2.Name = "button2";
            button2.Size = new Size(75, 26);
            button2.TabIndex = 3;
            button2.Text = "保存";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // MainWnd
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoScroll = true;
            ClientSize = new Size(914, 626);
            Controls.Add(button2);
            Controls.Add(button1);
            Controls.Add(dateTimePicker1);
            Controls.Add(BookListGrid);
            Font = new Font("Noto Sans JP", 9F, FontStyle.Regular, GraphicsUnit.Point, 128);
            Name = "MainWnd";
            Text = "Kobo Manga Scraper";
            Load += Form1_LoadAsync;
            ((System.ComponentModel.ISupportInitialize)BookListGrid).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private ImageList imageList1;
        private DataGridView BookListGrid;
        private DateTimePicker dateTimePicker1;
        private Button button1;
        private Button button2;
    }
}
