﻿using Avalonia.Media;
using HandsLiftedApp.Data.Models.Types;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Xml.Serialization;


namespace HandsLiftedApp.Data.Models.Items
{
    /**
     * A section heading item 
     */
    [XmlRoot("SectionHeading", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class SectionHeadingItem : Item
    {
        public SectionHeadingItem()
        {
            Title = "(New Section)";
        }

        public XmlColor GroupColour = Color.Parse("#4d347f");
        public Color ItemGroupAvaloniaColor { get => GroupColour; set => this.RaiseAndSetIfChanged(ref GroupColour, value); }

        // A section heading item itself has no slides (it is empty)
        public override ObservableCollection<Slide> Slides => new ObservableCollection<Slide>() { };
    }
}