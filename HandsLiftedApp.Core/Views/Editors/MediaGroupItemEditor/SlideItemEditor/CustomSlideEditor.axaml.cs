using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia;
using Avalonia.VisualTree;
using HandsLiftedApp.Core.Undo;
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
            VisualEditor.OnDeselectElement += (sender, args) =>
            {
                ListBox.SelectedItem = null;
            };

            DataContextChanged += (_, _) => ActivateHistoryForCurrentSlide();
            AttachedToVisualTree += (_, _) => ActivateHistoryForCurrentSlide();
            DetachedFromVisualTree += (_, _) => DeactivateHistory();
        }

        // ---------------- undo/redo ----------------
        // Each slide keeps its own history for as long as the editor *window* stays open. The histories live in
        // CustomSlideEditHistories, not here: the media group editor rebuilds this control whenever the selected
        // slide changes, so anything held by the control would be lost on a slide switch.

        private CustomSlideEditHistory? _activeHistory;
        private System.ComponentModel.PropertyChangedEventHandler? _activeHistoryHandler;

        private void ActivateHistoryForCurrentSlide()
        {
            DeactivateHistory();
            if (DataContext is not CustomSlide slide || Design.IsDesignMode || !this.IsAttachedToVisualTree()) return;

            var window = TopLevel.GetTopLevel(this);
            if (window == null) return;

            var history = CustomSlideEditHistories.Get(window, slide);
            _activeHistory = history;
            _activeHistoryHandler = (_, _) => RefreshUndoButtons();
            history.Stack.PropertyChanged += _activeHistoryHandler;
            RefreshUndoButtons();
        }

        private void DeactivateHistory()
        {
            if (_activeHistory != null && _activeHistoryHandler != null)
                _activeHistory.Stack.PropertyChanged -= _activeHistoryHandler;
            _activeHistory = null;
            _activeHistoryHandler = null;
            RefreshUndoButtons();
        }

        private void RefreshUndoButtons()
        {
            var stack = _activeHistory?.Stack;
            UndoButton.IsEnabled = stack?.CanUndo == true;
            RedoButton.IsEnabled = stack?.CanRedo == true;
            ToolTip.SetTip(UndoButton, stack?.UndoMenuText ?? "Undo");
            ToolTip.SetTip(RedoButton, stack?.RedoMenuText ?? "Redo");
        }

        private void UndoButton_OnClick(object? sender, RoutedEventArgs e) => _activeHistory?.Stack.Undo();
        private void RedoButton_OnClick(object? sender, RoutedEventArgs e) => _activeHistory?.Stack.Redo();

        /// <summary>Ctrl+Z / Ctrl+Y (or Ctrl+Shift+Z) for the slide being edited, unless a text box has focus (it keeps its own).</summary>
        protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled || _activeHistory == null) return;
            if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;

            var ctrl = (e.KeyModifiers & Avalonia.Input.KeyModifiers.Control) != 0;
            var shift = (e.KeyModifiers & Avalonia.Input.KeyModifiers.Shift) != 0;
            var alt = (e.KeyModifiers & Avalonia.Input.KeyModifiers.Alt) != 0;
            if (!ctrl || alt) return;

            if (e.Key == Avalonia.Input.Key.Z && !shift) _activeHistory.Stack.Undo();
            else if (e.Key == Avalonia.Input.Key.Y || (e.Key == Avalonia.Input.Key.Z && shift)) _activeHistory.Stack.Redo();
            else return;

            e.Handled = true;
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

        private static System.Collections.Generic.List<string>? _systemFontFamilies;

        // Font block lives inside a DataTemplate re-instantiated per selected element, so it can't be
        // wired up once in the constructor like SlideThemeDesigner does - populate on each realization instead.
        private void FontComboBox_OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                _systemFontFamilies ??= FontManager.Current.SystemFonts
                    .Select(x => x.Name)
                    .OrderBy(x => x)
                    .ToList();
                comboBox.ItemsSource = _systemFontFamilies;
            }
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