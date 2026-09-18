using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Core.Models.Library.Config;
using HandsLiftedApp.Core.Views.Setup;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.Utils;
using ReactiveUI;

namespace HandsLiftedApp.Core.ViewModels
{
    public class SetupWindowViewModel : ReactiveObject
    {
        private List<AppPreferencesViewModel.DisplayModel?> _AllAvailableScreens = new();
        public List<AppPreferencesViewModel.DisplayModel?> AllAvailableScreens { get => _AllAvailableScreens; set => this.RaiseAndSetIfChanged(ref _AllAvailableScreens, value); }
        List<DisplayIdentifyWindow> wnds = new();

        // The Setup window shows one section per library type instead of a Type picker per row.
        // Each collection is authoritative for its section; edits flatten back into the live
        // LibraryConfig.LibraryItems and persist immediately (see AttachAutoPersist) - no
        // separate Save/Reload step.
        public ObservableCollection<LibraryConfig.LibraryDefinition> MediaLibraryRows { get; } = new();
        public ObservableCollection<LibraryConfig.LibraryDefinition> SongLibraryRows { get; } = new();
        public ObservableCollection<LibraryConfig.LibraryDefinition> ScriptureLibraryRows { get; } = new();

        public void AddMediaLibraryRow() =>
            MediaLibraryRows.Add(new LibraryConfig.LibraryDefinition { Label = "New Library", Type = LibraryType.Media });

        public void AddSongLibraryRow() =>
            SongLibraryRows.Add(new LibraryConfig.LibraryDefinition { Label = "New Library", Type = LibraryType.Song });

        public void AddScriptureLibraryRow() =>
            ScriptureLibraryRows.Add(new LibraryConfig.LibraryDefinition { Label = "New Library", Type = LibraryType.Scripture });

        public void RemoveLibraryRow(LibraryConfig.LibraryDefinition row)
        {
            if (!MediaLibraryRows.Remove(row))
                if (!SongLibraryRows.Remove(row))
                    ScriptureLibraryRows.Remove(row);
        }

        private void AttachAutoPersist()
        {
            var config = Globals.Instance.MainViewModel.LibraryViewModel.LibraryConfig;
            foreach (var row in config.LibraryItems)
            {
                var target = row.Type switch
                {
                    LibraryType.Song => SongLibraryRows,
                    LibraryType.Scripture => ScriptureLibraryRows,
                    _ => MediaLibraryRows
                };
                target.Add(row);
            }

            foreach (var row in MediaLibraryRows.Concat(SongLibraryRows).Concat(ScriptureLibraryRows))
            {
                row.PropertyChanged += OnLibraryRowChanged;
            }

            foreach (var rows in new[] { MediaLibraryRows, SongLibraryRows, ScriptureLibraryRows })
            {
                rows.CollectionChanged += (_, e) =>
                {
                    if (e.OldItems != null)
                        foreach (LibraryConfig.LibraryDefinition row in e.OldItems)
                            row.PropertyChanged -= OnLibraryRowChanged;

                    if (e.NewItems != null)
                        foreach (LibraryConfig.LibraryDefinition row in e.NewItems)
                            row.PropertyChanged += OnLibraryRowChanged;

                    PersistLibraryRows();
                };
            }
        }

        private void OnLibraryRowChanged(object? sender, PropertyChangedEventArgs e) => PersistLibraryRows();

        private void PersistLibraryRows()
        {
            var libraryViewModel = Globals.Instance.MainViewModel.LibraryViewModel;
            libraryViewModel.LibraryConfig.LibraryItems = new ObservableCollection<LibraryConfig.LibraryDefinition>(
                MediaLibraryRows.Concat(SongLibraryRows).Concat(ScriptureLibraryRows));
            libraryViewModel.PersistLibraries();
        }

        public SetupWindowViewModel(Screens screens)
        {
            if (!Design.IsDesignMode)
            {
                AttachAutoPersist();
            }

            var friendlyNames = Win32DisplayHelper.GetMonitorFriendlyNames();

            AllAvailableScreens = screens.All.Select(i => new AppPreferencesViewModel.DisplayModel(i.Bounds)).ToList<AppPreferencesViewModel.DisplayModel?>();
            foreach (var (i, index) in AllAvailableScreens.WithIndex())
            {
                if (i == null) continue;
                var bounds = new PixelRect(i.X, i.Y, i.Width, i.Height);
                i.Label = friendlyNames.TryGetValue(bounds, out var name)
                    ? $"Display {index + 1} - {name}"
                    : $"Display {index + 1}";
            }

            AddConfiguredDisplayIfDisconnected(Globals.Instance.AppPreferences.OutputDisplayBounds);
            AddConfiguredDisplayIfDisconnected(Globals.Instance.AppPreferences.StageDisplayBounds);

            AllAvailableScreens.Add(null);
        }

        // Keeps the last-configured display selectable (and selected) even when it's not
        // currently connected, so the setup dropdowns don't reset to (Unset) just because
        // a projector/stage display happens to be off/unplugged at the moment Setup is opened.
        private void AddConfiguredDisplayIfDisconnected(AppPreferencesViewModel.DisplayModel? configured)
        {
            if (configured == null) return;
            if (_AllAvailableScreens.Any(s => s != null && s.Equals(configured))) return;

            _AllAvailableScreens.Add(new AppPreferencesViewModel.DisplayModel(new PixelRect(configured.X, configured.Y, configured.Width, configured.Height))
            {
                Label = $"{configured.Label} (Disconnected)"
            });
        }

        public void ShowDisplayIdentification(Screens screens)
        {
            foreach (var (i, index) in screens.All.WithIndex())
            {
                DisplayIdentifyWindow displayIdentifyWindow = new DisplayIdentifyWindow() { Screen = i };
                displayIdentifyWindow.Show();
                displayIdentifyWindow.Position = new PixelPoint(i.Bounds.X, i.Bounds.Y);
                displayIdentifyWindow.Topmost = true;
                
                if (OperatingSystem.IsWindows())
                    displayIdentifyWindow.WindowState = WindowState.FullScreen;
                
                displayIdentifyWindow.ScreenBounds.Text = $"{i.Bounds.Width} x {i.Bounds.Height}";
                displayIdentifyWindow.ScreenLocation.Text = $"({i.Bounds.X}, {i.Bounds.Y})";
                displayIdentifyWindow.ScreenNumber.Text = " ";
                displayIdentifyWindow.ScreenNumber.Text = $"{index + 1}";
                //Dispatcher.UIThread.InvokeAsync(() =>
                //{
                //    displayIdentifyWindow.WindowState = WindowState.FullScreen;
                //});
                // DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                // timer.Tick += (object? sender, EventArgs e) =>
                // {
                //     displayIdentifyWindow.Close();
                // };
                // timer.Start();
                wnds.Add(displayIdentifyWindow);
            }
        }
        public void HideDisplayItentification()
        {
            wnds.ForEach(wnd =>
            {
                if (wnd != null && wnd.IsVisible)
                {
                    wnd.Close();
                }
            });
        }
    }
}