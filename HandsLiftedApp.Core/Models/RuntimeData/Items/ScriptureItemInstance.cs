using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Services;
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
            // which isn't guaranteed to run in a unit-test host. Keeping it synchronous makes ActiveSlide
            // update deterministically and immediately when SelectedSlideIndex or Slides changes.
            // (UpdateVerseSlides below, which mutates Slides itself, does explicitly marshal to the UI
            // thread via Dispatcher.UIThread.Post since it's reachable from a background-thread
            // continuation of GenerateSlidesAsync's network fetch — this ActiveSlide chain is not.)
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
            // GenerateSlidesAsync awaits the network fetch with .ConfigureAwait(false), so this
            // continuation runs on a thread-pool thread, not the UI thread. Slides is bound live
            // in ItemSlidesView once a scripture item sits in a playlist, so the mutation below
            // (and the RaisePropertyChanged(nameof(Slides)) it triggers) must be marshaled back
            // to the UI thread rather than running here.
            Dispatcher.UIThread.Post(() =>
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

                // Enqueue newly created slides (and any reused slide that never got a first
                // render) for background thumbnail generation. Cached == null covers both:
                // brand-new slides from this call, and slides that were new on a prior call
                // but never got enqueued (which would otherwise stay permanently blank).
                var toRender = newSlides.OfType<ScriptureSlideInstance>()
                    .Where(s => s.Cached == null)
                    .Cast<IRenderable>()
                    .ToList();
                if (toRender.Count > 0)
                    Globals.Instance.SlideRenderQueue.EnqueueBatch(toRender);
            });
        }
    }
}
