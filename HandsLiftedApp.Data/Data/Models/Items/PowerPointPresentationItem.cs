﻿using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Models.Items
{
    [XmlRoot("PowerPoint", Namespace = Constants.Namespace, IsNullable = false)]
    [Serializable]
    public class PowerPointPresentationItem : MediaGroupItem
    {
        public PowerPointPresentationItem()
        {
        }

        private string _sourcePresentationFile;

        public string SourcePresentationFile { get => _sourcePresentationFile; set => this.RaiseAndSetIfChanged(ref _sourcePresentationFile, value); }

        private string _sourceSlidesExportDirectory;

        public string SourceSlidesExportDirectory { get => _sourceSlidesExportDirectory; set => this.RaiseAndSetIfChanged(ref _sourceSlidesExportDirectory, value); }

        // <"PowerPoint Slide ID", exported slide image filename> in order of slide index 
        private Dictionary<string, string> _slideIdMap = new Dictionary<string, string>();
        // TODO make this serializable
        [XmlIgnore]
        public Dictionary<string, string> SlideIdMap { get => _slideIdMap; set => this.RaiseAndSetIfChanged(ref _slideIdMap, value); }

    }
    public interface IPowerPointSlidesGroupItemState
    {
    }
}
