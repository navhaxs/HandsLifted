using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Importer.Scripture;
using ReactiveUI;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public class ScriptureItemInstance : ScriptureItem, IItemInstance, IItemDirtyBit
    {
        public PlaylistInstance? ParentPlaylist { get; set; }

        public event EventHandler? ItemDataModified;

        private readonly ScriptureSourceLoader _loader;

        public ScriptureItemInstance(PlaylistInstance? parentPlaylist, ScriptureSourceLoader? loader = null) : base()
        {
            ParentPlaylist = parentPlaylist;
            _loader = loader ?? new ScriptureSourceLoader();

            // Deliberately no .ObserveOn(RxApp.MainThreadScheduler) here (unlike SongItemInstance's
            // equivalent chain): that scheduler depends on Avalonia.ReactiveUI's dispatcher registration,
            // which isn't guaranteed to run in a unit-test host, and this phase does nothing that
            // requires cross-thread marshaling. Keeping it synchronous makes ActiveSlide update
            // deterministically and immediately when SelectedSlideIndex or Slides changes.
            _activeSlide = this.WhenAnyValue(x => x.SelectedSlideIndex, x => x.Slides,
                    (selectedSlideIndex, slides) => slides.ElementAtOrDefault(selectedSlideIndex))
                .ToProperty(this, x => x.ActiveSlide);

            this.WhenAnyValue(
                i => i.Title,
                i => i.Translation,
                i => i.Book,
                i => i.StartChapter,
                i => i.StartVerse,
                i => i.EndChapter,
                i => i.EndVerse
            ).Subscribe(_ =>
            {
                ItemDataModified?.Invoke(this, EventArgs.Empty);
            });
        }

        private ObservableCollection<Slide> _slides = new ObservableCollection<Slide>();
        public ObservableCollection<Slide> Slides => _slides;

        private int _selectedSlideIndex = -1;
        public int SelectedSlideIndex
        {
            get => _selectedSlideIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedSlideIndex, value);
        }

        private readonly ObservableAsPropertyHelper<Slide> _activeSlide;
        public Slide ActiveSlide => _activeSlide.Value;

        public async Task GenerateSlidesAsync()
        {
            var book = await _loader.LoadBookAsync(Translation, Book).ConfigureAwait(false);
            var verses = ScriptureVerseRangeExtractor.Extract(book, StartChapter, StartVerse, EndChapter, EndVerse);
            UpdateVerseSlides(book.Title, verses);
        }

        private void UpdateVerseSlides(string bookTitle, System.Collections.Generic.List<ScriptureVerseRef> verses)
        {
            var newSlides = new ObservableCollection<Slide>();

            foreach (var v in verses)
            {
                var slideId = $"{v.Chapter}:{v.Verse}";
                var label = string.IsNullOrEmpty(bookTitle) ? $"{Book} {v.Chapter}:{v.Verse}" : $"{bookTitle} {v.Chapter}:{v.Verse}";

                var existing = Slides
                    .OfType<ScriptureSlideInstance>()
                    .FirstOrDefault(s => s.Id == slideId);

                if (existing != null)
                {
                    if (existing.Text != v.Text) existing.Text = v.Text;
                    if (existing.Label != label) existing.Label = label;
                    newSlides.Add(existing);
                }
                else
                {
                    newSlides.Add(new ScriptureSlideInstance(this, slideId, text: v.Text, label: label));
                }
            }

            _slides = newSlides;
            this.RaisePropertyChanged(nameof(Slides));
        }
    }
}
