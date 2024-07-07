using System.Collections.Generic;

namespace HandsLiftedApp.Core.Models.Library.Config
{
    public class LibraryConfig
    {
        public List<LibraryDefinition> LibraryItems { get; set; } = new();

        public class LibraryDefinition
        {
            public string Label { get; set; }
            public string Directory { get; set; }
        }
    }
}