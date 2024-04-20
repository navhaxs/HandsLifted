using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using DebounceThrottle;
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
            this._theme =
                this.WhenAnyValue(x => x.ParentSongItem.Design, x => x.ParentSongStanza.Design,
                        (target2, target1) =>
                        {
                            if (target2 != null && target2 != Guid.Empty)
                            {
                                var baseSlideTheme =
                                    parentSongItem?.ParentPlaylist?.Designs.First(d => d.Id == target2);
                                if (baseSlideTheme != null)
                                {
                                    baseSlideTheme.PropertyChanged += (sender, args) =>
                                    {
                                        debounceDispatcher.Debounce(() => GenerateBitmaps());
                                    };
                                    return baseSlideTheme;
                                }
                            }

                            if (target1 != null && target1 != Guid.Empty)
                            {
                                var baseSlideTheme =
                                    parentSongItem?.ParentPlaylist?.Designs.First(d => d.Id == target1);
                                if (baseSlideTheme != null)
                                {
                                    baseSlideTheme.PropertyChanged += (sender, args) =>
                                    {
                                        debounceDispatcher.Debounce(() => GenerateBitmaps());
                                    };
                                    return baseSlideTheme;
                                }
                            }

                            return new BaseSlideTheme();
                        })
                    .ToProperty(this, x => x.Theme);

            this.WhenAnyValue(s => s.Text, s => s.Theme) // todo dirty bit?
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
                    this.Cached = obitmap;
                    Thumbnail = BitmapUtils.CreateThumbnail(obitmap);
                }
            ));
        }

        private ObservableAsPropertyHelper<BaseSlideTheme?> _theme;

        public BaseSlideTheme Theme
        {
            get => _theme.Value;
        }
        
        private BaseSlideTheme? _selectedSlideTheme;

        public BaseSlideTheme? SelectedSlideTheme
        {
            get => _selectedSlideTheme;
            set => this.RaiseAndSetIfChanged(ref _selectedSlideTheme, value);
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