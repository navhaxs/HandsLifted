using System.IO;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;

namespace SlideEditorStandalone;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}