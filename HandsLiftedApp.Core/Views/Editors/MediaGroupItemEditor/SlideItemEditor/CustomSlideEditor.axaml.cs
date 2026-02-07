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
    public partial class CustomSlideEditor : UserControl
    {
        public CustomSlideEditor()
        {
            InitializeComponent();

            if (Design.IsDesignMode)
            {
                this.DataContext = new CustomSlide() { SlideElements = new() { new TextElement() { Text = "Bible Reading" }, new ImageElement() { FilePath = "avares://HandsLiftedApp.Core/Assets/app.png"} } };
            }

            VisualEditor.OnUpdateSelectedElement += (sender, args) =>
            {
                ListBox.SelectedItem = args.SelectedElement;
            };
        }

        public void UpdateXml()
        {
            if (this.DataContext is CustomSlide vm)
            {
                _enableXMLParse = false;
                XmlSerializer serializer = new XmlSerializer(typeof(CustomSlide));
                using (StringWriter writer = new())
                {
                    serializer.Serialize(writer, vm);
                    CodeEditor.Text = writer.ToString();
                }

                _enableXMLParse = true;
            }
        }

        private void AddSlideElement(SlideElement slideElement)
        {
            if (this.DataContext is CustomSlide vm)
            {
                slideElement.X = vm.SlideWidth / 2 - slideElement.Width / 2;
                slideElement.Y = vm.SlideHeight / 2 - slideElement.Height / 2;
                vm.SlideElements.Add(slideElement);
            }
        }

        private async void ChangeImageElement_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                if (control.DataContext is ImageElement imageElement)
                {
                    var result = await SelectFilePicker();
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        imageElement.FilePath = result;
                    }
                }
            }
        }
        
        private async void ChangeSlideBgGraphic_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is CustomSlide customSlide)
            {
                var result = await SelectFilePicker();
                if (!string.IsNullOrWhiteSpace(result))
                {
                    customSlide.BackgroundGraphicFilePath = result;
                }
            }
        }
        
        private void ButtonAddTextElement_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is CustomSlide vm)
            {
                AddSlideElement(new TextElement());
            }
        }
        
        private void ButtonAddImageElement_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is CustomSlide vm)
            {
                AddSlideElement(new ImageElement());
            }
        }

        private void MoveItemUp_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is CustomSlide vm)
            {
                if (sender is Control { DataContext: SlideElement slideElement })
                {
                    var indexOf = vm.SlideElements.IndexOf(slideElement);
                    vm.SlideElements.Move(indexOf, Math.Max(indexOf - 1, 0));
                }
            }
        }

        private void MoveItemDown_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is CustomSlide vm)
            {
                if (sender is Control { DataContext: SlideElement slideElement })
                {
                    var indexOf = vm.SlideElements.IndexOf(slideElement);
                    vm.SlideElements.Move(indexOf, Math.Min(indexOf + 1, vm.SlideElements.Count - 1));
                }
            }
        }

        private void DeleteItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is CustomSlide vm)
            {
                if (sender is Control { DataContext: SlideElement slideElement })
                {
                    vm.SlideElements.Remove(slideElement);
                }
            }
        }

        private void DuplicateItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is CustomSlide vm)
            {
                if (sender is Control { DataContext: SlideElement slideElement })
                {
                    
                    
                    XmlSerializer serializer = new XmlSerializer(typeof(SlideElement));
                    using (StringWriter writer = new())
                    {
                        serializer.Serialize(writer, slideElement);
                        var obj = writer.ToString();
                        
                        using (StringReader reader = new StringReader(obj))
                        {
                            vm.SlideElements.Add(serializer.Deserialize(reader) as SlideElement);
                        }
                    }
                    
                }
            }
        }

        private void UpdateXmlButton_OnClick(object? sender, RoutedEventArgs e)
        {
            UpdateXml();
        }

        private bool _enableXMLParse = true;

        private void CodeEditor_OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_enableXMLParse && this.DataContext is CustomSlide vm)
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(CustomSlide));

                    using (StringReader reader = new StringReader(CodeEditor.Text))
                    {
                        // TODO does this work?
                        vm = serializer.Deserialize(reader) as CustomSlide;
                    }
                }
                catch (Exception exception)
                {
                    Log.Error(exception, "Failed to parse XML");
                }
            }
        }

        // TODO probably double-firing on visual editor selection click
        private void ListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var addedItem = e.AddedItems[0];
                if (addedItem is SlideElement element)
                {
                    VisualEditor.SelectElement(element);
                }
            }
        }
        
        private async Task<string?> SelectFilePicker()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
                return null;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select File",
                AllowMultiple = false
            });

            if (files is null || files.Count == 0)
                return null;

            return files[0].TryGetLocalPath();
        }

    }
}