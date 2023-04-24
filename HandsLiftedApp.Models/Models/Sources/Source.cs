using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Data.Models.Sources
{
    [Serializable]
    public class Source : ReactiveObject
    {
        public SearchModes? SearchMode { get; set; }
        public string? Filter { get; set; }
        public string? Path { get; set; }

        public enum SearchModes
        {
            MostRecent
        }
    }
}
