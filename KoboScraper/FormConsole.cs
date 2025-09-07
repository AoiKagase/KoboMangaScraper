using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static KoboScraper.Logger;

namespace KoboScraper
{
	public partial class FormConsole : Form
	{
		public FormConsole()
		{
			InitializeComponent();
		}

		private void FormConsole_Load(object sender, EventArgs e)
		{
			Logger.Output += AppendLog;
		}

		private void AppendLog(Logger.LogLevel level, string text)
		{
			if (InvokeRequired)
			{
				Invoke(new Action(() => AppendLog(level, text)));
				return;
			}

			Color color = level switch
			{
				LogLevel.Info => Color.LightGray,
				LogLevel.Warning => Color.Goldenrod,
				LogLevel.Error => Color.Red,
				LogLevel.Debug => Color.DodgerBlue,
				_ => Color.White
			};

			int start = richTextBox1.TextLength;
			richTextBox1.SelectionStart = start;
			richTextBox1.SelectionLength = 0;
			richTextBox1.SelectionColor = color;

			richTextBox1.AppendText(text + Environment.NewLine);

			richTextBox1.SelectionStart = richTextBox1.TextLength;
			richTextBox1.ScrollToCaret();
		}
	}
}
