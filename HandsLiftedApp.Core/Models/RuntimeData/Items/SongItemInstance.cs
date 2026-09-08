using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Avalonia.Media.Imaging;
using DebounceThrottle;
using HandsLiftedApp.Core.Services;
using HandsLiftedApp.Data;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Core.Utils;
using LibMpv.Thumbnailing;
using ReactiveUI;
using Serilog;
using ShellThumbs;
using SkiaSharp;

namespace HandsLiftedApp.Core.Models.RuntimeData.Items
{
    public class SongItemInstance : SongItem, IItemInstance, IItemDirtyBit, IDisposable
    {
        public PlaylistInstance? ParentPlaylist { get; set; }

        public bool HasThemeSelection => true;

        /// <summary>
        /// Which library song this instance resolves against — independent of UUID (this
        /// playlist item's own identity, used by slide navigation/drag-reorder elsewhere in
        /// this codebase). Two playlist items can share a SongId (both reference the same song)
        /// while each keeping their own distinct UUID.
        /// </summary>
        public Guid SongId { get; set; }

        public BaseSlideTheme? ResolvedDesignTheme
        {
            get => ParentPlaylist?.Designs.FirstOrDefault(d => d.Id == Design);
            set => Design = value?.Id ?? Guid.Empty;
        }

        // A brand-new song authored in the editor that has no library file yet. Non-null only
        // between NewDraft() and AttachToLibrary(); once attached, resolution goes through
        // Globals.Instance.SongLibraryIndex like every other SongItemInstance.
        private SongItem? _localDraft;

        // Subscription to the shared library's SongChanged notifications, taken out in the
        // constructor. Must be disposed when this instance is torn down (e.g. removed from a
        // playlist) — otherwise it permanently roots this instance against the app-lifetime
        // SongLibraryIndex and keeps re-running its re-render logic for a song this instance
        // no longer represents anywhere in the UI.
        private readonly IDisposable _songChangedSubscription;

        // The Arrangement/Stanzas collections this instance's CollectionChanged /
        // CollectionItemChanged handlers are currently attached to. Under the library facade
        // these are the *shared* library song's collections, held for the app's lifetime by
        // SongLibraryIndex — so Dispose() must detach from them, or a disposed instance stays
        // rooted and keeps re-running its slide-regeneration logic on every future edit.
        private ObservableCollection<Guid>? _lastSubscribedArrangement;
        private TrulyObservableCollection<SongStanza>? _lastSubscribedStanzas;

        public SongItem? ResolvedSong => Globals.Instance.SongLibraryIndex.Resolve(SongId) ?? _localDraft;

        public bool IsMissing => ResolvedSong == null;

        public static SongItemInstance NewDraft(PlaylistInstance? parentPlaylist)
        {
            var draftSong = new SongItem();
            // SongId points at the draft song; UUID keeps the fresh playlist-item identity the
            // base Item() constructor already assigned — do not overwrite it.
            var instance = new SongItemInstance(parentPlaylist) { SongId = draftSong.UUID };
            instance._localDraft = draftSong;
            // The constructor's WhenAnyValue(Stanzas)/WhenAnyValue(Arrangement) subscriptions ran
            // before _localDraft was assigned above, so they wired up against the throwaway empty
            // collections the facade getters return when ResolvedSong is still null at construction
            // time. Force a re-derivation now that _localDraft (and therefore ResolvedSong) is set,
            // so those subscriptions (and the title slide) attach to the real draft's collections.
            instance.RaiseForwardedPropertiesChanged();
            return instance;
        }

        /// <summary>
        /// Called once a draft's library file has been written (SongEditorWindow's
        /// save/add-to-playlist flow). Registers the draft's content into the shared index
        /// under this instance's SongId and clears local-draft state — from this point on,
        /// resolution goes through SongLibraryIndex like any other reference.
        /// </summary>
        public void AttachToLibrary(string filePath, string libraryDirectory)
        {
            if (_localDraft == null)
                return; // already attached, or was never a draft — no-op

            // Ensure the draft registers under whatever SongId this instance currently carries —
            // something may have reassigned this instance's SongId after NewDraft() ran.
            _localDraft.UUID = SongId;
            Globals.Instance.SongLibraryIndex.Register(_localDraft, filePath, libraryDirectory);
            _localDraft = null;
            this.RaisePropertyChanged(nameof(IsMissing));
        }

        public override string Title
        {
            get => ResolvedSong?.Title ?? "(Missing Song)";
            set { if (ResolvedSong is { } s) { s.Title = value; NotifySharedSongChanged(); } }
        }

        public override Guid Design
        {
            get => ResolvedSong?.Design ?? Guid.Empty;
            set { if (ResolvedSong is { } s) { s.Design = value; NotifySharedSongChanged(); } }
        }

        public override string Copyright
        {
            get => ResolvedSong?.Copyright ?? "";
            set { if (ResolvedSong is { } s) { s.Copyright = value; NotifySharedSongChanged(); } }
        }

        public override TrulyObservableCollection<SongStanza> Stanzas
        {
            get => ResolvedSong?.Stanzas ?? new TrulyObservableCollection<SongStanza>();
            set { if (ResolvedSong is { } s) { s.Stanzas = value; NotifySharedSongChanged(); } }
        }

        public override SerializableDictionary<string, List<Guid>> Arrangements
        {
            get => ResolvedSong?.Arrangements ?? new SerializableDictionary<string, List<Guid>>();
            set { if (ResolvedSong is { } s) { s.Arrangements = value; NotifySharedSongChanged(); } }
        }

        public override string? SelectedArrangementId
        {
            get => ResolvedSong?.SelectedArrangementId;
            set { if (ResolvedSong is { } s) { s.SelectedArrangementId = value; NotifySharedSongChanged(); } }
        }

        public override string? MotionBackgroundVideoPath
        {
            get => ResolvedSong?.MotionBackgroundVideoPath;
            set { if (ResolvedSong is { } s) { s.MotionBackgroundVideoPath = value; NotifySharedSongChanged(); } }
        }

        public override ObservableCollection<Guid> Arrangement
        {
            get => ResolvedSong?.Arrangement ?? new ObservableCollection<Guid>();
            set { if (ResolvedSong is { } s) { s.Arrangement = value; NotifySharedSongChanged(); } }
        }

        public override Boolean EndOnBlankSlide
        {
            get => ResolvedSong?.EndOnBlankSlide ?? true;
            set { if (ResolvedSong is { } s) { s.EndOnBlankSlide = value; NotifySharedSongChanged(); } }
        }

        public override Boolean StartOnTitleSlide
        {
            get => ResolvedSong?.StartOnTitleSlide ?? true;
            set { if (ResolvedSong is { } s) { s.StartOnTitleSlide = value; NotifySharedSongChanged(); } }
        }

        private void NotifySharedSongChanged()
        {
            if (Globals.Instance.SongLibraryIndex.Resolve(SongId) == null)
            {
                // Not yet attached to the library — nothing else can be watching this SongId yet,
                // just re-render this instance's own slides.
                RaiseForwardedPropertiesChanged();
                debounceDispatcher.Debounce(() => UpdateStanzaSlides());
                return;
            }

            Globals.Instance.SongLibraryIndex.NotifyChanged(SongId);
        }

        public void RaiseForwardedPropertiesChanged()
        {
            this.RaisePropertyChanged(nameof(Title));
            this.RaisePropertyChanged(nameof(Design));
            this.RaisePropertyChanged(nameof(ResolvedDesignTheme));
            this.RaisePropertyChanged(nameof(Copyright));
            this.RaisePropertyChanged(nameof(Stanzas));
            this.RaisePropertyChanged(nameof(Arrangements));
            this.RaisePropertyChanged(nameof(SelectedArrangementId));
            this.RaisePropertyChanged(nameof(MotionBackgroundVideoPath));
            this.RaisePropertyChanged(nameof(Arrangement));
            this.RaisePropertyChanged(nameof(EndOnBlankSlide));
            this.RaisePropertyChanged(nameof(StartOnTitleSlide));
            this.RaisePropertyChanged(nameof(IsMissing));
        }

        private SongTitleSlide titleSlide;
        private DebounceDispatcher debounceDispatcher = new(200);

        public event EventHandler ItemDataModified;

        public void GenerateArrangementViews()
        {
            var result = new ObservableCollection<ArrangementRef>();
            var i = 0;
            foreach (var stanzaId in Arrangement)
            {
                var stanza = Stanzas.FirstOrDefault(stanza => stanza.Id == stanzaId);
                if (stanza != null)
                {
                    result.Add(new ArrangementRef()
                        { Index = i, SongStanza = stanza });
                    i++;
                }
            }

            ArrangementAsRefList = result;
        }

        public SongItemInstance(PlaylistInstance? parentPlaylist) : base()
        {
            ParentPlaylist = parentPlaylist;

            _songChangedSubscription = Globals.Instance.SongLibraryIndex.SongChanged
                .Where(changedId => changedId == SongId)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ =>
                {
                    RaiseForwardedPropertiesChanged();
                    // Content changed on the shared song — force the Cached == null re-render sweep
                    // (existing gotcha: reassigning a watched property alone doesn't guarantee a
                    // re-render if the subscription chain doesn't happen to fire for it).
                    foreach (var slide in Slides.OfType<SongSlideInstance>())
                        slide.Cached = null;
                    if (TitleSlide is SongTitleSlideInstance titleInst)
                        titleInst.Cached = null;
                    debounceDispatcher.Debounce(() => UpdateStanzaSlides());
                });

            this.WhenAnyValue(x => x.Design)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(ResolvedDesignTheme)));

            titleSlide = new SongTitleSlideInstance(this);

            _titleSlide = this.WhenAnyValue(x => x.Title, x => x.Copyright,
                        (title, copyright) =>
                        {
                            // TODO do not keep re-creating the slide object, rather just update it
                            titleSlide.Title = title;
                            titleSlide.Copyright = copyright;
                            return titleSlide;
                        })
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Throttle(TimeSpan.FromMilliseconds(100), RxSchedulers.TaskpoolScheduler)
                    .ToProperty(this, c => c.TitleSlide)
                ;

            // TODO: reorder...
            this.WhenAnyValue(x => x.Arrangement)
                .Subscribe(a =>
                {
                    a.CollectionChanged -= OnArrangementCollectionChanged;
                    GenerateArrangementViews();
                    a.CollectionChanged += OnArrangementCollectionChanged;
                    _lastSubscribedArrangement = a;
                });
            // TODO: reorder...
            this.WhenAnyValue(x => x.Stanzas)
                .Subscribe(a =>
                {
                    a.CollectionItemChanged -= _stanzas_CollectionItemChanged;
                    a.CollectionChanged -= OnArrangementCollectionChanged;
                    GenerateArrangementViews();
                    a.CollectionChanged += OnArrangementCollectionChanged;
                    a.CollectionItemChanged += _stanzas_CollectionItemChanged;
                    _lastSubscribedStanzas = a;
                });
            GenerateArrangementViews();

            this.WhenAnyValue(x => x.TitleSlide).Subscribe((d) =>
            {
                debounceDispatcher.Debounce(() => UpdateStanzaSlides());
            });

            this.WhenAnyValue(x => x.EndOnBlankSlide).Subscribe((d) =>
            {
                debounceDispatcher.Debounce(() => UpdateStanzaSlides());
            });

            this.WhenAnyValue(x => x.StartOnTitleSlide).Subscribe((d) =>
            {
                debounceDispatcher.Debounce(() => UpdateStanzaSlides());
            });

            this.WhenAnyValue(x => x.MotionBackgroundVideoPath)
                .Scan(
                    new { Previous = (string?)null, Current = (string?)null },
                    (acc, newValue) => new { Previous = acc.Current, Current = newValue })
                .Subscribe(pair =>
                {
                    this.RaisePropertyChanged(nameof(HasMotionBackground));

                    var wasValid = MotionBackgroundService.IsValidVideoFile(pair.Previous);
                    var isValid = MotionBackgroundService.IsValidVideoFile(pair.Current);

                    // When path changes from null/empty to a valid path, regenerate all slide bitmaps
                    if (!wasValid && isValid)
                    {
                        RegenerateAllSlideBitmaps();
                    }
                    // When path changes from valid to null/empty, defer regeneration
                    // until next slide transition (do not regenerate immediately)
                });

            _activeSlide = this.WhenAnyValue(x => x.SelectedSlideIndex, x => x.Slides,
                    (selectedSlideIndex, slides) => { return slides.ElementAtOrDefault(selectedSlideIndex); })
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .ToProperty(this, x => x.ActiveSlide);

            this.WhenAnyValue(
                i => i.Title,
                i => i.Arrangement,
                i => i.Arrangements,
                // i => i.SelectedArrangementId,
                i => i.Stanzas,
                i => i.Copyright,
                // i => i.Design,
                i => i.EndOnBlankSlide,
                i => i.StartOnTitleSlide
            ).Subscribe(_ =>
            {
                ItemDataModified?.Invoke(this, EventArgs.Empty);
            });
        }

        public void Dispose()
        {
            _songChangedSubscription.Dispose();
            if (_lastSubscribedArrangement != null)
                _lastSubscribedArrangement.CollectionChanged -= OnArrangementCollectionChanged;
            if (_lastSubscribedStanzas != null)
            {
                _lastSubscribedStanzas.CollectionItemChanged -= _stanzas_CollectionItemChanged;
                _lastSubscribedStanzas.CollectionChanged -= OnArrangementCollectionChanged;
            }
        }

        private void OnArrangementCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ItemDataModified?.Invoke(this, EventArgs.Empty);
            GenerateArrangementViews();
            debounceDispatcher.Debounce(() => UpdateStanzaSlides());
        }

        public void ResetArrangement()
        {
            var a = new ObservableCollection<Guid>();
            foreach (var stanza in Stanzas)
            {
                a.Add(stanza.Id);
            }

            Arrangement = a;
            
            debounceDispatcher.Debounce(() => UpdateStanzaSlides());
        }

        private readonly object stantaSlidesLock = new object();

        public void GenerateSlides()
        {
            UpdateStanzaSlides();
        }

        void UpdateStanzaSlides()
        {
            System.Collections.Generic.List<IRenderable>? toRender = null;
            lock (stantaSlidesLock)
            {
                try
                {
                    if (IsMissing)
                    {
                        var missingSlides = new TrulyObservableCollection<Slide>
                        {
                            new SongSlideInstance(this, new SongStanza(), "MISSING", text: "(Missing Song)", label: null)
                        };
                        StanzaSlides = missingSlides;
                        this.RaisePropertyChanged("Slides");
                        Globals.Instance.SlideRenderQueue.EnqueueBatch(missingSlides.OfType<IRenderable>().ToList());
                        return;
                    }

                    var newSlides = new TrulyObservableCollection<Slide>();
                    foreach (var existingSlide in Slides)
                    {
                        newSlides.Add(existingSlide);
                    }
                    
                    int i = 0;

                    // add title slide
                    if (StartOnTitleSlide && TitleSlide != null)
                    {
                        //TitleSlide.Index = i;

                        if (newSlides.ElementAtOrDefault(0) is SongTitleSlide)
                        {
                            ((SongTitleSlide)newSlides.ElementAt(0)).Title = Title;
                            ((SongTitleSlide)newSlides.ElementAt(0)).Copyright = Copyright;
                        }
                        else
                        {
                            newSlides.Insert(i, TitleSlide);
                        }

                        i++;
                    }

                    Dictionary<Guid, int> stanzaSeenCount = new Dictionary<Guid, int>();

                    foreach (var a in Arrangement)
                    {
                        // todo match Stanzas by first match, so content is up to date. dont trust the cached copy in Arrangement
                        SongStanza _datum = Stanzas.First(stanza => stanza.Id == a);
                        if (_datum == null)
                        {
                            continue;
                        }

                        stanzaSeenCount[_datum.Id] =
                            stanzaSeenCount.ContainsKey(_datum.Id) ? stanzaSeenCount[_datum.Id] + 1 : 0;

                        // break slides by newlines
                        string[] lines = _datum.Lyrics.Replace("\r\n", "\n").Split(new string[] { "\n\n" },
                            StringSplitOptions.RemoveEmptyEntries);

                        foreach (var x in lines.Select((line, index) => new { line, index }))
                        {
                            var Text = x.line;
                            var Label = (x.index == 0) ? $"{_datum.Name}" : null;

                            var slideId = $"{_datum.Id}:{stanzaSeenCount[_datum.Id]}:{x.index}";

                            var prevIndex = newSlides.Select((data, index) => new { data, index })
                                .FirstOrDefault(s => (s.data) is SongSlide && ((SongSlide)s.data).Id == slideId);

                            if (prevIndex != null)
                            {
                                // update the existing slide object of the same id
                                if (((SongSlide)prevIndex.data).Text != Text)
                                {
                                    ((SongSlide)prevIndex.data).Text = Text;
                                }

                                if (((SongSlide)prevIndex.data).Label != Label)
                                {
                                    ((SongSlide)prevIndex.data).Label = Label;
                                }

                                //prevIndex.data.Index = i;

                                if (prevIndex.index != i)
                                {
                                    //re-order to index i
                                    newSlides.Move(prevIndex.index, i);
                                }
                            }
                            else
                            {
                                var slide = new SongSlideInstance(this, _datum, slideId, text: Text, label: Label);
                                newSlides.Insert(i, slide);
                            }

                            i++;
                        }
                    }

                    if (EndOnBlankSlide == true)
                    {
                        var prevIndex = newSlides.Select((data, index) => new { data, index })
                            .FirstOrDefault(s => (s.data) is (SongSlide) && ((SongSlide)s.data).Id == "BLANK");
                        if (prevIndex != null && prevIndex.index == i)
                        {
                        }
                        else if (prevIndex != null && prevIndex.index != i)
                        {
                            //prevIndex.data.Index = i;
                            newSlides.Move(prevIndex.index, i);
                        }
                        else
                        {
                            newSlides.Insert(i,
                                new SongSlideInstance(this, new SongStanza(), "BLANK")); // { Index = i });
                        }

                        i++;
                    }

                    // need to delete old items
                    if (i < newSlides.Count)
                    {
                        var howManyToDelete = newSlides.Count - i;
                        while (howManyToDelete > 0)
                        {
                            newSlides.RemoveAt(newSlides.Count - 1); // remove last
                            howManyToDelete--;
                        }
                    }
                    
                    // StanzaSlides.RemoveAt(0);
                    Log.Verbose("Generated Stanza Slides. Count={Count}", newSlides.Count);
                    StanzaSlides = newSlides;
                    // this.RaisePropertyChanged("StanzaSlides");
                    this.RaisePropertyChanged("Slides");
                    // Enqueue newly created slides for background thumbnail generation.
                    // Cached == null identifies slides created this call (existing slides keep their cached bitmap).
                    toRender = new System.Collections.Generic.List<IRenderable>();
                    foreach (var slide in newSlides)
                    {
                        if (slide is SongSlideInstance s && s.Cached == null)
                            toRender.Add(s);
                    }
                    if (TitleSlide is SongTitleSlideInstance titleInst && titleInst.Cached == null)
                        toRender.Add(titleInst);
                }
                catch (Exception ex)
                {
                    Log.Error("SongItemInstance.GenerateSlides", ex);
                }


            }
            if (toRender?.Count > 0)
                Globals.Instance.SlideRenderQueue.EnqueueBatch(toRender);

        }

        private void _stanzas_CollectionItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            s();
            ItemDataModified?.Invoke(this, EventArgs.Empty);
        }

        private void _stanzas_CollectionChanged(object? sender,
            NotifyCollectionChangedEventArgs e)
        {
            s();
            ItemDataModified?.Invoke(this, EventArgs.Empty);
        }

        void s()
        {
            debounceDispatcher.Debounce(() => UpdateStanzaSlides());
        }

        [XmlIgnore] private ObservableAsPropertyHelper<Slide> _titleSlide;

        [XmlIgnore]
        public Slide TitleSlide
        {
            get => _titleSlide.Value;
        }

        // Stanzas + Arrangement = _stanzaSlides
        [XmlIgnore] private TrulyObservableCollection<Slide> _stanzaSlides = new TrulyObservableCollection<Slide>();

        [XmlIgnore]
        public TrulyObservableCollection<Slide> StanzaSlides
        {
            get => _stanzaSlides;
            set => this.RaiseAndSetIfChanged(ref _stanzaSlides, value);
        }

        //private ObservableAsPropertyHelper<ObservableCollection<Slide>> _slides;

        public ObservableCollection<Slide> Slides
        {
            get => _stanzaSlides;
        }

        public void ReplaceWith(SongItemInstance itemInstance)
        {
            this.Title = itemInstance.Title;
            this.Stanzas = itemInstance.Stanzas;
            this.Copyright = itemInstance.Copyright;
            this.ResetArrangement();
        }

        private int _selectedSlideIndex = -1;

        public int SelectedSlideIndex
        {
            get => _selectedSlideIndex;
            set => this.RaiseAndSetIfChanged(ref _selectedSlideIndex, value);
        }

        private ObservableAsPropertyHelper<Slide> _activeSlide;

        public Slide ActiveSlide
        {
            get => _activeSlide.Value;
        }

        // private ObservableAsPropertyHelper<ObservableCollection<ArrangementRef>> _arrangementAsRefList;
        //
        // public ObservableCollection<ArrangementRef> ArrangementAsRefList
        // {
        //     get => _arrangementAsRefList.Value;
        // }

        private ObservableCollection<ArrangementRef> _arrangementAsRefList;

        public ObservableCollection<ArrangementRef> ArrangementAsRefList
        {
            get => _arrangementAsRefList;
            set => this.RaiseAndSetIfChanged(ref _arrangementAsRefList, value);
        }

        [XmlIgnore]
        public bool HasMotionBackground =>
            !string.IsNullOrWhiteSpace(MotionBackgroundVideoPath)
            && MotionBackgroundService.IsValidVideoFile(MotionBackgroundVideoPath);

        private void RegenerateAllSlideBitmaps()
        {
            foreach (var slide in Slides.OfType<SongSlideInstance>())
            {
                var spec = HandsLiftedApp.Core.Render.Skia.Builders.SongSlideSpecBuilder.Build(slide);
                using var skBitmap = HandsLiftedApp.Core.Render.Skia.SlideRenderer.RenderToSKBitmap(spec);
                slide.Cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
                slide.Thumbnail = BitmapUtils.CreateThumbnail(slide.Cached);
            }

            // Also regenerate title slide bitmap
            if (titleSlide is SongTitleSlideInstance titleInstance)
            {
                SKBitmap? videoFrame = null;
                if (HasMotionBackground)
                {
                    if (ThumbnailEngineSettings.UseMpvEngine)
                    {
                        try
                        {
                            using var avaBmp = MpvThumbnailExtractor.ExtractAsync(MotionBackgroundVideoPath, maxWidth: 1920, maxHeight: 1080)
                                .GetAwaiter().GetResult();
                            if (avaBmp != null)
                                videoFrame = BitmapUtils.AvaloniaToSKBitmap(avaBmp);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "[SongItemInstance] Failed to extract video thumbnail from {Path}", MotionBackgroundVideoPath);
                        }
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        try
                        {
                            using var avaBmp = WindowsThumbnailProvider.GetThumbnail(
                                MotionBackgroundVideoPath, 1920, 1080, ThumbnailOptions.None);
                            if (avaBmp != null)
                                videoFrame = BitmapUtils.AvaloniaToSKBitmap(avaBmp);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "[SongItemInstance] Failed to extract video thumbnail from {Path}", MotionBackgroundVideoPath);
                        }
                    }
                }

                var spec = HandsLiftedApp.Core.Render.Skia.Builders.SongTitleSlideSpecBuilder.Build(titleInstance, videoFrame);
                using var skBitmap = HandsLiftedApp.Core.Render.Skia.SlideRenderer.RenderToSKBitmap(spec);
                videoFrame?.Dispose();
                titleInstance.Cached = BitmapUtils.SKBitmapToAvalonia(skBitmap);
                titleInstance.Thumbnail = BitmapUtils.CreateThumbnail(titleInstance.Cached);
            }
        }
    }

    public class ArrangementRef
    {
        public int Index { get; init; }
        public SongStanza SongStanza { get; init; }

        private sealed class IndexEqualityComparer : IEqualityComparer<ArrangementRef>
        {
            public bool Equals(ArrangementRef x, ArrangementRef y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (ReferenceEquals(x, null))
                {
                    return false;
                }

                if (ReferenceEquals(y, null))
                {
                    return false;
                }

                if (x.GetType() != y.GetType())
                {
                    return false;
                }

                return x.SongStanza.Id == y.SongStanza.Id;
            }

            public int GetHashCode(ArrangementRef obj)
            {
                return obj.Index;
            }
        }

        public static IEqualityComparer<ArrangementRef> IndexComparer { get; } = new IndexEqualityComparer();
    }
}