﻿using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Core.Models.RuntimeData
{
    public interface IItemInstance
    {
        public PlaylistInstance? ParentPlaylist { get; set; }
        public int SelectedSlideIndex { get; set; }
        public Slide ActiveSlide { get; }
        public ObservableCollection<Slide> Slides { get; }
    }

    public static class IItemInstanceExtension
    {
        public static IItemInstance? GetAsIItemInstance(this Item t)
        {
            if (t is IItemInstance itemInstance)
            {
                return itemInstance;
            }

            return null;
        }
    }
}