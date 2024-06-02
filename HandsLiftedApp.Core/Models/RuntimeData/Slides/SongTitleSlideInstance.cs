using System;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using DebounceThrottle;
using DynamicData.Binding;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Data.Slides
{
    public class SongTitleSlideInstance : SongTitleSlide, ISlideInstance
    {
        private DebounceDispatcher debounceDispatcher = new(200);

        public SongTitleSlideInstance(SongItemInstance? parentSongItem) : base()
        {
            Log.Verbose("Creating slide instance");
            Theme = Globals.AppPreferences.DefaultTheme;

            Globals.AppPreferences.DefaultTheme.WhenAnyPropertyChanged().Subscribe(x =>
            {
                Theme = x;
                debounceDispatcher.Debounce(() => GenerateBitmaps());
            });

            this.WhenAnyValue(s => s.Title, s => s.Copyright) // todo dirty bit?
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(text => { debounceDispatcher.Debounce(() => GenerateBitmaps()); });

            debounceDispatcher.Debounce(() => GenerateBitmaps());
        }

        private void GenerateBitmaps()
        {
            MessageBus.Current.SendMessage(new SlideRenderRequestMessage(
                this,
                (obitmap) =>
                {
                    Cached = obitmap;
                    Thumbnail = BitmapUtils.CreateThumbnail(obitmap);
                }
            ));
        }

        private BaseSlideTheme _theme;

        public BaseSlideTheme Theme
        {
            get => _theme;
            set => this.RaiseAndSetIfChanged(ref _theme, value);
        }

        // refs
        public SongItemInstance? ParentSongItem { get; } = null;

        Bitmap _cached;

        public Bitmap? Cached
        {
            get => _cached;
            set => this.RaiseAndSetIfChanged(ref _cached, value);
        }

        Bitmap _thumbnail;

        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            set => this.RaiseAndSetIfChanged(ref _thumbnail, value);
        }

        public ItemAutoAdvanceTimer? SlideTimerConfig => null;
    }
}