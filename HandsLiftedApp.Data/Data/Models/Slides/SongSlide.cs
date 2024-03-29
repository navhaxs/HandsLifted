﻿using Avalonia.Media.Imaging;
using HandsLiftedApp.Data.Models.Items;
using ReactiveUI;
using System;

namespace HandsLiftedApp.Data.Slides
{
    public class SongSlide : Slide
    {
        public string Id { get; set; }

        public SongSlide(SongItem? parentSongItem, SongStanza? parentSongStanza, string id)
        {
            ParentSongItem = parentSongItem;
            ParentSongStanza = parentSongStanza;
            Id = id;
        }

        private string _text = "";
        public string Text
        {
            get => _text;
            set
            {
                this.RaiseAndSetIfChanged(ref _text, value);
                //cached = null;
                //this.RaisePropertyChanged(nameof(SlideText));
            }
        }

        private string _label = "";
        public string Label
        {
            get => _label;
            set
            {
                this.RaiseAndSetIfChanged(ref _label, value);
                //this.RaisePropertyChanged(nameof(SlideLabel));
            }
        }

        public override string? SlideText => Text;

        public override string? SlideLabel => Label;

        // refs
        public SongItem? ParentSongItem { get; } = null;
        public SongStanza? ParentSongStanza { get; } = null;

        public override bool Equals(Object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                SongSlide p = (SongSlide)obj;
                //return (Text == p.Text) && (Label == p.Label);
                return (Id == p.Id);
            }
        }
    }
}
