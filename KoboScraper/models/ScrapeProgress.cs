using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KoboScraper.models
{
	public class ScrapeProgress
	{
		public bool IsImageLoading { get; set; }
		public int ImageLoadProgress { get; set; }
		public int CountBookLoaded { get; set; }
		public int CountBookSkipped { get; set; }
		public int CountImageLoaded { get; set; }
		public string? StatusMessage { get; set; }
	}
}
