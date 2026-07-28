using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using DebounceThrottle;
using DynamicData.Binding;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Importer.Scripture;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public class ScriptureItemInstance : ScriptureItem, IItemInstance, IItemDirtyBit
    {
        public PlaylistInstance? ParentPlaylist { get; set; }

        public event EventHandler? ItemDataModified;

        private readonly ScriptureLocalUsxStore? _injectedStore;

        private readonly DebounceDispatcher _themeChangeDebounce = new(200);

        public ScriptureItemInstance(PlaylistInstance? parentPlaylist, ScriptureLocalUsxStore? store = null) : base()
        {
            ParentPlaylist = parentPlaylist;
            _injectedStore = store;

            // Deliberately no .ObserveOn(RxApp.MainThreadScheduler) here (unlike SongItemInstance's
            // equivalent chain): that scheduler depends on Avalonia.ReactiveUI's dispatcher registration,
            // which isn't guaranteed to run in a unit-test host. Keeping it synchronous makes ActiveSlide
            // update deterministically and immediately when SelectedSlideIndex or Slides changes.
            // (UpdateVerseSlides below, which mutates Slides itself, does explicitly marshal to the UI
            // thread via Dispatcher.UIThread.Post since it's reachable from a background-thread
            // continuation of GenerateSlidesAsync's disk read — this ActiveSlide chain is not.)
            _activeSlide = this.WhenAnyValue(x => x.SelectedSlideIndex, x => x.Slides,
                    (selectedSlideIndex, slides) => slides.ElementAtOrDefault(selectedSlideIndex))
                .ToProperty(this, x => x.ActiveSlide);

            this.WhenAnyValue(x => x.Design)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(ResolvedDesignTheme)));

            // Repaginate (debounced) whenever any property of the currently-resolved theme
            // changes — not just when Design switches to a different theme object, but also
            // when an already-selected theme is edited in place (e.g. a live font-size slider
            // in the theme editor, which mutates the same BaseSlideTheme object every item
            // pointing at that Design already shares). Pagination is font-size/line-height
            // dependent, so a plain re-render (what ScriptureSlideInstance's own Theme-change
            // subscription does) isn't enough here — the slide count itself may need to change.
            this.WhenAnyValue(x => x.ResolvedDesignTheme)
                .Select(t => t?.WhenAnyPropertyChanged() ?? Observable.Never<BaseSlideTheme?>())
                .Switch()
                .Subscribe(_2 => _themeChangeDebounce.Debounce(() =>
                    _ = GenerateSlidesAsync(forceInvalidateCache: true).ContinueWith(
                        t => Log.Error(t.Exception, "Failed to generate scripture slides for {Title}", Title),
                        TaskContinuationOptions.OnlyOnFaulted)));

            // ReactiveUI's no-selector WhenAnyValue only goes up to 7 properties; an 8th property
            // (Design) requires the selector-taking overload instead, so this passes a no-op
            // selector purely to combine all 8 change streams into one Subscribe.
            this.WhenAnyValue(
                i => i.Title,
                i => i.Translation,
                i => i.Book,
                i => i.StartChapter,
                i => i.StartVerse,
                i => i.EndChapter,
                i => i.EndVerse,
                i => i.Design,
                (_1, _2, _3, _4, _5, _6, _7, _8) => Unit.Default
            ).Subscribe(_ =>
            {
                ItemDataModified?.Invoke(this, EventArgs.Empty);
            });
        }

        public BaseSlideTheme? ResolvedDesignTheme
        {
            get => ParentPlaylist?.Designs.FirstOrDefault(d => d.Id == Design)
                   ?? Globals.Instance.AppPreferences?.DefaultTheme;
            set
            {
                Design = value?.Id ?? Guid.Empty;
                _ = GenerateSlidesAsync().ContinueWith(
                    t => Log.Error(t.Exception, "Failed to generate scripture slides for {Title}", Title),
                    TaskContinuationOptions.OnlyOnFaulted);
            }
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

        public async Task GenerateSlidesAsync(bool forceInvalidateCache = false)
        {
            var store = _injectedStore ?? new ScriptureLocalUsxStore(Globals.Instance.AppPreferences.ScriptureDataPath);
            List<ScriptureVerseRef> verses;
            string bookTitle;
            try
            {
                var book = await store.LoadBookAsync(Book).ConfigureAwait(false);
                verses = ScriptureVerseRangeExtractor.Extract(book, StartChapter, StartVerse, EndChapter, EndVerse);
                bookTitle = book.Title;
            }
            catch (ScriptureBookNotFoundException ex)
            {
                Log.Error(ex, "Scripture data not found for {Book} ({Translation})", Book, Translation);
                verses = MakeMissingDataPlaceholder();
                bookTitle = Book;
            }

            var referenceLabel = FormatReferenceLabel(bookTitle);
            UpdatePages(referenceLabel, verses, forceInvalidateCache);
        }

        private string FormatReferenceLabel(string bookTitle)
        {
            var title = string.IsNullOrEmpty(bookTitle) ? Book : bookTitle;
            return StartChapter == EndChapter && StartVerse == EndVerse
                ? $"{title} {StartChapter}:{StartVerse}"
                : $"{title} {StartChapter}:{StartVerse}-{EndChapter}:{EndVerse}";
        }

        private List<ScriptureVerseRef> MakeMissingDataPlaceholder()
        {
            var text =
                $"Scripture data not found: {Book} {StartChapter}:{StartVerse}-{EndChapter}:{EndVerse} ({Translation})\n" +
                "Check Setup > Library > Scripture Data Path";
            return new List<ScriptureVerseRef> { new ScriptureVerseRef(StartChapter, StartVerse, text) };
        }

        private void UpdatePages(string referenceLabel, List<ScriptureVerseRef> verses, bool forceInvalidateCache = false)
        {
            var theme = ResolvedDesignTheme ?? new BaseSlideTheme();
            var pages = ScriptureParagraphLayoutEngine.Paginate(verses, referenceLabel, theme);

            // GenerateSlidesAsync awaits the local disk read with .ConfigureAwait(false), so this
            // continuation still runs on a thread-pool thread (File I/O, not UI-thread work), not
            // the UI thread. Slides is bound live in ItemSlidesView once a scripture item sits in a
            // playlist, so the mutation below (and the RaisePropertyChanged(nameof(Slides)) it
            // triggers) must be marshaled back to the UI thread rather than running here.
            Dispatcher.UIThread.Post(() =>
            {
                var newSlides = new ObservableCollection<Slide>();

                for (int i = 0; i < pages.Count; i++)
                {
                    var page = pages[i];
                    var slideId = $"page{i}";
                    // Each ScriptureParagraphLine's Runs already reconstruct that line's text exactly
                    // when concatenated with no separator (inter-word spacing is itself a standalone
                    // run — see ScriptureParagraphLayoutEngine.PrependSpace — and a run that starts a
                    // wrapped line never carries a leading space). The single line break introduced by
                    // wrapping stands in for the original space between words, so lines are rejoined
                    // with a single space to restore that spacing (also separating the header line(s)
                    // from the verse text by exactly one space).
                    var flatText = string.Join(" ", page.Lines.Select(l => string.Concat(l.Runs.Select(r => r.Text)))).Trim();

                    var existing = Slides
                        .OfType<ScriptureSlideInstance>()
                        .FirstOrDefault(s => s.Id == slideId);

                    if (existing != null)
                    {
                        existing.Lines = page.Lines;
                        if (existing.Text != flatText) existing.Text = flatText;
                        if (existing.Label != referenceLabel) existing.Label = referenceLabel;
                        bool themeChanged = !ReferenceEquals(existing.Theme, theme);
                        if (themeChanged) existing.Theme = theme;
                        if (themeChanged || forceInvalidateCache) existing.Cached = null;
                        newSlides.Add(existing);
                    }
                    else
                    {
                        var slide = new ScriptureSlideInstance(this, slideId, text: flatText, label: referenceLabel)
                        {
                            Lines = page.Lines,
                            Theme = theme
                        };
                        newSlides.Add(slide);
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
