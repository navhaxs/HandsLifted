using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Skia;
using Avalonia.Skia.Helpers;
using DebounceThrottle;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.Views;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using SkiaSharp;

namespace HandsLiftedApp.Data.Slides
{
    public class SongTitleSlideInstance : SongTitleSlide, ISlideInstance
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
                .Subscribe(text => { debounceDispatcher.Debounce(() => GenerateBitmaps()); });
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

        private BaseSlideTheme? _theme;

        public BaseSlideTheme? Theme
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
    }
}