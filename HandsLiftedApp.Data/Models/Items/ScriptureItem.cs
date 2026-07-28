using System;
using System.Xml.Serialization;
using ReactiveUI;

namespace HandsLiftedApp.Data.Models.Items
{
    // Deliberately stores only the passage reference, not cached parsed content:
    // HandsLiftedApp.Data has no dependency on HandsLiftedApp.Importer.Scripture (and shouldn't gain
    // one), and ScriptureLocalUsxStore already caches parsed USX in memory, reading from a local,
    // user-configured directory (see AppPreferencesViewModel.ScriptureDataPath) rather than the network.
    [XmlRoot("Scripture", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class ScriptureItem : Item
    {
        private string _translation = "";
        public string Translation { get => _translation; set => this.RaiseAndSetIfChanged(ref _translation, value); }

        private string _book = "";
        public string Book { get => _book; set => this.RaiseAndSetIfChanged(ref _book, value); }

        private int _startChapter = 1;
        public int StartChapter { get => _startChapter; set => this.RaiseAndSetIfChanged(ref _startChapter, value); }

        private int _startVerse = 1;
        public int StartVerse { get => _startVerse; set => this.RaiseAndSetIfChanged(ref _startVerse, value); }

        private int _endChapter = 1;
        public int EndChapter { get => _endChapter; set => this.RaiseAndSetIfChanged(ref _endChapter, value); }

        private int _endVerse = 1;
        public int EndVerse { get => _endVerse; set => this.RaiseAndSetIfChanged(ref _endVerse, value); }
    }
}
