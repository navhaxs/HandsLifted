# Third-Party Notices Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an "Open Source Notices" button to the About window that opens a scrollable window listing all third-party library attributions.

**Architecture:** Static data class holds all notice entries. A converter maps license strings to foreground colors. A new `ThirdPartyNoticesWindow` displays the data in a read-only `DataGrid`. The About window gets a link-style button that opens the notices window as a modal dialog.

**Tech Stack:** Avalonia 11, C#, `DataGrid` (Avalonia.Controls), `IValueConverter` (Avalonia.Data.Converters)

## Global Constraints

- Avalonia 11.3.x — use `x:Class`, `AvaloniaXamlLoader.Load(this)`, `FindControl<T>` patterns matching existing windows
- Converter pattern: implement `IValueConverter` (non-nullable parameters) — match `FullscreenIconKindConverter.cs`
- Namespace for new converter: `HandsLiftedApp.Core.Converters`
- Namespace for new data class: `HandsLiftedApp.Core.Data`
- Namespace for new window: `HandsLiftedApp.Core.Views`
- No ViewModels needed — data is static; code-behind sets `ItemsSource` directly

---

### Task 1: ThirdPartyNotices data class

**Files:**
- Create: `HandsLiftedApp.Core/Data/ThirdPartyNotices.cs`

**Interfaces:**
- Produces: `NoticeEntry` record and `ThirdPartyNotices.All` (`IReadOnlyList<NoticeEntry>`) consumed by Task 3

- [ ] **Step 1: Create the file**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace HandsLiftedApp.Core.Data;

public record NoticeEntry(
    string Library,
    string License,
    string Copyright,
    string? Url = null);

public static class ThirdPartyNotices
{
    public static readonly IReadOnlyList<NoticeEntry> All =
        new NoticeEntry[]
        {
            new("AsyncImageLoader.Avalonia", "MIT",             "© AsyncImageLoader contributors"),
            new("Avalonia",                  "MIT",             "© Avalonia contributors",           "https://avaloniaui.net"),
            new("AvaloniaNDI",               "Public Domain + Ms-PL", "© AvaloniaNDI contributors"),
            new("BmpSharp",                  "MIT",             "© BmpSharp contributors"),
            new("ByteSize",                  "MIT",             "© Omar Khudeira"),
            new("Config.Net",                "MIT",             "© Ivan Gavryliuk"),
            new("DebounceThrottle",          "MIT",             "© DebounceThrottle contributors"),
            new("DynamicData",               "MIT",             "© Roland Pheasant",                 "https://github.com/reactivemarbles/DynamicData"),
            new("Google APIs (Drive/Slides)", "Apache 2.0",     "© Google LLC",                      "https://github.com/googleapis/google-api-dotnet-client"),
            new("HidApi.Net",                "MIT",             "© HidApi.Net contributors"),
            new("libmpv",                    "GPL 2.0+",        "© mpv contributors",                "https://mpv.io"),
            new("LibVLC / VideoLAN",         "LGPL 2.1+",       "© VideoLAN",                        "https://videolan.org"),
            new("LoadingIndicators.Avalonia", "MIT",            "© LoadingIndicators contributors"),
            new("Magick.NET",                "Apache 2.0",      "© Dirk Lemstra",                   "https://github.com/dlemstra/Magick.NET"),
            new("Material.Icons.Avalonia",   "MIT",             "© Material.Icons.Avalonia contributors"),
            new("NAudio",                    "Ms-PL",           "© Mark Heath",                      "https://github.com/naudio/NAudio"),
            new("NaturalSort.Extension",     "MIT",             "© NaturalSort contributors"),
            new("NDI SDK",                   "Proprietary",     "© Vizrt Group AS — includes RapidJSON (MIT), Speex (BSD), RapidXML (MIT), Opus (BSD), ASIO (Boost 1.0)"),
            new("Newtonsoft.Json",           "MIT",             "© James Newton-King",               "https://www.newtonsoft.com/json"),
            new("OpenMoji",                  "CC BY-SA 4.0",    "© OpenMoji contributors",           "https://openmoji.org"),
            new("PDFiumCore",                "BSD-3-Clause",    "© PDFium Authors"),
            new("protobuf-net",              "Apache 2.0",      "© Marc Gravell"),
            new("ReactiveUI",                "MIT",             "© ReactiveUI contributors",         "https://reactiveui.net"),
            new("RtfDomParser",              "MIT",             "© RtfDomParser contributors"),
            new("Serilog",                   "Apache 2.0",      "© Serilog contributors",            "https://serilog.net"),
            new("SIL.Scripture",             "MIT",             "© SIL International"),
            new("SkiaSharp",                 "MIT",             "© Microsoft",                       "https://github.com/mono/SkiaSharp"),
            new("Syncfusion",                "Commercial",      "© Syncfusion Inc.",                 "https://syncfusion.com"),
            new("System.Reactive",           "MIT",             "© .NET Foundation"),
            new("YamlDotNet",                "MIT",             "© Antoine Aubry",                   "https://github.com/aaubry/YamlDotNet"),
        }
        .OrderBy(e => e.Library, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
```

- [ ] **Step 2: Build to confirm no compile errors**

```
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add HandsLiftedApp.Core/Data/ThirdPartyNotices.cs
git commit -m "feat: add ThirdPartyNotices static data class"
```

---

### Task 2: LicenseKindToBrushConverter

**Files:**
- Create: `HandsLiftedApp.Core/Converters/LicenseKindToBrushConverter.cs`

**Interfaces:**
- Consumes: license string (e.g. `"GPL 2.0+"`, `"MIT"`, `"Proprietary"`)
- Produces: `LicenseKindToBrushConverter` — `IValueConverter` usable as `{StaticResource LicenseConverter}` in XAML

- [ ] **Step 1: Create the converter**

```csharp
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace HandsLiftedApp.Core.Converters;

public class LicenseKindToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var license = value as string ?? string.Empty;

        if (license.StartsWith("GPL", StringComparison.OrdinalIgnoreCase)
            || license.StartsWith("LGPL", StringComparison.OrdinalIgnoreCase)
            || license.StartsWith("CC BY-SA", StringComparison.OrdinalIgnoreCase))
            return new SolidColorBrush(Color.Parse("#d97706")); // amber — copyleft

        if (license.Equals("Proprietary", StringComparison.OrdinalIgnoreCase)
            || license.Equals("Commercial", StringComparison.OrdinalIgnoreCase))
            return new SolidColorBrush(Color.Parse("#dc2626")); // red — proprietary

        return new SolidColorBrush(Color.Parse("#6b7280")); // muted gray — permissive
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
```

- [ ] **Step 2: Build to confirm no compile errors**

```
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add HandsLiftedApp.Core/Converters/LicenseKindToBrushConverter.cs
git commit -m "feat: add LicenseKindToBrushConverter for notices window"
```

---

### Task 3: ThirdPartyNoticesWindow

**Files:**
- Create: `HandsLiftedApp.Core/Views/ThirdPartyNoticesWindow.axaml`
- Create: `HandsLiftedApp.Core/Views/ThirdPartyNoticesWindow.axaml.cs`

**Interfaces:**
- Consumes: `ThirdPartyNotices.All` (`IReadOnlyList<NoticeEntry>`) from Task 1
- Consumes: `LicenseKindToBrushConverter` from Task 2
- Produces: `ThirdPartyNoticesWindow` class — opened via `ShowDialog(owner)` in Task 4

- [ ] **Step 1: Create the AXAML file**

```xml
<Window
    x:Class="HandsLiftedApp.Core.Views.ThirdPartyNoticesWindow"
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:converters="clr-namespace:HandsLiftedApp.Core.Converters"
    Title="Open Source Notices"
    Width="700"
    Height="500"
    CanResize="True"
    ShowInTaskbar="False"
    WindowStartupLocation="CenterOwner"
    mc:Ignorable="d">

    <Window.Resources>
        <converters:LicenseKindToBrushConverter x:Key="LicenseConverter" />
    </Window.Resources>

    <DockPanel>
        <!--  header  -->
        <StackPanel DockPanel.Dock="Top" Margin="16,16,16,8">
            <TextBlock FontSize="18" FontWeight="Bold" Text="Open Source Notices" />
            <TextBlock
                Margin="0,4,0,0"
                FontSize="13"
                Foreground="#6b7280"
                Text="VisionScreens uses the following third-party software and assets." />
        </StackPanel>

        <!--  footer  -->
        <Grid DockPanel.Dock="Bottom" Margin="0,8,12,12">
            <Button
                x:Name="buttonClose"
                Width="120"
                Height="36"
                HorizontalAlignment="Right"
                Content="Close"
                IsCancel="True"
                IsDefault="True" />
        </Grid>

        <!--  body  -->
        <DataGrid
            x:Name="noticesGrid"
            Margin="16,0,16,0"
            AutoGenerateColumns="False"
            CanUserReorderColumns="False"
            CanUserResizeColumns="True"
            CanUserSortColumns="False"
            GridLinesVisibility="Horizontal"
            HeadersVisibility="Column"
            IsReadOnly="True">
            <DataGrid.Columns>

                <!--  Library  -->
                <DataGridTemplateColumn Header="Library" Width="220">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <TextBlock
                                Margin="4,0"
                                VerticalAlignment="Center"
                                FontSize="13"
                                FontWeight="SemiBold"
                                Text="{Binding Library}"
                                ToolTip.Tip="{Binding Url}" />
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>

                <!--  License  -->
                <DataGridTemplateColumn Header="License" Width="140">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <TextBlock
                                Margin="4,0"
                                VerticalAlignment="Center"
                                FontSize="11"
                                Foreground="{Binding License, Converter={StaticResource LicenseConverter}}"
                                Text="{Binding License}" />
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>

                <!--  Copyright  -->
                <DataGridTemplateColumn Header="Copyright" Width="*">
                    <DataGridTemplateColumn.CellTemplate>
                        <DataTemplate>
                            <TextBlock
                                Margin="4,0"
                                VerticalAlignment="Center"
                                FontSize="12"
                                Foreground="#6b7280"
                                Text="{Binding Copyright}"
                                TextWrapping="Wrap" />
                        </DataTemplate>
                    </DataGridTemplateColumn.CellTemplate>
                </DataGridTemplateColumn>

            </DataGrid.Columns>
        </DataGrid>
    </DockPanel>
</Window>
```

- [ ] **Step 2: Create the code-behind file**

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HandsLiftedApp.Core.Data;

namespace HandsLiftedApp.Core.Views;

public partial class ThirdPartyNoticesWindow : Window
{
    public ThirdPartyNoticesWindow()
    {
        InitializeComponent();

        var grid = this.FindControl<DataGrid>("noticesGrid");
        grid!.ItemsSource = ThirdPartyNotices.All;

        var buttonClose = this.FindControl<Button>("buttonClose");
        buttonClose!.Click += (_, _) => Close();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
```

- [ ] **Step 3: Build to confirm no compile errors**

```
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add HandsLiftedApp.Core/Views/ThirdPartyNoticesWindow.axaml
git add HandsLiftedApp.Core/Views/ThirdPartyNoticesWindow.axaml.cs
git commit -m "feat: add ThirdPartyNoticesWindow with DataGrid list"
```

---

### Task 4: Wire button in AboutWindow

**Files:**
- Modify: `HandsLiftedApp.Core/Views/AboutWindow.axaml` — add button to footer StackPanel
- Modify: `HandsLiftedApp.Core/Views/AboutWindow.axaml.cs` — wire click handler

**Interfaces:**
- Consumes: `ThirdPartyNoticesWindow` from Task 3

- [ ] **Step 1: Add button to AboutWindow.axaml**

In the footer `StackPanel` (the one containing "View diagnostic logs" and "Generate support info"), add a third button **before** the existing two buttons:

Find this block:
```xml
                <StackPanel Orientation="Horizontal">
                    <StackPanel.Styles>
                        <Style Selector="Button">
                            <Setter Property="Margin" Value="0 0 8 0" />
                        </Style>
                        <Style Selector="Button material|MaterialIcon">
                            <Setter Property="Margin" Value="1 0 0 0" />
                            <Setter Property="Height" Value="14" />
                        </Style>
                    </StackPanel.Styles>
                    <Button HorizontalAlignment="Left" Classes="link">
```

Add `x:Name="buttonNotices"` button as the first child inside the `StackPanel`, before the "View diagnostic logs" button:

```xml
                <StackPanel Orientation="Horizontal">
                    <StackPanel.Styles>
                        <Style Selector="Button">
                            <Setter Property="Margin" Value="0 0 8 0" />
                        </Style>
                        <Style Selector="Button material|MaterialIcon">
                            <Setter Property="Margin" Value="1 0 0 0" />
                            <Setter Property="Height" Value="14" />
                        </Style>
                    </StackPanel.Styles>
                    <Button x:Name="buttonNotices" HorizontalAlignment="Left" Classes="link">
                        <TextBlock Text="Open Source Notices" />
                    </Button>
                    <Button HorizontalAlignment="Left" Classes="link">
```

- [ ] **Step 2: Wire the click handler in AboutWindow.axaml.cs**

Add after the existing `buttonDone.Click` wire-up:

```csharp
        public AboutWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            var buttonDone = this.FindControl<Button>("buttonDone");
            buttonDone.Click += (o, e) => this.Close();

            var buttonNotices = this.FindControl<Button>("buttonNotices");
            buttonNotices!.Click += async (_, _) =>
                await new ThirdPartyNoticesWindow().ShowDialog(this);

            this.DataContext = this;
        }
```

Also add the using at the top of the file if not already present (it should be, since `ThirdPartyNoticesWindow` is in the same namespace):
```csharp
using HandsLiftedApp.Core.Views; // already the current namespace — no import needed
```

- [ ] **Step 3: Remove the OpenMoji attribution added inline to AboutWindow.axaml in an earlier commit**

The earlier commit added this WrapPanel to the footer:
```xml
                <WrapPanel>
                    <TextBlock Text="Icons by " />
                    <TextBlock FontWeight="SemiBold" Text="OpenMoji" />
                    <TextBlock Text=" — CC BY-SA 4.0 (openmoji.org)" />
                </WrapPanel>
```

Remove it — OpenMoji attribution is now covered in the notices window.

- [ ] **Step 4: Build the full solution**

```
dotnet build HandsLiftedApp.Core/HandsLiftedApp.Core.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Run the app and verify**

Launch the app. Open About window. Confirm:
- "Open Source Notices" button appears in footer
- Clicking it opens the notices window
- DataGrid shows all 30 entries in alphabetical order
- Library column is semi-bold
- License column: amber for GPL/LGPL/CC BY-SA, red for Proprietary/Commercial, gray for MIT/Apache/BSD
- Copyright column is muted gray
- Hovering a library name with a URL shows the URL in a tooltip
- Window is resizable; Close button dismisses it

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Views/AboutWindow.axaml
git add HandsLiftedApp.Core/Views/AboutWindow.axaml.cs
git commit -m "feat: add Open Source Notices button to About window"
```
