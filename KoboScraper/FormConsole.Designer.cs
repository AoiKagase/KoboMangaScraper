namespace KoboScraper
{
	partial class FormConsole
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
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
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			richTextBox1 = new RichTextBox();
			SuspendLayout();
			// 
			// richTextBox1
			// 
			richTextBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			richTextBox1.BackColor = Color.Black;
			richTextBox1.Font = new Font("Noto Sans JP", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
			richTextBox1.ForeColor = Color.DarkGray;
			richTextBox1.Location = new Point(12, 12);
			richTextBox1.Name = "richTextBox1";
			richTextBox1.ReadOnly = true;
			richTextBox1.ScrollBars = RichTextBoxScrollBars.Vertical;
			richTextBox1.Size = new Size(776, 426);
			richTextBox1.TabIndex = 1;
			richTextBox1.Text = "";
			// 
			// FormConsole
			// 
			AutoScaleDimensions = new SizeF(7F, 15F);
			AutoScaleMode = AutoScaleMode.Font;
			ClientSize = new Size(800, 450);
			Controls.Add(richTextBox1);
			Name = "FormConsole";
			Text = "LogViewer";
			Load += FormConsole_Load;
			ResumeLayout(false);
		}

		#endregion

		private RichTextBox richTextBox1;
	}
}