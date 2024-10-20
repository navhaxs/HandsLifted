﻿using Avalonia.Controls;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Data.SlideTheme;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models
{
    [XmlRoot("Playlist", Namespace = Constants.Namespace, IsNullable = false)]
    // todo: see https://stackoverflow.com/questions/11886290/use-the-xmlinclude-or-soapinclude-attribute-to-specify-types-that-are-not-known
    [XmlInclude(typeof(LogoItem))]
    [XmlInclude(typeof(SectionHeadingItem))]
    [XmlInclude(typeof(SongItem))]
    [XmlInclude(typeof(MediaGroupItem))]
    [XmlInclude(typeof(SlidesGroupItem))]
    [XmlInclude(typeof(GoogleSlidesGroupItem))]
    [XmlInclude(typeof(PowerPointPresentationItem))]
    [XmlInclude(typeof(PDFSlidesGroupItem))]
    [XmlInclude(typeof(CommentItem))]
    //
    [XmlInclude(typeof(ImageSlide))]
    [XmlInclude(typeof(VideoSlide))]
    [Serializable]
    public class Playlist : ReactiveObject
    {
        public Playlist()
        {
            Items = new TrulyObservableCollection<Item>();
            if (Design.IsDesignMode)
                return;
        }

        private String _title = "Untitled Playlist";
        public String Title { get => _title; set => this.RaiseAndSetIfChanged(ref _title, value); }

        // private String _notes = "Some notes go here";
        // public String Notes { get => _notes; set => this.RaiseAndSetIfChanged(ref _notes, value); }

        // TODO can this Dictionary have elements that can have bindings to?
        public SerializableDictionary<String, Object> Meta { get; set; } = new SerializableDictionary<String, Object>();

        // TODO move into Dictionary
        private String? _logoGraphicFile = @"avares://HandsLiftedApp.Core/Assets/DefaultTheme/logo-default.png";
        public String? LogoGraphicFile { get => _logoGraphicFile; set => this.RaiseAndSetIfChanged(ref _logoGraphicFile, value); }

        private ObservableCollection<BaseSlideTheme> _designs = new() {};
        public ObservableCollection<BaseSlideTheme> Designs { get => _designs; set => this.RaiseAndSetIfChanged(ref _designs, value); }

        // // TODO move into Dictionary
        // private DateTimeOffset _date = DateTimeOffset.Now;
        // public DateTimeOffset Date { get => _date; set => this.RaiseAndSetIfChanged(ref _date, value); }

        // [XmlIgnore]
        // public String PrettyDate => Date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        //
        private TrulyObservableCollection<Item> _items;

        public TrulyObservableCollection<Item> Items
        {
            get => _items;
            set => this.RaiseAndSetIfChanged(ref _items, value);
        }
    }
}
