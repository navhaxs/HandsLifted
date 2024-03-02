using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Media.Imaging;
using DebounceThrottle;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;

namespace HandsLiftedApp.Data.Slides
{
    public class SongTitleSlideInstance : SongTitleSlide
    {
        private DebounceDispatcher debounceDispatcher = new(200);

        public SongTitleSlideInstance(SongItemInstance? parentSongItem) : base()
        {
            parentSongItem.WhenAnyValue(parentSongItem => parentSongItem.Design)
                .Subscribe(target =>
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
                            Theme = baseSlideTheme;
                            return;
                        }
                    }

                    Theme = new BaseSlideTheme();
                });

            this.WhenAnyValue(s => s.Title, s => s.Copyright, s => s.Theme) // todo dirty bit?
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

        private BaseSlideTheme? _theme;

        public BaseSlideTheme? Theme
        {
            get => _theme;
            set => this.RaiseAndSetIfChanged(ref _theme, value);
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