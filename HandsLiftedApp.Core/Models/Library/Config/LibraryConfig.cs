using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HandsLiftedApp.Core.Models.Library.Config
{
    public enum LibraryType { Song, Media, Scripture }

    public class LibraryConfig
    {
        // ObservableCollection so the Setup window's library table can bind to this directly and
        // have add/remove reflected live, instead of editing a separate copy.
        public ObservableCollection<LibraryDefinition> LibraryItems { get; set; } = new();

        public class LibraryDefinition : INotifyPropertyChanged
        {
            private string _label;
            public string Label { get => _label; set => SetField(ref _label, value); }

            private string _icon;
            public string Icon { get => _icon; set => SetField(ref _icon, value); }

            private string _directory;
            public string Directory { get => _directory; set => SetField(ref _directory, value); }

            private LibraryType _type = LibraryType.Media;
            public LibraryType Type { get => _type; set => SetField(ref _type, value); }

            public event PropertyChangedEventHandler? PropertyChanged;

            // Setting Directory from the Browse folder picker (code-behind, not a binding round-trip)
            // needs this to make the bound TextBox pick up the change.
            private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
