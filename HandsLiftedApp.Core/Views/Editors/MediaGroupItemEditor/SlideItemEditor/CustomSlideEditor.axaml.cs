using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia;
using Avalonia.VisualTree;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Utils;
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

            WireToolbar();
            WireStatusBar();
            SyncSelection();

            DataContextChanged += (_, _) =>
            {
                Toolbar.Slide = DataContext as CustomSlide;
                ActivateHistoryForCurrentSlide();
            };
            Toolbar.Slide = DataContext as CustomSlide;
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
            Toolbar.SetUndoState(stack?.CanUndo == true, stack?.CanRedo == true, stack?.UndoMenuText, stack?.RedoMenuText);
        }

        // ---------------- toolbar ----------------

        private SlideElement? SelectedElement => ListBox.SelectedItem as SlideElement;

        private void WireToolbar()
        {
            Toolbar.UndoRequested += (_, _) =>
            {
                CommitInlineEdit();
                _activeHistory?.Stack.Undo();
            };
            Toolbar.RedoRequested += (_, _) =>
            {
                CommitInlineEdit();
                _activeHistory?.Stack.Redo();
            };
            Toolbar.AddTextRequested += (_, _) => AddText();
            Toolbar.AddImageRequested += (_, _) => AddImage();
            Toolbar.DuplicateRequested += (_, _) => Duplicate(SelectedElement);
            Toolbar.DeleteRequested += (_, _) => Delete(SelectedElement);
            Toolbar.BringForwardRequested += (_, _) => BringForward(SelectedElement);
            Toolbar.SendBackwardRequested += (_, _) => SendBackward(SelectedElement);
            Toolbar.ReplaceImageRequested += async (_, _) =>
            {
                if (SelectedElement is ImageElement image) await RunLoggedAsync("replace image", () => ReplaceImageAsync(image));
            };
            Toolbar.FitImageRequested += async (_, _) =>
            {
                if (SelectedElement is ImageElement image) await RunLoggedAsync("fit image", () => FitImageAsync(image));
            };
            Toolbar.ChangeBackgroundGraphicRequested += (_, _) => ChangeSlideBgGraphic_OnClick(Toolbar, new RoutedEventArgs());
        }

        private void AddText()
        {
            if (DataContext is not CustomSlide vm) return;
            var element = SlideElementCommands.AddText(vm);
            Select(element);
            // straight into typing, with the placeholder selected so the first keystroke replaces it
            VisualEditor.BeginEdit(element, selectAll: true);
        }

        /// <summary>
        /// Ends an open inline text edit (writing the typed text as its one undo step) before an action that works on
        /// the model. Toolbar buttons and menus cannot take focus, so clicking one does not end the edit by itself;
        /// without this a duplicate copies the old text, a delete cancels the edit, and undo throws the typing away.
        /// </summary>
        private void CommitInlineEdit() => VisualEditor.CommitEdit();

        private void AddImage()
        {
            CommitInlineEdit();
            if (DataContext is CustomSlide vm) Select(SlideElementCommands.AddImage(vm));
        }

        private void Duplicate(SlideElement? element)
        {
            CommitInlineEdit();
            if (DataContext is CustomSlide vm && element != null) Select(SlideElementCommands.Duplicate(vm, element));
        }

        private void Delete(SlideElement? element)
        {
            CommitInlineEdit();
            if (DataContext is CustomSlide vm) SlideElementCommands.Delete(vm, element);
        }

        // ObservableCollection.Move reaches the ListBox as remove + add, so it drops the moved element from its
        // selection; select it again explicitly rather than relying on the order of CollectionChanged handlers.
        private void BringForward(SlideElement? element)
        {
            CommitInlineEdit();
            if (DataContext is CustomSlide vm && element != null && SlideElementCommands.BringForward(vm, element))
                Select(element);
        }

        private void SendBackward(SlideElement? element)
        {
            CommitInlineEdit();
            if (DataContext is CustomSlide vm && element != null && SlideElementCommands.SendBackward(vm, element))
                Select(element);
        }

        /// <summary>Makes <paramref name="element"/> the selection (layers list, designer, toolbar, properties).</summary>
        private void Select(SlideElement? element)
        {
            if (element != null) ListBox.SelectedItem = element;
        }

        // ---------------- status bar (zoom) ----------------

        private void WireStatusBar()
        {
            VisualEditor.ZoomChanged += (_, _) => RefreshZoomText();
            RefreshZoomText();
        }

        private void RefreshZoomText() => ZoomPercentText.Text = $"{Math.Round(VisualEditor.ZoomPercent)}%";

        private void ZoomOutButton_OnClick(object? sender, RoutedEventArgs e) => VisualEditor.ZoomOut();
        private void ZoomInButton_OnClick(object? sender, RoutedEventArgs e) => VisualEditor.ZoomIn();
        private void FitButton_OnClick(object? sender, RoutedEventArgs e) => VisualEditor.FitToView();

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

        /// <summary>
        /// Runs an async handler body from an async-void entry point: a file-picker or decode exception is logged,
        /// never rethrown (it would reach the dispatcher and crash the app).
        /// </summary>
        private static async Task RunLoggedAsync(string what, Func<Task> body)
        {
            try
            {
                await body();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Custom slide editor: failed to {Action}", what);
            }
        }

        private async void ChangeImageElement_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: ImageElement imageElement })
                await RunLoggedAsync("replace image", () => ReplaceImageAsync(imageElement));
        }

        /// <summary>Asks for a picture and puts it in the element, fitting the box to it (one undo step).</summary>
        private async Task ReplaceImageAsync(ImageElement imageElement)
        {
            CommitInlineEdit();
            var result = await SelectFilePicker();
            if (string.IsNullOrWhiteSpace(result)) return;

            // decode first (off the UI thread; it also lands in the bitmap cache, so the element then
            // shows it immediately) so the box can be fitted to the picture
            var bitmap = await BitmapLoader.LoadBitmapAsync(result);

            // one undo step for the new picture and its new size
            using (_activeHistory?.BeginBatch("Change Image"))
            {
                if (bitmap != null && DataContext is CustomSlide slide)
                    FitElementToImage(imageElement, bitmap.PixelSize, slide);
                imageElement.FilePath = result;
            }
        }

        /// <summary>Gives the element its current picture's proportions (one undo step).</summary>
        private async Task FitImageAsync(ImageElement imageElement)
        {
            CommitInlineEdit();
            var path = imageElement.FilePath;
            if (string.IsNullOrWhiteSpace(path)) return;

            var bitmap = await BitmapLoader.LoadBitmapAsync(path);
            // skip if the picture or the slide changed while it was loading
            if (bitmap == null || DataContext is not CustomSlide slide || imageElement.FilePath != path) return;

            using (_activeHistory?.BeginBatch("Fit to Image"))
                FitElementToImage(imageElement, bitmap.PixelSize, slide);
        }
        
        /// <summary>
        /// Gives the element the picture's own proportions (its pixel size, scaled down to fit the slide), keeping its
        /// centre where it was. Without this a newly added image keeps the 100x100 starting box, so the selection
        /// handles sit around empty space and the picture is letterboxed inside it.
        /// </summary>
        internal static void FitElementToImage(ImageElement element, PixelSize pixels, CustomSlide slide)
        {
            if (pixels.Width <= 0 || pixels.Height <= 0) return;

            var scale = Math.Min(1.0, Math.Min((double)slide.SlideWidth / pixels.Width, (double)slide.SlideHeight / pixels.Height));
            var width = Math.Max(1, (int)Math.Round(pixels.Width * scale));
            var height = Math.Max(1, (int)Math.Round(pixels.Height * scale));

            var centreX = element.X + element.Width / 2.0;
            var centreY = element.Y + element.Height / 2.0;
            var x = (int)Math.Round(Math.Clamp(centreX - width / 2.0, 0, Math.Max(0, slide.SlideWidth - width)));
            var y = (int)Math.Round(Math.Clamp(centreY - height / 2.0, 0, Math.Max(0, slide.SlideHeight - height)));

            element.Width = width;
            element.Height = height;
            element.X = x;
            element.Y = y;
        }

        private async void ChangeSlideBgGraphic_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is CustomSlide customSlide)
            {
                await RunLoggedAsync("change background graphic", async () =>
                {
                    var result = await SelectFilePicker();
                    if (!string.IsNullOrWhiteSpace(result))
                    {
                        customSlide.BackgroundGraphicFilePath = result;
                    }
                });
            }
        }
        
        private void MoveItemUp_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: SlideElement el }) BringForward(el);
        }

        private void MoveItemDown_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: SlideElement el }) SendBackward(el);
        }

        private void DeleteItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: SlideElement el }) Delete(el);
        }

        private void DuplicateItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: SlideElement el }) Duplicate(el);
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

        // The layers list is the one source of truth for the selection: the designer reports clicks into it (see the
        // constructor) and everything else follows it from here.
        private void ListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => SyncSelection();

        private void SyncSelection()
        {
            var element = SelectedElement;

            if (element != null) VisualEditor.SelectElement(element);
            else VisualEditor.ClearSelection();

            Toolbar.SelectedElement = element;

            // set imperatively, not via {Binding #ListBox.SelectedItem...} (goes stale on selection changes, CLAUDE.md)
            SelectedElementProperties.Content = element;
            GeometryPanel.DataContext = element;
            GeometryPanel.IsVisible = element != null;
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