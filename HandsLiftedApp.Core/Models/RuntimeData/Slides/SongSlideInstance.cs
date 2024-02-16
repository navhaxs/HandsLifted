using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;
using System.Linq;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Data.Slides
{
    public class SongSlideInstance : SongSlide
    {
        public SongSlideInstance(SongItemInstance? parentSongItem, SongStanza? parentSongStanza, string id) : base(
            parentSongItem, parentSongStanza, id)
        {
            this._theme =
                parentSongItem.WhenAnyValue(parentSongItem => parentSongItem.Design,
                        (target =>
                        {
                            if (target != Guid.Empty)
                            {
                                var baseSlideTheme = parentSongItem.ParentPlaylist.Designs.First(d => d.Id == target);
                                return baseSlideTheme;
                            }

                            return new BaseSlideTheme();
                        })
                    )
                    .ToProperty(this, x => x.Theme);
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