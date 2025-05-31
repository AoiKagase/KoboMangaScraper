using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KoboScraper.models
{
    /// <summary>
    /// 本の情報
    /// </summary>
    internal class BookRecord
    {
        [DisplayName("予約")]
        public bool isChecked { get; set; } = false; // DataGridViewのチェックボックス用
        [DisplayName("発売日")]
        public string? releaseDate { get; set; }
        [DisplayName("画像")]
        [JsonIgnore]
        public Image? image { get; set; }
        [DisplayName("タイトル")]
        public string? title { get; set; }
        [DisplayName("作者")]
        public string? author { get; set; }
        [DisplayName("価格")]
        public string? price { get; set; }

        [Browsable(false)]
        public string? link { get; set; }
        [Browsable(false)]
        public string? imageLink { get; set; }
    }
}
