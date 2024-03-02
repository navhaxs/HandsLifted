using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using DebounceThrottle;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Data.Slides
{
    public class SongSlideInstance : SongSlide
    {
        private DebounceDispatcher debounceDispatcher = new(200);

        public SongSlideInstance(SongItemInstance? parentSongItem, SongStanza? parentSongStanza, string id) : base(
            parentSongItem, parentSongStanza, id)
        {
            this._theme =
                parentSongItem.WhenAnyValue(parentSongItem => parentSongItem.Design,
                        (target =>
                        {
                            if (target != Guid.Empty)
                            {
                                var baseSlideTheme = parentSongItem?.ParentPlaylist?.Designs.First(d => d.Id == target);
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
                    )
                    .ToProperty(this, x => x.Theme);
            
            
            this.WhenAnyValue(s => s.Text, s => s.Theme) // todo dirty bit?
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(text =>
                {
                    debounceDispatcher.Debounce(() => GenerateBitmaps());
                });
        }
        
        private void GenerateBitmaps()
        {
            MessageBus.Current.SendMessage(new SlideRenderRequestMessage(
                this,
                (bitmap) =>
                {
                    this.cached = bitmap;
                    // https://github.com/AvaloniaUI/Avalonia/issues/8444
                    // TODO
                }
            )); 
        }
        
        private ObservableAsPropertyHelper<BaseSlideTheme?> _theme;

        public BaseSlideTheme Theme
        {
            get => _theme.Value;
        }

        // refs
        public SongItemInstance? ParentSongItem { get; } = null;

        Bitmap _cached;

        public Bitmap? cached
        {
            get => _cached;
            set => this.RaiseAndSetIfChanged(ref _cached, value);
        }
    }
}