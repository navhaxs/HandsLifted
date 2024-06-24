using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using DebounceThrottle;
using DynamicData.Binding;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Data.Data.Models.Items;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Data.Slides
{
    public class SongSlideInstance : SongSlide, ISlideInstance
    {
        private DebounceDispatcher debounceDispatcher = new(200);

        public SongSlideInstance(SongItemInstance? parentSongItem, SongStanza? parentSongStanza, string id) : base(
            parentSongItem, parentSongStanza, id)
        {
            Theme = Globals.Instance.AppPreferences?.DefaultTheme;

            Globals.Instance.AppPreferences?.DefaultTheme.WhenAnyPropertyChanged().Subscribe(x =>
            {
                Theme = x;
                debounceDispatcher.Debounce(() => GenerateBitmaps());
            });

            debounceDispatcher.Debounce(() => GenerateBitmaps());

            this.WhenAnyValue(x => x.Text)
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe((x) =>
                {
                    debounceDispatcher.Debounce(() => GenerateBitmaps());
                });
        }

        private void GenerateBitmaps()
        {
            MessageBus.Current.SendMessage(new SlideRenderRequestMessage(
                this,
                (obitmap) =>
                {
                    this.Cached = obitmap;
                    Thumbnail = BitmapUtils.CreateThumbnail(obitmap);
                }
            ));
        }

        private BaseSlideTheme? _theme;
        public BaseSlideTheme? Theme
        {
            get => _theme;
            set => this.RaiseAndSetIfChanged(ref _theme, value);
        }

        // refs

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