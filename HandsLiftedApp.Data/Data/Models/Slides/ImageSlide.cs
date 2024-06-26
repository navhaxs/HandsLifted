﻿using Avalonia.Media.Imaging;
using System;
using System.IO;
using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Slides
{
    [XmlRoot(Namespace = Constants.Namespace)]
    [Serializable]
    public class ImageSlide : MediaSlide
    {
        public ImageSlide(string imagePath = @"C:\VisionScreens\TestImages\SWEC App Announcement.png") : this()
        {
            SourceMediaFilePath = imagePath;
        }

        public ImageSlide()
        {
        }

        public override string? SlideText => null;

        public override string? SlideLabel => Path.GetFileName(SourceMediaFilePath);

        public override void OnPreloadSlide()
        {
            // does not need to be async
            base.OnEnterSlide();
        }
        public override void OnEnterSlide()
        {
            base.OnEnterSlide();
        }

        public override void OnLeaveSlide()
        {
            base.OnLeaveSlide();
        }
        //
        // public Bitmap GetBitmap()
        // {
        //     // return State.GetBitmap();
        // }
    }
}
