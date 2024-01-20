using ReactiveUI;
using System;

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
