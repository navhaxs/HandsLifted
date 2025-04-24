using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using HandsLiftedApp.Core.Models.Library;
using HandsLiftedApp.Core.Utils;

namespace HandsLiftedApp.Core.Views.LibraryView
{
    public partial class LibraryQueryView : UserControl
    {
        public LibraryQueryView()
        {
            AsyncImageLoader.ImageLoader.AsyncImageLoader = new WindowsThumbnailImageLoader();
            
            InitializeComponent();
        }
        
        private async void DockPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Control control)
            {
                if (control.DataContext is LibraryItem libraryItem)
                {
                    var dragData = new DataObject();
                    var topLevel = TopLevel.GetTopLevel(this);
                    IStorageFile originalCoverImage = await topLevel.StorageProvider.TryGetFileFromPathAsync((Uri)new Uri(libraryItem.FullFilePath));
                    
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

        private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchBox.Text = "";
            }
        }
    }
}