using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia;
using Avalonia.VisualTree;
using HandsLiftedApp.Core.Models.UI;
using HandsLiftedApp.Core.Undo;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data.Data.Models.Slides;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Models.SlideElement;
using HandsLiftedApp.Utils;
using ReactiveUI;
using Serilog;

namespace HandsLiftedApp.Core.Views.Editors.FreeText
{
    /// <summary>A picture read from the clipboard, already encoded as PNG.</summary>
    internal sealed record ClipboardPicture(byte[] PngBytes, PixelSize Pixels);

    public partial class CustomSlideEditor : UserControl
    {
        public CustomSlideEditor()
        {
            ReadClipboardFilePaths = ReadClipboardFilePathsFromSystem;
            ReadClipboardPicture = ReadClipboardPictureFromSystem;
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
            VisualEditor.ImageFilesDropped += (paths, slidePoint) =>
                PendingImageAction = RunLoggedAsync("add dropped images", () => AddImagesFromFilesAsync(paths, slidePoint));
            VisualEditor.PasteRequested += (_, _) =>
                PendingImageAction = RunLoggedAsync("paste", PasteAsync);

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
            Toolbar.AddImageRequested += async (_, _) => await RunLoggedAsync("add image", AddImageAsync);
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

        /// <summary>Add Image: asks for the picture first (an empty image box draws nothing and looks broken).</summary>
        private Task AddImageAsync() => AddImageAsync(
            () => SelectFilePicker("Add Image"),
            async path => (await BitmapLoader.LoadBitmapAsync(path))?.PixelSize);

        /// <summary>
        /// Picks a file, then adds an image element already fitted to it, centred, frontmost and selected, as ONE undo
        /// step. Cancelling the picker, or a file that cannot be read as a picture, adds nothing (no empty placeholder
        /// and no undo step). The two functions are the file picker and the picture-size lookup, injected for tests.
        /// </summary>
        internal async Task AddImageAsync(Func<Task<string?>> pickFile, Func<string, Task<PixelSize?>> measure)
        {
            CommitInlineEdit();
            if (DataContext is not CustomSlide slide) return;

            var path = await pickFile();
            if (string.IsNullOrWhiteSpace(path)) return;

            // busy from here, not while the file dialog was open (the user was choosing then)
            using var busy = BeginBusy("Adding picture...");

            var pixels = await measure(path);
            if (pixels == null)
            {
                Log.Warning("Custom slide editor: {Path} could not be read as a picture - no image added", path);
                return;
            }

            // the editor is rebuilt for another slide when the selected slide changes; never add to the wrong one
            if (!ReferenceEquals(DataContext, slide)) return;

            // like every other add flow, the picture is copied into the Media Library so the playlist is portable
            var storedPath = await LocalizeImageAsync(path);
            if (storedPath == null || !ReferenceEquals(DataContext, slide)) return;

            ImageElement element;
            using (_activeHistory?.BeginBatch("Add Image"))
            {
                element = SlideElementCommands.AddImage(slide);
                FitElementToImage(element, pixels.Value, slide);
                element.FilePath = storedPath;
            }

            Select(element);
        }

        // ---- busy feedback ----
        // A paste, drop or add-image copies a file into the Media Library, may read and encode a large clipboard
        // picture and decodes it to measure it, which takes visible time for a photo or a screenshot. While that runs
        // the cursor is the wait cursor and the status bar shows an indeterminate progress bar. Scopes nest (a paste
        // runs an add inside it); everything is on the UI thread (continuations resume there).
        private int _busyCount;

        /// <summary>True while a paste, drop or add-image is working.</summary>
        internal bool IsBusy => _busyCount > 0;

        private IDisposable BeginBusy(string message)
        {
            _busyCount++;
            BusyText.Text = message;
            BusyPanel.IsVisible = true;
            Cursor = new Cursor(StandardCursorType.Wait);
            return new BusyScope(this);
        }

        private void EndBusy()
        {
            if (--_busyCount > 0) return;
            _busyCount = 0;
            BusyPanel.IsVisible = false;
            Cursor = null;
        }

        private sealed class BusyScope : IDisposable
        {
            private CustomSlideEditor? _editor;
            public BusyScope(CustomSlideEditor editor) => _editor = editor;

            public void Dispose()
            {
                // disposed twice is harmless
                var editor = _editor;
                _editor = null;
                editor?.EndBusy();
            }
        }

        /// <summary>The configured Media Library folder; injectable so tests do not need <c>Globals</c>.</summary>
        internal Func<string?> MediaLibraryPathProvider { get; set; } = () => Globals.Instance.AppPreferences?.MediaLibraryPath;

        /// <summary>Shows "title, message" to the user; injectable so tests do not need a window.</summary>
        internal Action<string, string> ShowMediaLibraryError { get; set; } = (title, message) =>
            MessageBus.Current.SendMessage(new MessageWindowViewModel { Title = title, Content = message });

        /// <summary>The picture-size lookup used by drops and pastes; injectable for tests.</summary>
        internal Func<string, Task<PixelSize?>> MeasureImage { get; set; } = MeasureWithBitmapLoader;

        private static async Task<PixelSize?> MeasureWithBitmapLoader(string path) =>
            (await BitmapLoader.LoadBitmapAsync(path))?.PixelSize;

        /// <summary>
        /// Reads the local paths of the files on the clipboard (null or empty when there are none); injectable so tests
        /// do not need a real clipboard.
        /// </summary>
        internal Func<Task<IReadOnlyList<string>?>> ReadClipboardFilePaths { get; set; }

        private async Task<IReadOnlyList<string>?> ReadClipboardFilePathsFromSystem()
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null) return null;
            var items = await clipboard.TryGetFilesAsync();
            return items?.Select(item => item.TryGetLocalPath()).OfType<string>().ToList();
        }

        /// <summary>
        /// The most recent drop/paste started by a designer event (fire-and-forget from the UI's point of view);
        /// tests await it.
        /// </summary>
        internal Task PendingImageAction { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// Ctrl+V on the canvas: picture FILES on the clipboard (copied in Explorer) are added like a drop at the slide
        /// centre. Failing that, a raw picture (<see cref="ReadClipboardPicture"/>: screenshot, "Copy image") is saved
        /// as a PNG in Media/Pasted and added the same way. Anything else is ignored. Nothing is added when the slide
        /// changes while the clipboard is read or the picture saved.
        /// </summary>
        internal async Task PasteAsync()
        {
            CommitInlineEdit();
            using var busy = BeginBusy("Pasting...");
            var slide = DataContext as CustomSlide;
            var paths = await ReadClipboardFilePaths();
            var images = CustomSlideDesigner.ImageFilePaths(paths);
            if (images.Count > 0)
            {
                await AddImagesFromFilesAsync(images, null, expectedSlide: slide);
                return;
            }

            // no files: a raw picture (screenshot, "Copy image" from a browser) is saved as a PNG in Media/Pasted and
            // then added through the very same path as every other image
            var picture = await ReadClipboardPicture();
            if (picture == null || !ReferenceEquals(DataContext, slide)) return;

            CommitInlineEdit();
            string savedPath;
            try
            {
                var libraryPath = MediaLibraryPathProvider();
                savedPath = await Task.Run(() => PortableAssetCopier.SavePastedImage(picture.PngBytes, libraryPath));
            }
            catch (MediaLibraryNotConfiguredException ex)
            {
                Log.Warning("Custom slide editor: Media Library not configured - pasted picture not added");
                ShowMediaLibraryError("Media Library not configured", ex.Message);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Error(ex, "Custom slide editor: could not save the pasted picture into the Media Library");
                return;
            }

            await AddImagesFromFilesAsync(new[] { savedPath }, null, _ => Task.FromResult<PixelSize?>(picture.Pixels),
                expectedSlide: slide);
        }

        /// <summary>
        /// Reads a raw picture from the clipboard (null when there is none); injectable so tests do not need a real
        /// clipboard.
        /// </summary>
        internal Func<Task<ClipboardPicture?>> ReadClipboardPicture { get; set; }

        private async Task<ClipboardPicture?> ReadClipboardPictureFromSystem()
        {
            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard == null) return null;
                using var bitmap = await clipboard.TryGetBitmapAsync();
                if (bitmap == null) return null;
                var pixels = bitmap.PixelSize;
                var bytes = await Task.Run(() =>
                {
                    using var stream = new MemoryStream();
                    bitmap.Save(stream); // PNG
                    return stream.ToArray();
                });
                return new ClipboardPicture(bytes, pixels);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Custom slide editor: could not read a picture from the clipboard - treated as none");
                return null;
            }
        }

        /// <summary>
        /// Adds dropped or pasted picture files: only images (by extension) are used, each is localized into the Media
        /// Library's <see cref="PortableAssetCopier.PastedSubfolder"/> and measured; files that fail are skipped. All
        /// that succeeded are added as ONE undo step, the first centred on <paramref name="slidePoint"/> (the slide
        /// centre when null) and each further one 40 px right and down from the previous, all fitted inside the slide,
        /// the last one selected. Nothing at all happens when no file qualifies or the slide changed meanwhile.
        /// </summary>
        internal async Task AddImagesFromFilesAsync(IReadOnlyList<string> paths, Point? slidePoint,
            Func<string, Task<PixelSize?>>? measure = null, CustomSlide? expectedSlide = null)
        {
            CommitInlineEdit();
            if (DataContext is not CustomSlide slide) return;
            // a paste captured its slide before its first await: if the editor moved on since, add nothing
            if (expectedSlide != null && !ReferenceEquals(expectedSlide, slide)) return;
            using var busy = BeginBusy("Adding pictures...");
            measure ??= MeasureImage;

            var images = CustomSlideDesigner.ImageFilePaths(paths);
            var ready = new List<(string Path, PixelSize Pixels)>();
            foreach (var path in images)
            {
                var (outcome, storedPath) = await TryLocalizeImageAsync(path, PortableAssetCopier.ResolveOrCopyIntoPastedFolder);
                // the user has been told once; every other file would fail the same way
                if (outcome == LocalizeOutcome.LibraryNotConfigured) return;
                if (storedPath == null) continue;

                var pixels = await measure(storedPath);
                if (pixels == null)
                {
                    Log.Warning("Custom slide editor: {Path} could not be read as a picture - skipped", path);
                    continue;
                }

                ready.Add((storedPath, pixels.Value));
            }

            // the editor is rebuilt for another slide when the selected slide changes; never add to the wrong one
            if (ready.Count == 0 || !ReferenceEquals(DataContext, slide)) return;

            var start = slidePoint ?? new Point(slide.SlideWidth / 2.0, slide.SlideHeight / 2.0);
            ImageElement? last = null;
            using (_activeHistory?.BeginBatch(ready.Count == 1 ? "Add Image" : "Add Images"))
            {
                for (var i = 0; i < ready.Count; i++)
                {
                    var element = SlideElementCommands.AddImage(slide);
                    // put the starting box's centre where the picture should go; fitting keeps the centre (and clamps)
                    var centreX = start.X + FanOutStep * i;
                    var centreY = start.Y + FanOutStep * i;
                    element.X = (int)Math.Round(centreX - element.Width / 2.0);
                    element.Y = (int)Math.Round(centreY - element.Height / 2.0);
                    FitElementToImage(element, ready[i].Pixels, slide);
                    element.FilePath = ready[i].Path;
                    last = element;
                }
            }

            Select(last);
        }

        /// <summary>How far each further dropped/pasted picture is moved right and down from the previous one.</summary>
        private const int FanOutStep = 40;

        private enum LocalizeOutcome { Stored, LibraryNotConfigured, Failed }

        /// <summary>
        /// Copies a picked picture into the Media Library (off the UI thread) and returns the library path, or null
        /// when the picture must not be used (no library configured - the user is told - or the copy failed).
        /// </summary>
        private async Task<string?> LocalizeImageAsync(string pickedPath) =>
            (await TryLocalizeImageAsync(pickedPath, PortableAssetCopier.ResolveOrCopyIntoMediaLibrary)).StoredPath;

        /// <summary>
        /// Runs <paramref name="copier"/> (which Media Library folder the picture goes to) off the UI thread. A missing
        /// library is shown to the user; an IO failure is logged. Only <see cref="LocalizeOutcome.Stored"/> has a path.
        /// </summary>
        private async Task<(LocalizeOutcome Outcome, string? StoredPath)> TryLocalizeImageAsync(
            string path, Func<string, string?, string> copier)
        {
            var libraryPath = MediaLibraryPathProvider();
            try
            {
                return (LocalizeOutcome.Stored, await Task.Run(() => copier(path, libraryPath)));
            }
            catch (MediaLibraryNotConfiguredException ex)
            {
                Log.Warning("Custom slide editor: Media Library not configured - {Path} not added", path);
                ShowMediaLibraryError("Media Library not configured", ex.Message);
                return (LocalizeOutcome.LibraryNotConfigured, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Error(ex, "Custom slide editor: could not copy {Path} into the Media Library", path);
                return (LocalizeOutcome.Failed, null);
            }
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
        private Task ReplaceImageAsync(ImageElement imageElement) => ReplaceImageAsync(
            imageElement,
            () => SelectFilePicker("Select Image"),
            async path => (await BitmapLoader.LoadBitmapAsync(path))?.PixelSize);

        internal async Task ReplaceImageAsync(ImageElement imageElement, Func<Task<string?>> pickFile, Func<string, Task<PixelSize?>> measure)
        {
            CommitInlineEdit();
            var result = await pickFile();
            if (string.IsNullOrWhiteSpace(result)) return;

            using var busy = BeginBusy("Replacing picture...");

            // decode first (off the UI thread; it also lands in the bitmap cache, so the element then
            // shows it immediately) so the box can be fitted to the picture
            var pixels = await measure(result);

            var storedPath = await LocalizeImageAsync(result);
            if (storedPath == null) return;

            // one undo step for the new picture and its new size
            using (_activeHistory?.BeginBatch("Change Image"))
            {
                if (pixels != null && DataContext is CustomSlide slide)
                    FitElementToImage(imageElement, pixels.Value, slide);
                imageElement.FilePath = storedPath;
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
                await RunLoggedAsync("change background graphic",
                    () => ChangeBackgroundGraphicAsync(() => SelectFilePicker()));
            }
        }

        internal async Task ChangeBackgroundGraphicAsync(Func<Task<string?>> pickFile)
        {
            if (DataContext is not CustomSlide customSlide) return;
            var result = await pickFile();
            if (string.IsNullOrWhiteSpace(result)) return;

            var storedPath = await LocalizeImageAsync(result);
            if (storedPath == null || !ReferenceEquals(DataContext, customSlide)) return;
            customSlide.BackgroundGraphicFilePath = storedPath;
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
        
        private async Task<string?> SelectFilePicker(string title = "Select File")
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
                return null;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                // pictures first, but not exclusively (other formats the decoder supports can still be chosen)
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll, FilePickerFileTypes.All },
            });

            if (files is null || files.Count == 0)
                return null;

            return files[0].TryGetLocalPath();
        }

    }
}