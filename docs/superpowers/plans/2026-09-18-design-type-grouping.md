# Design/Theme Type Grouping Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `Type` field (`General` / `SongTheme` / `ScriptureTheme`) to `BaseSlideTheme`, group the theme designer's list by it, filter the song/scripture theme pickers by it, and add a scripture preview to the theme designer.

**Architecture:** A new `SlideThemeType` enum and `Type` property on `BaseSlideTheme` (default `General`, so old playlists deserialize unchanged). Two extension-method predicates (`AppliesToSongs`/`AppliesToScripture`) centralize the "which type is valid where" rule. Filtering and grouping in the UI are both done via per-item `IsVisible` bindings on `ListBoxItem`/`ComboBoxItem` styles (driven by small dedicated `IValueConverter`s), rather than swapping `ItemsSource` to filtered snapshots — this keeps every list bound directly to the live `Playlist.Designs` `ObservableCollection`, so add/remove and live `Type` edits keep working exactly as they do today, with no extra collection-sync code. A new `ScriptureSlideView` (copied from the existing `SongSlideView` pattern) adds a third preview mode to the theme designer.

**Tech Stack:** Avalonia 11 (XAML + code-behind, no ViewModel for the designer view), ReactiveUI (`ReactiveObject`), MSTest (`HandsLiftedApp.Tests`, `[TestClass]`/`[TestMethod]`).

**Spec:** [docs/superpowers/specs/2026-09-18-design-type-grouping-design.md](../specs/2026-09-18-design-type-grouping-design.md)

## Global Constraints

- Old playlist XML with no `<Type>` element must deserialize to `SlideThemeType.General` — no migration code, rely on the enum's default value.
- All three theme-designer preview toggles (Song Lyric / Song Title / Scripture) are shown regardless of the selected design's `Type`.
- No native Avalonia `GroupStyle`/`ICollectionView` grouping — not used anywhere else in this codebase.
- Follow this repo's CLAUDE.md rule: any UI change must be clicked through in the running app before being considered done (see Task 7).

---

### Task 1: `SlideThemeType` enum + `BaseSlideTheme.Type` property

**Files:**
- Create: `HandsLiftedApp.Data/Models/SlideTheme/SlideThemeType.cs`
- Modify: `HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs:115-124` (insert new property after `Name`)
- Test: `HandsLiftedApp.Tests/Models/SlideTheme/BaseSlideThemeTypeTests.cs`

**Interfaces:**
- Produces: `HandsLiftedApp.Data.SlideTheme.SlideThemeType` enum with members `General = 0`, `SongTheme`, `ScriptureTheme`. `BaseSlideTheme.Type` (get/set, `[DataMember]`, `ReactiveObject`-backed, default `SlideThemeType.General`).

- [ ] **Step 1: Write the failing tests**

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Utils;
using HandsLiftedApp.Data.SlideTheme;
using System.IO;
using System.Text;

namespace HandsLiftedApp.Tests.Models.SlideTheme;

[TestClass]
public class BaseSlideThemeTypeTests
{
    [TestMethod]
    public void Type_DefaultsToGeneral()
    {
        var theme = new BaseSlideTheme();

        Assert.AreEqual(SlideThemeType.General, theme.Type);
    }

    [TestMethod]
    public void Type_CanBeSetToSongTheme()
    {
        var theme = new BaseSlideTheme { Type = SlideThemeType.SongTheme };

        Assert.AreEqual(SlideThemeType.SongTheme, theme.Type);
    }

    [TestMethod]
    public void TryDeserialize_XmlWithNoTypeElement_DefaultsToGeneral()
    {
        const string xml = "<?xml version=\"1.0\"?><BaseSlideTheme><Name>Old Theme</Name></BaseSlideTheme>";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        var ok = SlideThemeXmlSerializer.TryDeserialize(stream, out var theme);

        Assert.IsTrue(ok);
        Assert.IsNotNull(theme);
        Assert.AreEqual(SlideThemeType.General, theme!.Type);
        Assert.AreEqual("Old Theme", theme.Name);
    }

    [TestMethod]
    public void CopyFrom_CopiesType()
    {
        var source = new BaseSlideTheme { Type = SlideThemeType.ScriptureTheme };
        var target = new BaseSlideTheme();

        target.CopyFrom(source);

        Assert.AreEqual(SlideThemeType.ScriptureTheme, target.Type);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~BaseSlideThemeTypeTests"`
Expected: FAIL to compile — `SlideThemeType` and `BaseSlideTheme.Type` don't exist yet.

- [ ] **Step 3: Create the enum**

```csharp
using System;

namespace HandsLiftedApp.Data.SlideTheme
{
    [Serializable]
    public enum SlideThemeType
    {
        General = 0,
        SongTheme,
        ScriptureTheme,
    }
}
```

- [ ] **Step 4: Add the `Type` property to `BaseSlideTheme`**

In `HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs`, insert immediately after the existing `Name` property (after line 123, `}` closing the `Name` getter/setter):

```csharp
        private SlideThemeType _type = SlideThemeType.General;

        [DataMember]
        public SlideThemeType Type
        {
            get => _type;
            set => this.RaiseAndSetIfChanged(ref _type, value);
        }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~BaseSlideThemeTypeTests"`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Data/Models/SlideTheme/SlideThemeType.cs HandsLiftedApp.Data/Models/SlideTheme/BaseSlideTheme.cs HandsLiftedApp.Tests/Models/SlideTheme/BaseSlideThemeTypeTests.cs
git commit -m "feat: add Type field to BaseSlideTheme"
```

---

### Task 2: `AppliesToSongs`/`AppliesToScripture` extension methods

**Files:**
- Create: `HandsLiftedApp.Data/Models/SlideTheme/SlideThemeTypeExtensions.cs`
- Test: `HandsLiftedApp.Tests/Models/SlideTheme/SlideThemeTypeExtensionsTests.cs`

**Interfaces:**
- Consumes: `HandsLiftedApp.Data.SlideTheme.BaseSlideTheme.Type`, `SlideThemeType` (Task 1).
- Produces: `public static bool BaseSlideTheme.AppliesToSongs()`, `public static bool BaseSlideTheme.AppliesToScripture()` (extension methods on `BaseSlideTheme`, namespace `HandsLiftedApp.Data.SlideTheme`).

- [ ] **Step 1: Write the failing tests**

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Tests.Models.SlideTheme;

[TestClass]
public class SlideThemeTypeExtensionsTests
{
    [DataTestMethod]
    [DataRow(SlideThemeType.General, true)]
    [DataRow(SlideThemeType.SongTheme, true)]
    [DataRow(SlideThemeType.ScriptureTheme, false)]
    public void AppliesToSongs_MatchesGeneralAndSongTheme(SlideThemeType type, bool expected)
    {
        var theme = new BaseSlideTheme { Type = type };

        Assert.AreEqual(expected, theme.AppliesToSongs());
    }

    [DataTestMethod]
    [DataRow(SlideThemeType.General, true)]
    [DataRow(SlideThemeType.SongTheme, false)]
    [DataRow(SlideThemeType.ScriptureTheme, true)]
    public void AppliesToScripture_MatchesGeneralAndScriptureTheme(SlideThemeType type, bool expected)
    {
        var theme = new BaseSlideTheme { Type = type };

        Assert.AreEqual(expected, theme.AppliesToScripture());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SlideThemeTypeExtensionsTests"`
Expected: FAIL to compile — `AppliesToSongs`/`AppliesToScripture` don't exist yet.

- [ ] **Step 3: Implement the extension methods**

```csharp
namespace HandsLiftedApp.Data.SlideTheme
{
    public static class SlideThemeTypeExtensions
    {
        public static bool AppliesToSongs(this BaseSlideTheme design) =>
            design.Type is SlideThemeType.General or SlideThemeType.SongTheme;

        public static bool AppliesToScripture(this BaseSlideTheme design) =>
            design.Type is SlideThemeType.General or SlideThemeType.ScriptureTheme;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~SlideThemeTypeExtensionsTests"`
Expected: PASS (6 tests)

- [ ] **Step 5: Commit**

```bash
git add HandsLiftedApp.Data/Models/SlideTheme/SlideThemeTypeExtensions.cs HandsLiftedApp.Tests/Models/SlideTheme/SlideThemeTypeExtensionsTests.cs
git commit -m "feat: add AppliesToSongs/AppliesToScripture theme-type predicates"
```

---

### Task 3: Visibility converters for grouping and filtering

**Files:**
- Create: `HandsLiftedApp.Core/Converters/IsGeneralThemeTypeConverter.cs`
- Create: `HandsLiftedApp.Core/Converters/IsSongThemeTypeConverter.cs`
- Create: `HandsLiftedApp.Core/Converters/IsScriptureThemeTypeConverter.cs`
- Create: `HandsLiftedApp.Core/Converters/AppliesToSongsVisibilityConverter.cs`
- Create: `HandsLiftedApp.Core/Converters/AppliesToScriptureVisibilityConverter.cs`
- Test: `HandsLiftedApp.Tests/Converters/ThemeTypeConvertersTests.cs`

**Interfaces:**
- Consumes: `SlideThemeType` (Task 1), `AppliesToSongs()`/`AppliesToScripture()` (Task 2).
- Produces: five `Avalonia.Data.Converters.IValueConverter` classes in namespace `HandsLiftedApp.Core.Converters`.
  - `IsGeneralThemeTypeConverter`, `IsSongThemeTypeConverter`, `IsScriptureThemeTypeConverter`: `Convert(object? value, ...)` expects `value` to be a `SlideThemeType` (bind to `Type`), returns `bool`.
  - `AppliesToSongsVisibilityConverter`, `AppliesToScriptureVisibilityConverter`: `Convert(object? value, ...)` expects `value` to be a `BaseSlideTheme` **or null** (bind to `.`, the whole item — null must return `true`, used for the scripture picker's blank/"use default" placeholder entry), returns `bool`.

- [ ] **Step 1: Write the failing tests**

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Converters;
using HandsLiftedApp.Data.SlideTheme;
using System.Globalization;

namespace HandsLiftedApp.Tests.Converters;

[TestClass]
public class ThemeTypeConvertersTests
{
    [DataTestMethod]
    [DataRow(SlideThemeType.General, true)]
    [DataRow(SlideThemeType.SongTheme, false)]
    [DataRow(SlideThemeType.ScriptureTheme, false)]
    public void IsGeneralThemeTypeConverter_MatchesOnlyGeneral(SlideThemeType type, bool expected)
    {
        var converter = new IsGeneralThemeTypeConverter();

        var result = converter.Convert(type, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow(SlideThemeType.General, false)]
    [DataRow(SlideThemeType.SongTheme, true)]
    [DataRow(SlideThemeType.ScriptureTheme, false)]
    public void IsSongThemeTypeConverter_MatchesOnlySongTheme(SlideThemeType type, bool expected)
    {
        var converter = new IsSongThemeTypeConverter();

        var result = converter.Convert(type, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, result);
    }

    [DataTestMethod]
    [DataRow(SlideThemeType.General, false)]
    [DataRow(SlideThemeType.SongTheme, false)]
    [DataRow(SlideThemeType.ScriptureTheme, true)]
    public void IsScriptureThemeTypeConverter_MatchesOnlyScriptureTheme(SlideThemeType type, bool expected)
    {
        var converter = new IsScriptureThemeTypeConverter();

        var result = converter.Convert(type, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void AppliesToSongsVisibilityConverter_NullIsAlwaysVisible()
    {
        var converter = new AppliesToSongsVisibilityConverter();

        var result = converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(true, result);
    }

    [DataTestMethod]
    [DataRow(SlideThemeType.General, true)]
    [DataRow(SlideThemeType.SongTheme, true)]
    [DataRow(SlideThemeType.ScriptureTheme, false)]
    public void AppliesToSongsVisibilityConverter_FiltersByType(SlideThemeType type, bool expected)
    {
        var converter = new AppliesToSongsVisibilityConverter();
        var theme = new BaseSlideTheme { Type = type };

        var result = converter.Convert(theme, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void AppliesToScriptureVisibilityConverter_NullIsAlwaysVisible()
    {
        var converter = new AppliesToScriptureVisibilityConverter();

        var result = converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(true, result);
    }

    [DataTestMethod]
    [DataRow(SlideThemeType.General, true)]
    [DataRow(SlideThemeType.SongTheme, false)]
    [DataRow(SlideThemeType.ScriptureTheme, true)]
    public void AppliesToScriptureVisibilityConverter_FiltersByType(SlideThemeType type, bool expected)
    {
        var converter = new AppliesToScriptureVisibilityConverter();
        var theme = new BaseSlideTheme { Type = type };

        var result = converter.Convert(theme, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, result);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~ThemeTypeConvertersTests"`
Expected: FAIL to compile — none of the five converter classes exist yet.

- [ ] **Step 3: Implement the three exact-type converters**

`HandsLiftedApp.Core/Converters/IsGeneralThemeTypeConverter.cs`:
```csharp
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    public class IsGeneralThemeTypeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is SlideThemeType type && type == SlideThemeType.General;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
```

`HandsLiftedApp.Core/Converters/IsSongThemeTypeConverter.cs`:
```csharp
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    public class IsSongThemeTypeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is SlideThemeType type && type == SlideThemeType.SongTheme;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
```

`HandsLiftedApp.Core/Converters/IsScriptureThemeTypeConverter.cs`:
```csharp
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    public class IsScriptureThemeTypeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is SlideThemeType type && type == SlideThemeType.ScriptureTheme;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
```

- [ ] **Step 4: Implement the two "applies to" visibility converters**

`HandsLiftedApp.Core/Converters/AppliesToSongsVisibilityConverter.cs`:
```csharp
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    public class AppliesToSongsVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is not BaseSlideTheme theme || theme.AppliesToSongs();

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
```

`HandsLiftedApp.Core/Converters/AppliesToScriptureVisibilityConverter.cs`:
```csharp
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using HandsLiftedApp.Data.SlideTheme;

namespace HandsLiftedApp.Core.Converters
{
    public class AppliesToScriptureVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is not BaseSlideTheme theme || theme.AppliesToScripture();

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test HandsLiftedApp.Tests --filter "FullyQualifiedName~ThemeTypeConvertersTests"`
Expected: PASS (14 tests)

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Converters/IsGeneralThemeTypeConverter.cs HandsLiftedApp.Core/Converters/IsSongThemeTypeConverter.cs HandsLiftedApp.Core/Converters/IsScriptureThemeTypeConverter.cs HandsLiftedApp.Core/Converters/AppliesToSongsVisibilityConverter.cs HandsLiftedApp.Core/Converters/AppliesToScriptureVisibilityConverter.cs HandsLiftedApp.Tests/Converters/ThemeTypeConvertersTests.cs
git commit -m "feat: add theme-type visibility converters"
```

---

### Task 4: Filter the song/scripture theme pickers

**Files:**
- Modify: `HandsLiftedApp.Core/Views/ItemEditDock/ItemEditDockRoot.axaml:17-19` (resources), `:74-83` (song ComboBox), `:162-171` (scripture ComboBox)
- Modify: `HandsLiftedApp.Core/Views/Editors/Song/SongEditorControl.axaml:1-23` (xmlns + resources), `:86-97` (Design tab ComboBox)

**Interfaces:**
- Consumes: `AppliesToSongsVisibilityConverter`, `AppliesToScriptureVisibilityConverter` (Task 3).

No new C# — this task is pure XAML wiring, verified live in Task 7 (per this repo's CLAUDE.md rule that UI changes need a click-through, not just a build).

- [ ] **Step 1: Register the new converters as resources in `ItemEditDockRoot.axaml`**

In `HandsLiftedApp.Core/Views/ItemEditDock/ItemEditDockRoot.axaml`, the `converters` xmlns prefix (line 10) already points at `HandsLiftedApp.Core.Converters`, so just extend the existing resources block (lines 17-19):

```xml
    <UserControl.Resources>
        <converters:PrependBlankThemeOptionConverter x:Key="PrependBlankThemeOptionConverter" />
        <converters:AppliesToSongsVisibilityConverter x:Key="AppliesToSongsVisibilityConverter" />
        <converters:AppliesToScriptureVisibilityConverter x:Key="AppliesToScriptureVisibilityConverter" />
    </UserControl.Resources>
```

- [ ] **Step 2: Filter the song item's theme ComboBox**

Replace (lines 74-83):
```xml
                                                    <ComboBox
                                                        HorizontalAlignment="Stretch"
                                                        ItemsSource="{Binding ParentPlaylist.Designs}"
                                                        SelectedItem="{Binding ResolvedDesignTheme}">
                                                        <ComboBox.ItemTemplate>
                                                            <DataTemplate>
                                                                <TextBlock Text="{Binding Name}" />
                                                            </DataTemplate>
                                                        </ComboBox.ItemTemplate>
                                                    </ComboBox>
```
with:
```xml
                                                    <ComboBox
                                                        HorizontalAlignment="Stretch"
                                                        ItemsSource="{Binding ParentPlaylist.Designs}"
                                                        SelectedItem="{Binding ResolvedDesignTheme}">
                                                        <ComboBox.Styles>
                                                            <Style Selector="ComboBoxItem">
                                                                <Setter Property="IsVisible"
                                                                        Value="{Binding Converter={StaticResource AppliesToSongsVisibilityConverter}}" />
                                                            </Style>
                                                        </ComboBox.Styles>
                                                        <ComboBox.ItemTemplate>
                                                            <DataTemplate>
                                                                <TextBlock Text="{Binding Name}" />
                                                            </DataTemplate>
                                                        </ComboBox.ItemTemplate>
                                                    </ComboBox>
```

- [ ] **Step 3: Filter the scripture item's theme ComboBox**

Replace (lines 162-171):
```xml
                                                    <ComboBox
                                                        HorizontalAlignment="Stretch"
                                                        ItemsSource="{Binding ParentPlaylist.Designs, Converter={StaticResource PrependBlankThemeOptionConverter}}"
                                                        SelectedItem="{Binding ExplicitDesignTheme}">
                                                        <ComboBox.ItemTemplate>
                                                            <DataTemplate>
                                                                <TextBlock Text="{Binding Name, FallbackValue=''}" />
                                                            </DataTemplate>
                                                        </ComboBox.ItemTemplate>
                                                    </ComboBox>
```
with:
```xml
                                                    <ComboBox
                                                        HorizontalAlignment="Stretch"
                                                        ItemsSource="{Binding ParentPlaylist.Designs, Converter={StaticResource PrependBlankThemeOptionConverter}}"
                                                        SelectedItem="{Binding ExplicitDesignTheme}">
                                                        <ComboBox.Styles>
                                                            <Style Selector="ComboBoxItem">
                                                                <Setter Property="IsVisible"
                                                                        Value="{Binding Converter={StaticResource AppliesToScriptureVisibilityConverter}}" />
                                                            </Style>
                                                        </ComboBox.Styles>
                                                        <ComboBox.ItemTemplate>
                                                            <DataTemplate>
                                                                <TextBlock Text="{Binding Name, FallbackValue=''}" />
                                                            </DataTemplate>
                                                        </ComboBox.ItemTemplate>
                                                    </ComboBox>
```

The blank/"use default" entry is `null`, and both new converters return `true` for a non-`BaseSlideTheme` value, so it stays visible.

- [ ] **Step 4: Filter the Song editor's "Design" tab ComboBox**

In `HandsLiftedApp.Core/Views/Editors/Song/SongEditorControl.axaml`, add a new xmlns prefix next to the existing ones (after line 11, `xmlns:controls=...`):

```xml
    xmlns:coreConverters="clr-namespace:HandsLiftedApp.Core.Converters"
```

Extend the resources block (lines 19-23):
```xml
    <UserControl.Resources>
        <ResourceDictionary>
            <converters:PercentageScaleConverter x:Key="PercentageScaleConverter" />
            <coreConverters:AppliesToSongsVisibilityConverter x:Key="AppliesToSongsVisibilityConverter" />
        </ResourceDictionary>
    </UserControl.Resources>
```

Replace the Design tab ComboBox (lines 88-97):
```xml
                        <ComboBox
                            HorizontalAlignment="Stretch"
                            ItemsSource="{Binding Playlist.Designs}"
                            SelectedItem="{Binding SelectedSlideTheme}">
                            <ComboBox.ItemTemplate>
                                <DataTemplate>
                                    <TextBlock Text="{Binding Name}" />
                                </DataTemplate>
                            </ComboBox.ItemTemplate>
                        </ComboBox>
```
with:
```xml
                        <ComboBox
                            HorizontalAlignment="Stretch"
                            ItemsSource="{Binding Playlist.Designs}"
                            SelectedItem="{Binding SelectedSlideTheme}">
                            <ComboBox.Styles>
                                <Style Selector="ComboBoxItem">
                                    <Setter Property="IsVisible"
                                            Value="{Binding Converter={StaticResource AppliesToSongsVisibilityConverter}}" />
                                </Style>
                            </ComboBox.Styles>
                            <ComboBox.ItemTemplate>
                                <DataTemplate>
                                    <TextBlock Text="{Binding Name}" />
                                </DataTemplate>
                            </ComboBox.ItemTemplate>
                        </ComboBox>
```

- [ ] **Step 5: Build to confirm no XAML compile errors**

Run: `dotnet build HandsLiftedApp.Core`
Expected: builds with no errors (Avalonia compiles XAML at build time here since these two files use `x:CompileBindings="False"`/default — a typo in a `StaticResource` key or converter name will surface as a runtime `KeyNotFoundException` the first time the flyout opens, not a build error, so Task 7's manual click-through is what actually proves this step).

- [ ] **Step 6: Commit**

```bash
git add HandsLiftedApp.Core/Views/ItemEditDock/ItemEditDockRoot.axaml HandsLiftedApp.Core/Views/Editors/Song/SongEditorControl.axaml
git commit -m "feat: filter song/scripture theme pickers by design type"
```

---

### Task 5: Group the SlideThemeDesigner list by type and add a Type editor field

**Files:**
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml:18-27` (resources), `:36-153` (list + add button)
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml` (Common section of `themeEditorPanel`, around line 199-201, to add the Type field)
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs` (constructor, `SyncEditorToSelection`, every handler currently reading `designsListBox.SelectedItem`/`SelectedIndex`)

**Interfaces:**
- Consumes: `IsGeneralThemeTypeConverter`, `IsSongThemeTypeConverter`, `IsScriptureThemeTypeConverter` (Task 3), `SlideThemeType` (Task 1).
- Produces: `SlideThemeDesigner.SelectedDesign` (private `BaseSlideTheme?` property later tasks/steps in this same task rely on), `SlideThemeDesigner.SelectDesign(BaseSlideTheme)` (private helper).

This task is XAML/code-behind only — verified live in Task 7, matching how the rest of this designer view has always been tested (no ViewModel, no unit tests for it today).

- [ ] **Step 1: Register the three grouping converters as resources**

In `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml`, extend the existing resources block (lines 18-27, `converters2` prefix already points at `HandsLiftedApp.Core.Converters`):

```xml
        <converters2:IsDefaultScriptureThemeConverter x:Key="IsDefaultScriptureThemeConverter" />
        <converters2:IsGeneralThemeTypeConverter x:Key="IsGeneralThemeTypeConverter" />
        <converters2:IsSongThemeTypeConverter x:Key="IsSongThemeTypeConverter" />
        <converters2:IsScriptureThemeTypeConverter x:Key="IsScriptureThemeTypeConverter" />
```

- [ ] **Step 2: Replace the single flat ListBox with three grouped, filtered ListBoxes**

Replace the entire left `DockPanel` (lines 36-153):

```xml
        <DockPanel DockPanel.Dock="Left">
            <Button DockPanel.Dock="Bottom" Click="AddItem_OnClick">Add new theme</Button>
            <ListBox
                SelectedIndex="0"
                MinWidth="80"
                Name="designsListBox"
                ItemsSource="{Binding Playlist.Designs}">
                <ListBox.ContextMenu>
                    <ContextMenu>
                        <MenuItem Header="Import" Click="ImportItem_OnClick">
                            <MenuItem.Icon>
                                <avalonia:MaterialIcon Foreground="#888888" Kind="FolderOpen" />
                            </MenuItem.Icon>
                        </MenuItem>
                    </ContextMenu>
                </ListBox.ContextMenu>
                <ListBox.Styles>
                    <Style Selector="ListBoxItem">
                        <Setter Property="ContextMenu">
                            <ContextMenu>
                                <MenuItem Header="Export" Click="ExportItem_OnClick">
                                    <MenuItem.Icon>
                                        <avalonia:MaterialIcon Foreground="#888888" Kind="ContentSave" />
                                    </MenuItem.Icon>
                                </MenuItem>
                                <MenuItem Header="Duplicate" Click="DuplicateItem_OnClick">
                                    <MenuItem.Icon>
                                        <avalonia:MaterialIcon Foreground="#888888" Kind="ContentCopy" />
                                    </MenuItem.Icon>
                                </MenuItem>
                                <MenuItem Header="Remove" Click="RemoveItem_OnClick">
                                    <MenuItem.Icon>
                                        <avalonia:MaterialIcon Foreground="#888888" Kind="Times" />
                                    </MenuItem.Icon>
                                </MenuItem>
                                <MenuItem Header="Set as default for Songs" Click="SetDefaultSongTheme_OnClick">
                                    <MenuItem.Icon>
                                        <avalonia:MaterialIcon Foreground="#888888" Kind="Star" />
                                    </MenuItem.Icon>
                                </MenuItem>
                                <MenuItem Header="Set as default for Songs (motion bg)" Click="SetDefaultSongMotionTheme_OnClick">
                                    <MenuItem.Icon>
                                        <avalonia:MaterialIcon Foreground="#888888" Kind="Star" />
                                    </MenuItem.Icon>
                                </MenuItem>
                                <MenuItem Header="Set as default for Scripture" Click="SetDefaultScriptureTheme_OnClick">
                                    <MenuItem.Icon>
                                        <avalonia:MaterialIcon Foreground="#888888" Kind="Star" />
                                    </MenuItem.Icon>
                                </MenuItem>
                            </ContextMenu>
                        </Setter>
                    </Style>
                </ListBox.Styles>
                <ListBox.ItemTemplate>
                    <DataTemplate>
                        <DockPanel>
                            <TextBlock Text="{Binding Name}" DockPanel.Dock="Bottom" HorizontalAlignment="Center"
                                       Margin="0 6 0 0" />
                            <TextBlock Text="default"
                                       DockPanel.Dock="Bottom"
                                       HorizontalAlignment="Center"
                                       FontSize="10"
                                       Foreground="#888888"
                                       IsVisible="{Binding ., Converter={StaticResource IsDefaultThemeConverter}}" />
                            <TextBlock Text="default (songs)"
                                       DockPanel.Dock="Bottom"
                                       HorizontalAlignment="Center"
                                       FontSize="10"
                                       Foreground="#888888">
                                <TextBlock.IsVisible>
                                    <MultiBinding Converter="{StaticResource IsDefaultSongThemeConverter}">
                                        <Binding Path="." />
                                        <Binding Path="$parent[UserControl].DataContext.Playlist.DefaultSongThemeId" />
                                    </MultiBinding>
                                </TextBlock.IsVisible>
                            </TextBlock>
                            <TextBlock Text="default (songs, motion)"
                                       DockPanel.Dock="Bottom"
                                       HorizontalAlignment="Center"
                                       FontSize="10"
                                       Foreground="#888888">
                                <TextBlock.IsVisible>
                                    <MultiBinding Converter="{StaticResource IsDefaultSongMotionThemeConverter}">
                                        <Binding Path="." />
                                        <Binding Path="$parent[UserControl].DataContext.Playlist.DefaultSongMotionThemeId" />
                                    </MultiBinding>
                                </TextBlock.IsVisible>
                            </TextBlock>
                            <TextBlock Text="default (scripture)"
                                       DockPanel.Dock="Bottom"
                                       HorizontalAlignment="Center"
                                       FontSize="10"
                                       Foreground="#888888">
                                <TextBlock.IsVisible>
                                    <MultiBinding Converter="{StaticResource IsDefaultScriptureThemeConverter}">
                                        <Binding Path="." />
                                        <Binding Path="$parent[UserControl].DataContext.Playlist.DefaultScriptureThemeId" />
                                    </MultiBinding>
                                </TextBlock.IsVisible>
                            </TextBlock>
                        </DockPanel>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>
        </DockPanel>
```

with:

```xml
        <DockPanel DockPanel.Dock="Left">
            <UniformGrid DockPanel.Dock="Bottom" Columns="3" Rows="1">
                <Button Tag="General" Click="AddItem_OnClick">+ General</Button>
                <Button Tag="SongTheme" Click="AddItem_OnClick">+ Song</Button>
                <Button Tag="ScriptureTheme" Click="AddItem_OnClick">+ Scripture</Button>
            </UniformGrid>
            <ScrollViewer>
                <StackPanel MinWidth="80">
                    <TextBlock Text="General" Classes="SectionHeader" Margin="4 4 4 0" />
                    <ListBox Name="generalDesignsListBox"
                             ItemsSource="{Binding Playlist.Designs}"
                             ItemTemplate="{StaticResource DesignListItemTemplate}"
                             SelectionChanged="DesignsListBox_OnSelectionChanged">
                        <ListBox.ContextMenu>
                            <ContextMenu>
                                <MenuItem Header="Import" Click="ImportItem_OnClick">
                                    <MenuItem.Icon>
                                        <avalonia:MaterialIcon Foreground="#888888" Kind="FolderOpen" />
                                    </MenuItem.Icon>
                                </MenuItem>
                            </ContextMenu>
                        </ListBox.ContextMenu>
                        <ListBox.Styles>
                            <Style Selector="ListBoxItem">
                                <Setter Property="IsVisible" Value="{Binding Type, Converter={StaticResource IsGeneralThemeTypeConverter}}" />
                                <Setter Property="ContextMenu" Value="{StaticResource DesignListItemContextMenu}" />
                            </Style>
                        </ListBox.Styles>
                    </ListBox>

                    <TextBlock Text="Song Themes" Classes="SectionHeader" Margin="4 10 4 0" />
                    <ListBox Name="songDesignsListBox"
                             ItemsSource="{Binding Playlist.Designs}"
                             ItemTemplate="{StaticResource DesignListItemTemplate}"
                             SelectionChanged="DesignsListBox_OnSelectionChanged">
                        <ListBox.ContextMenu>
                            <ContextMenu>
                                <MenuItem Header="Import" Click="ImportItem_OnClick">
                                    <MenuItem.Icon>
                                        <avalonia:MaterialIcon Foreground="#888888" Kind="FolderOpen" />
                                    </MenuItem.Icon>
                                </MenuItem>
                            </ContextMenu>
                        </ListBox.ContextMenu>
                        <ListBox.Styles>
                            <Style Selector="ListBoxItem">
                                <Setter Property="IsVisible" Value="{Binding Type, Converter={StaticResource IsSongThemeTypeConverter}}" />
                                <Setter Property="ContextMenu" Value="{StaticResource DesignListItemContextMenu}" />
                            </Style>
                        </ListBox.Styles>
                    </ListBox>

                    <TextBlock Text="Scripture Themes" Classes="SectionHeader" Margin="4 10 4 0" />
                    <ListBox Name="scriptureDesignsListBox"
                             ItemsSource="{Binding Playlist.Designs}"
                             ItemTemplate="{StaticResource DesignListItemTemplate}"
                             SelectionChanged="DesignsListBox_OnSelectionChanged">
                        <ListBox.ContextMenu>
                            <ContextMenu>
                                <MenuItem Header="Import" Click="ImportItem_OnClick">
                                    <MenuItem.Icon>
                                        <avalonia:MaterialIcon Foreground="#888888" Kind="FolderOpen" />
                                    </MenuItem.Icon>
                                </MenuItem>
                            </ContextMenu>
                        </ListBox.ContextMenu>
                        <ListBox.Styles>
                            <Style Selector="ListBoxItem">
                                <Setter Property="IsVisible" Value="{Binding Type, Converter={StaticResource IsScriptureThemeTypeConverter}}" />
                                <Setter Property="ContextMenu" Value="{StaticResource DesignListItemContextMenu}" />
                            </Style>
                        </ListBox.Styles>
                    </ListBox>
                </StackPanel>
            </ScrollViewer>
        </DockPanel>
```

Each of the three `ListBox`es is bound directly to the same live `Playlist.Designs` `ObservableCollection` (not a filtered copy), so add/remove and `Type` edits stay live everywhere exactly as they did with the single flat list before this change — only the per-`ListBoxItem` `IsVisible` setter differs between the three (`IsGeneralThemeTypeConverter` / `IsSongThemeTypeConverter` / `IsScriptureThemeTypeConverter`, from Task 3). The `ContextMenu` and `ItemTemplate` are shared (identical for all three groups), so pull them into two resources instead of writing the same markup three times over. Add both to `UserControl.Resources` (same place as Step 1's converters):

```xml
        <ContextMenu x:Key="DesignListItemContextMenu">
            <MenuItem Header="Export" Click="ExportItem_OnClick">
                <MenuItem.Icon>
                    <avalonia:MaterialIcon Foreground="#888888" Kind="ContentSave" />
                </MenuItem.Icon>
            </MenuItem>
            <MenuItem Header="Duplicate" Click="DuplicateItem_OnClick">
                <MenuItem.Icon>
                    <avalonia:MaterialIcon Foreground="#888888" Kind="ContentCopy" />
                </MenuItem.Icon>
            </MenuItem>
            <MenuItem Header="Remove" Click="RemoveItem_OnClick">
                <MenuItem.Icon>
                    <avalonia:MaterialIcon Foreground="#888888" Kind="Times" />
                </MenuItem.Icon>
            </MenuItem>
            <MenuItem Header="Set as default for Songs" Click="SetDefaultSongTheme_OnClick">
                <MenuItem.Icon>
                    <avalonia:MaterialIcon Foreground="#888888" Kind="Star" />
                </MenuItem.Icon>
            </MenuItem>
            <MenuItem Header="Set as default for Songs (motion bg)" Click="SetDefaultSongMotionTheme_OnClick">
                <MenuItem.Icon>
                    <avalonia:MaterialIcon Foreground="#888888" Kind="Star" />
                </MenuItem.Icon>
            </MenuItem>
            <MenuItem Header="Set as default for Scripture" Click="SetDefaultScriptureTheme_OnClick">
                <MenuItem.Icon>
                    <avalonia:MaterialIcon Foreground="#888888" Kind="Star" />
                </MenuItem.Icon>
            </MenuItem>
        </ContextMenu>
        <DataTemplate x:Key="DesignListItemTemplate">
            <DockPanel>
                <TextBlock Text="{Binding Name}" DockPanel.Dock="Bottom" HorizontalAlignment="Center"
                           Margin="0 6 0 0" />
                <TextBlock Text="default"
                           DockPanel.Dock="Bottom"
                           HorizontalAlignment="Center"
                           FontSize="10"
                           Foreground="#888888"
                           IsVisible="{Binding ., Converter={StaticResource IsDefaultThemeConverter}}" />
                <TextBlock Text="default (songs)"
                           DockPanel.Dock="Bottom"
                           HorizontalAlignment="Center"
                           FontSize="10"
                           Foreground="#888888">
                    <TextBlock.IsVisible>
                        <MultiBinding Converter="{StaticResource IsDefaultSongThemeConverter}">
                            <Binding Path="." />
                            <Binding Path="$parent[UserControl].DataContext.Playlist.DefaultSongThemeId" />
                        </MultiBinding>
                    </TextBlock.IsVisible>
                </TextBlock>
                <TextBlock Text="default (songs, motion)"
                           DockPanel.Dock="Bottom"
                           HorizontalAlignment="Center"
                           FontSize="10"
                           Foreground="#888888">
                    <TextBlock.IsVisible>
                        <MultiBinding Converter="{StaticResource IsDefaultSongMotionThemeConverter}">
                            <Binding Path="." />
                            <Binding Path="$parent[UserControl].DataContext.Playlist.DefaultSongMotionThemeId" />
                        </MultiBinding>
                    </TextBlock.IsVisible>
                </TextBlock>
                <TextBlock Text="default (scripture)"
                           DockPanel.Dock="Bottom"
                           HorizontalAlignment="Center"
                           FontSize="10"
                           Foreground="#888888">
                    <TextBlock.IsVisible>
                        <MultiBinding Converter="{StaticResource IsDefaultScriptureThemeConverter}">
                            <Binding Path="." />
                            <Binding Path="$parent[UserControl].DataContext.Playlist.DefaultScriptureThemeId" />
                        </MultiBinding>
                    </TextBlock.IsVisible>
                </TextBlock>
            </DockPanel>
        </DataTemplate>
```

Note the `ContextMenu` is set twice in the snippet above (once via `<ListBox.ContextMenu>` for the `ListBox` itself, right-clicking empty space — this still only has the `Import` item, unchanged from the original single-list version — and once via the `ListBoxItem` style `Setter` for right-clicking an actual row, reusing the shared `DesignListItemContextMenu` resource). Both are needed; they are two different controls' context menus, exactly as in the original file (the original also set `ListBox.ContextMenu` for `Import` and a `ListBoxItem` style `Setter` for the per-row menu).

- [ ] **Step 3: Add the "Applies To" type field to the editor panel**

In the "Common" section of `themeEditorPanel`, immediately after the `Name` `TextBox` (after line 200, `<TextBox Watermark="" Text="{Binding Name}" />`), add:

```xml
                    <TextBlock Text="Applies To" Margin="0 6 0 0" />
                    <controls:ComboBoxWithoutWheelScroll x:Name="themeTypeComboBox"
                                                         HorizontalAlignment="Stretch"
                                                         SelectedItem="{Binding Type}" />
```

- [ ] **Step 4: Update the code-behind — selection model and constructor**

In `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs`, replace the constructor body's `designsListBox`-specific wiring (lines 65-75):

```csharp
            this.WhenAnyValue(v => v.designsListBox.ItemsSource)
                .Subscribe((x) =>
                {
                    if (designsListBox.SelectedIndex == -1)
                        designsListBox.SelectedIndex = 0;
                    SyncEditorToSelection();
                });

            designsListBox.SelectionChanged += (sender, args) => SyncEditorToSelection();

            designsListBox.DataContextChanged += (sender, args) => SyncEditorToSelection();
```

with:

```csharp
            themeTypeComboBox.ItemsSource = Enum.GetValues<SlideThemeType>();

            this.WhenAnyValue(v => v.generalDesignsListBox.ItemsSource)
                .Subscribe((x) =>
                {
                    if (SelectedDesign == null)
                    {
                        var first = (generalDesignsListBox.ItemsSource as System.Collections.IEnumerable)?
                            .Cast<BaseSlideTheme>().FirstOrDefault();
                        if (first != null)
                            SelectDesign(first);
                    }
                    SyncEditorToSelection();
                });

            generalDesignsListBox.DataContextChanged += (sender, args) => SyncEditorToSelection();
```

Add `using System.Collections.Generic;` is already present; add `using` for `HandsLiftedApp.Data.SlideTheme` is already present (line 10). Add a `using System.Linq;` (already present, line 17).

- [ ] **Step 5: Add the selection-coordination helpers**

Add these private members to the `SlideThemeDesigner` class (near the top, after `_fontWeightCache`):

```csharp
        private bool _suppressSelectionSync;

        private BaseSlideTheme? SelectedDesign =>
            generalDesignsListBox.SelectedItem as BaseSlideTheme
            ?? songDesignsListBox.SelectedItem as BaseSlideTheme
            ?? scriptureDesignsListBox.SelectedItem as BaseSlideTheme;

        private ListBox ListBoxFor(SlideThemeType type) => type switch
        {
            SlideThemeType.SongTheme => songDesignsListBox,
            SlideThemeType.ScriptureTheme => scriptureDesignsListBox,
            _ => generalDesignsListBox,
        };

        private void SelectDesign(BaseSlideTheme design)
        {
            _suppressSelectionSync = true;
            try
            {
                generalDesignsListBox.SelectedItem = null;
                songDesignsListBox.SelectedItem = null;
                scriptureDesignsListBox.SelectedItem = null;
                ListBoxFor(design.Type).SelectedItem = design;
            }
            finally
            {
                _suppressSelectionSync = false;
            }
            SyncEditorToSelection();
        }

        private void DesignsListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionSync) return;
            if (sender is not ListBox changedListBox || changedListBox.SelectedItem is not BaseSlideTheme)
                return;

            _suppressSelectionSync = true;
            try
            {
                if (changedListBox != generalDesignsListBox) generalDesignsListBox.SelectedItem = null;
                if (changedListBox != songDesignsListBox) songDesignsListBox.SelectedItem = null;
                if (changedListBox != scriptureDesignsListBox) scriptureDesignsListBox.SelectedItem = null;
            }
            finally
            {
                _suppressSelectionSync = false;
            }
            SyncEditorToSelection();
        }
```

`_suppressSelectionSync` exists because clearing the other two `ListBox.SelectedItem`s inside a `SelectionChanged` handler would otherwise re-enter this same handler (with `SelectedItem == null`, which the early-return guard already skips — but the guard also avoids redundant `SyncEditorToSelection()` calls firing three times for one click).

- [ ] **Step 6: Update `SyncEditorToSelection` and every other handler to use `SelectedDesign`/`SelectDesign`**

Replace `var item = designsListBox.SelectedItem as BaseSlideTheme;` in `SyncEditorToSelection` (line 87) with `var item = SelectedDesign;`.

In `RemoveItem_OnClick` (lines 213-242), replace the `else if` branch:
```csharp
                        else if (mainViewModel.Playlist.Designs.Count > 1)
                        {
                            designsListBox.SelectedIndex = 0;
                            mainViewModel.Playlist.Designs.Remove(item);
                        }
```
with:
```csharp
                        else if (mainViewModel.Playlist.Designs.Count > 1)
                        {
                            var remainingDesigns = mainViewModel.Playlist.Designs.Where(d => d.Id != item.Id).ToList();
                            mainViewModel.Playlist.Designs.Remove(item);
                            if (remainingDesigns.Count > 0)
                                SelectDesign(remainingDesigns[0]);
                        }
```

Replace `AddItem_OnClick` (lines 244-252) entirely:
```csharp
        private void AddItem_OnClick(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainViewModel mainViewModel && sender is Button { Tag: string tagText } &&
                Enum.TryParse<SlideThemeType>(tagText, out var type))
            {
                var newTheme = new BaseSlideTheme { Type = type };
                mainViewModel.Playlist.Designs.Add(newTheme);
                SelectDesign(newTheme);
            }
        }
```

Replace `DuplicateItem_OnClick`'s selection line (line 267) — the method body stays the same up to `mainViewModel.Playlist.Designs.Add(copy);`, then replace `designsListBox.SelectedIndex = mainViewModel.Playlist.Designs.Count - 1;` with `SelectDesign(copy);`.

Replace `ImportItem_OnClick`'s selection line (line 342) similarly: replace `designsListBox.SelectedIndex = mainViewModel.Playlist.Designs.Count - 1;` with `SelectDesign(theme);`.

In `ChangeThemeBgGraphic_OnClick` (line 393), replace `var selectedTheme = designsListBox.SelectedItem as BaseSlideTheme;` with `var selectedTheme = SelectedDesign;`.

- [ ] **Step 7: Update `PreviewModeToggle_OnChecked`'s doc comment reference (no behavior change in this step — behavior updated in Task 6)**

No code change needed here yet; Task 6 adds the third toggle and updates this method's body together with the new scripture preview view.

- [ ] **Step 8: Build**

Run: `dotnet build HandsLiftedApp.Core`
Expected: builds with no errors.

- [ ] **Step 9: Commit**

```bash
git add HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs
git commit -m "feat: group SlideThemeDesigner list by theme type"
```

---

### Task 6: Scripture preview in the theme designer

**Files:**
- Create: `HandsLiftedApp.Core/Views/Designer/ScriptureSlideView.axaml`
- Create: `HandsLiftedApp.Core/Views/Designer/ScriptureSlideView.axaml.cs`
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml` (preview toggle row + preview `Grid`)
- Modify: `HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs` (`SyncEditorToSelection`, `PreviewModeToggle_OnChecked`)

**Interfaces:**
- Consumes: `ScriptureParagraphSpecBuilder.Build(ScriptureSlideInstance)` (existing), `ScriptureParagraphLayoutEngine.Paginate(IReadOnlyList<ScriptureVerseRef>, string, BaseSlideTheme)` (existing), `ScriptureVerseRef(int Chapter, int Verse, string Text)` (existing).
- Produces: `ScriptureSlideView.SetSlide(ScriptureSlideInstance?)`, matching `SongSlideView.SetSlide`'s exact signature shape.

- [ ] **Step 1: Create `ScriptureSlideView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
             xmlns:slides1="clr-namespace:HandsLiftedApp.Data.Slides"
             xmlns:skia="clr-namespace:HandsLiftedApp.Core.Render.Skia"
             mc:Ignorable="d" d:DesignWidth="1920" d:DesignHeight="1080"
             x:DataType="slides1:ScriptureSlideInstance"
             x:Class="HandsLiftedApp.Core.Views.Designer.ScriptureSlideView">

    <Grid>
        <skia:SlideCanvas x:Name="SlideCanvas" />
    </Grid>
</UserControl>
```

- [ ] **Step 2: Create `ScriptureSlideView.axaml.cs`**

```csharp
using System;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using HandsLiftedApp.Core.Render.Skia.Builders;
using HandsLiftedApp.Data.Slides;
using ReactiveUI;

namespace HandsLiftedApp.Core.Views.Designer;

public partial class ScriptureSlideView : UserControl
{
    private IDisposable? _subscription;

    public ScriptureSlideView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ScriptureSlideInstance slide)
            SetSlide(slide);
    }

    public void SetSlide(ScriptureSlideInstance? slide)
    {
        _subscription?.Dispose();
        _subscription = null;

        if (slide == null)
        {
            SlideCanvas.Spec = null;
            return;
        }

        var themePropertyChanges = slide
            .WhenAnyValue(s => s.Theme)
            .Select(t => t?.Changed.Select(_ => Unit.Default) ?? Observable.Never<Unit>())
            .Switch();

        _subscription = Observable
            .Merge(
                slide.WhenAnyValue(s => s.Lines, s => s.Theme).Select(_ => Unit.Default),
                themePropertyChanges
            )
            .Subscribe(_ => RebuildSpec(slide));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _subscription?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    private void RebuildSpec(ScriptureSlideInstance slide)
    {
        SlideCanvas.Spec = ScriptureParagraphSpecBuilder.Build(slide);
    }
}
```

- [ ] **Step 3: Add the third preview toggle and view to `SlideThemeDesigner.axaml`**

Replace the preview toggle row and preview `Grid` (originally lines 157-173, now shifted by Task 5's edits — locate by content):

```xml
                <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="8" Spacing="4">
                    <RadioButton x:Name="previewLyricToggle" Content="Song Lyric"
                                 GroupName="ThemePreviewMode" IsChecked="True"
                                 IsCheckedChanged="PreviewModeToggle_OnChecked" />
                    <RadioButton x:Name="previewTitleToggle" Content="Song Title"
                                 GroupName="ThemePreviewMode"
                                 IsCheckedChanged="PreviewModeToggle_OnChecked" />
                </StackPanel>
                <Viewbox StretchDirection="DownOnly"
                         VerticalAlignment="Stretch"
                         HorizontalAlignment="Stretch">
                    <Grid Width="1920" Height="1080">
                        <designer:SongSlideView x:Name="themePreviewSlideView" Height="1080" Width="1920" />
                        <designer:SongTitleSlideView x:Name="themePreviewTitleSlideView" Height="1080" Width="1920"
                                                      IsVisible="False" />
                    </Grid>
                </Viewbox>
```

with:

```xml
                <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Margin="8" Spacing="4">
                    <RadioButton x:Name="previewLyricToggle" Content="Song Lyric"
                                 GroupName="ThemePreviewMode" IsChecked="True"
                                 IsCheckedChanged="PreviewModeToggle_OnChecked" />
                    <RadioButton x:Name="previewTitleToggle" Content="Song Title"
                                 GroupName="ThemePreviewMode"
                                 IsCheckedChanged="PreviewModeToggle_OnChecked" />
                    <RadioButton x:Name="previewScriptureToggle" Content="Scripture"
                                 GroupName="ThemePreviewMode"
                                 IsCheckedChanged="PreviewModeToggle_OnChecked" />
                </StackPanel>
                <Viewbox StretchDirection="DownOnly"
                         VerticalAlignment="Stretch"
                         HorizontalAlignment="Stretch">
                    <Grid Width="1920" Height="1080">
                        <designer:SongSlideView x:Name="themePreviewSlideView" Height="1080" Width="1920" />
                        <designer:SongTitleSlideView x:Name="themePreviewTitleSlideView" Height="1080" Width="1920"
                                                      IsVisible="False" />
                        <designer:ScriptureSlideView x:Name="themePreviewScriptureView" Height="1080" Width="1920"
                                                      IsVisible="False" />
                    </Grid>
                </Viewbox>
```

- [ ] **Step 4: Wire the scripture sample slide into `SyncEditorToSelection`**

In `SlideThemeDesigner.axaml.cs`, add the sample data as class-level constants next to the existing `PreviewText`/`PreviewTitleText`/`PreviewCopyrightText`:

```csharp
        private static readonly IReadOnlyList<ScriptureVerseRef> PreviewScriptureVerses = new List<ScriptureVerseRef>
        {
            new(3, 16, "For God so loved the world, that he gave his only begotten Son, that whosoever believeth in him should not perish, but have everlasting life."),
            new(3, 17, "For God sent not his Son into the world to condemn the world, but that the world through him might be saved."),
        };

        private const string PreviewScriptureHeader = "John 3:16-17";
```

Add `using HandsLiftedApp.Core.Models.RuntimeData.Items;` to the file's usings (for `ScriptureVerseRef`) and `using HandsLiftedApp.Core.Models.RuntimeData.Items;` also brings `ScriptureParagraphLayoutEngine` (same namespace).

Update `SyncEditorToSelection` (lines 85-111) to also build and set the scripture preview:

```csharp
        private void SyncEditorToSelection()
        {
            var item = SelectedDesign;
            themeEditorPanel.DataContext = item;
            if (item != null)
            {
                fontComboBox.SelectedValue = item.FontFamilyAsText;
                UpdateFontWeightOptions(item.FontFamilyAsText, item.FontWeight);
                FontWeightComboBox.SelectedValue = item.FontWeight;
                themePreviewSlideView.SetSlide(new SongSlideInstance(null, null, null)
                {
                    Text = PreviewText,
                    Theme = item,
                });
                themePreviewTitleSlideView.SetSlide(new SongTitleSlideInstance(null)
                {
                    Title = PreviewTitleText,
                    Copyright = PreviewCopyrightText,
                    Theme = item,
                });

                var scriptureSlide = new ScriptureSlideInstance(null, "theme-preview") { Theme = item };
                var pages = ScriptureParagraphLayoutEngine.Paginate(PreviewScriptureVerses, PreviewScriptureHeader, item);
                if (pages.Count > 0)
                {
                    scriptureSlide.Lines = pages[0].Lines;
                    scriptureSlide.EffectiveFontSize = pages[0].FontSize;
                }
                themePreviewScriptureView.SetSlide(scriptureSlide);
            }
            else
            {
                themePreviewSlideView.SetSlide(null);
                themePreviewTitleSlideView.SetSlide(null);
                themePreviewScriptureView.SetSlide(null);
            }
        }
```

- [ ] **Step 5: Update `PreviewModeToggle_OnChecked`**

Replace (lines 171-184):
```csharp
        private void PreviewModeToggle_OnChecked(object? sender, RoutedEventArgs e)
        {
            // Avalonia 12's ToggleButton only exposes IsCheckedChanged, which fires twice per
            // group toggle: once when the clicked radio button becomes checked (while the
            // sibling is still stale-checked), and again when the group manager unchecks the
            // sibling. The handler body below is a pure function of both toggles' current
            // IsChecked state, so it's safe - and necessary - to let it run on both
            // transitions: the first pass may briefly show both panels, but the second pass
            // (after the sibling settles) recomputes from the final state and corrects it.
            // Guarding to only the first transition (as the old WPF-style Checked-only
            // semantics would) would leave the transient "both visible" result uncorrected.
            themePreviewSlideView.IsVisible = previewLyricToggle.IsChecked == true;
            themePreviewTitleSlideView.IsVisible = previewTitleToggle.IsChecked == true;
        }
```
with:
```csharp
        private void PreviewModeToggle_OnChecked(object? sender, RoutedEventArgs e)
        {
            // Avalonia 12's ToggleButton only exposes IsCheckedChanged, which fires twice per
            // group toggle: once when the clicked radio button becomes checked (while the
            // sibling is still stale-checked), and again when the group manager unchecks the
            // sibling. The handler body below is a pure function of all three toggles' current
            // IsChecked state, so it's safe - and necessary - to let it run on every
            // transition: an intermediate pass may briefly show more than one panel, but the
            // final pass (after the siblings settle) recomputes from the final state and
            // corrects it. Guarding to only the first transition (as the old WPF-style
            // Checked-only semantics would) would leave a transient "multiple visible" result
            // uncorrected.
            themePreviewSlideView.IsVisible = previewLyricToggle.IsChecked == true;
            themePreviewTitleSlideView.IsVisible = previewTitleToggle.IsChecked == true;
            themePreviewScriptureView.IsVisible = previewScriptureToggle.IsChecked == true;
        }
```

- [ ] **Step 6: Build**

Run: `dotnet build HandsLiftedApp.Core`
Expected: builds with no errors.

- [ ] **Step 7: Commit**

```bash
git add HandsLiftedApp.Core/Views/Designer/ScriptureSlideView.axaml HandsLiftedApp.Core/Views/Designer/ScriptureSlideView.axaml.cs HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml HandsLiftedApp.Core/Views/Designer/SlideThemeDesigner.axaml.cs
git commit -m "feat: add scripture preview to theme designer"
```

---

### Task 7: Manual verification pass

This repo's CLAUDE.md is explicit that UI changes must be clicked through in the running app, not just built — none of Tasks 4-6 have automated coverage for the actual rendered/interactive behavior. Use the `run` skill (or launch the app the way this project normally launches for manual testing) and confirm all of the following in one session:

- [ ] **Step 1: Verify grouping in the theme designer**

Open the Slide Theme Designer. Using the three "+ General" / "+ Song" / "+ Scripture" buttons, add one design of each type. Confirm each new design appears under the correct header (General / Song Themes / Scripture Themes) and nowhere else, and that clicking a design in one section clears the selection highlight in the other two sections.

- [ ] **Step 2: Verify the Type field is editable and live-regroups**

Select a General-type design, change its "Applies To" field to "SongTheme" via the new ComboBox in the editor panel, and confirm it immediately disappears from the General section and appears in the Song Themes section (still selected).

- [ ] **Step 3: Verify all three preview toggles work for any design**

With a General-type design selected, click through Song Lyric / Song Title / Scripture toggles and confirm each renders a plausible preview (sample lyric text, sample title, and the John 3:16-17 sample verse text respectively) using that design's theme. Repeat for a Song-type and a Scripture-type design — confirm all three toggles remain available and functional regardless of the selected design's Type.

- [ ] **Step 4: Verify song item picker filtering**

Add a song item to a playlist, open its "Theme" flyout. Confirm the ComboBox lists only General and Song-type designs (the Scripture-type design from Step 1 must not appear). Open the Song editor's "Design" tab for the same song and confirm the same filtered list.

- [ ] **Step 5: Verify scripture item picker filtering**

Add a scripture item to a playlist, open its "Theme" flyout. Confirm the ComboBox lists only General and Scripture-type designs (the Song-type design must not appear), and that the blank "use default" entry (✕ button / null entry) is still present and still works.

- [ ] **Step 6: Verify old playlists still work**

Open a playlist file saved before this change (or hand-edit a `<BaseSlideTheme>` element in a test playlist XML to remove any `<Type>` element, if none exist from before this feature). Confirm its designs load into the "General" group and remain selectable from both the song and scripture item pickers.

- [ ] **Step 7: Run the full automated test suite**

Run: `dotnet test HandsLiftedApp.Tests`
Expected: all tests pass, including the new tests from Tasks 1-3.

- [ ] **Step 8: Final commit (if manual verification uncovered fixups)**

```bash
git add -A
git commit -m "fix: address issues found in manual verification of design type grouping"
```

(Skip this step entirely if Steps 1-7 required no code changes.)
