using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Castle.Core.Logging;
using HandsLiftedApp.Core.Models.Library;

namespace HandsLiftedApp.Views.Library
{
    public partial class LibraryPaneView : UserControl
    {
        public LibraryPaneView()
        {
            InitializeComponent();

            //this.DataContext = new LogoSlide();
        }

        private const string CustomFormat = "application/xxx-avalonia-controlcatalog-custom";

        private async void DockPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Control control)
            {
                if (control.DataContext is LibraryItem libraryItem)
                {
                    var dragData = new DataObject();
                    var topLevel = TopLevel.GetTopLevel(this);
                    IStorageFile originalCoverImage = await topLevel.StorageProvider.TryGetFileFromPathAsync(new Uri(libraryItem.FullFilePath));
                    
                    dragData.Set(DataFormats.Files, new[] { originalCoverImage });

                    var result = await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Copy);
                    Debug.Print("a");
                    // switch (result)
                    // {
                    //     case DragDropEffects.Move:
                    //         dragState.Text = "Data was moved";
                    //         break;
                    //     case DragDropEffects.Copy:
                    //         dragState.Text = "Data was copied";
                    //         break;
                    //     case DragDropEffects.Link:
                    //         dragState.Text = "Data was linked";
                    //         break;
                    //     case DragDropEffects.None:
                    //         dragState.Text = "The drag operation was canceled";
                    //         break;
                    //     default:
                    //         dragState.Text = "Unknown result";
                    //         break;
                    // }
                }
            }
        }

        private void MenuItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: Core.Models.Library.Library library })
                Process.Start(new ProcessStartInfo(library.Config.Directory) { UseShellExecute = true });

        }
    }
}