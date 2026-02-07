using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Models.SlideElement;
using Serilog;

namespace HandsLiftedApp.Core.Views.Editors.FreeText
{
    public partial class MediaItemEditor : UserControl
    {
        public MediaItemEditor()
        {
            InitializeComponent();
            
            if (Design.IsDesignMode)
            {
                this.DataContext = new MediaGroupItem.MediaItem() { SourceMediaFilePath = "avares://HandsLiftedApp.Core/Assets/DefaultTheme/VisionScreens_1440_placeholder.png" };
            }
            
        }

    }
}