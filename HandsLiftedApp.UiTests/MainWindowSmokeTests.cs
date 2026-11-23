using Avalonia.Controls;
using Avalonia.Headless;
using HandsLiftedApp.UiTests.Support;

namespace HandsLiftedApp.UiTests;

[TestClass]
public class MainWindowSmokeTests
{
    [TestMethod]
    public async Task MainWindow_Opens_And_Renders_A_Frame()
    {
        await HeadlessSession.Run(() =>
        {
            var window = UiTestHelpers.OpenMainWindow();
            Assert.IsTrue(window.IsVisible);
            Assert.IsNotNull(window.CaptureRenderedFrame(), "headless renderer produced no frame");
            window.Screenshot("mainwindow");
            window.Close();
        });
    }

    [TestMethod]
    public async Task EmptyPlaylist_ShowsHint_And_AddItem_Button_Is_Findable()
    {
        await HeadlessSession.Run(() =>
        {
            var window = UiTestHelpers.OpenMainWindow();

            Assert.IsNotNull(window.FindByText<TextBlock>("Playlist is empty. Begin by adding a item."),
                "empty-playlist hint not found in the visual tree");

            var addItem = window.FindByText<Button>("Add Item");
            Assert.IsNotNull(addItem, "'Add Item' button not found in the visual tree");
            Assert.IsTrue(addItem.IsEffectivelyEnabled);
            window.Close();
        });
    }

    // The bug class CLAUDE.md warns about (a flyout MenuItem click that silently does nothing) is exactly
    // what this catches: click through button -> flyout -> menu item and assert the view-model changed.
    [TestMethod]
    public async Task AddItem_Flyout_Section_Adds_A_Section_To_The_Playlist()
    {
        await HeadlessSession.Run(() =>
        {
            var window = UiTestHelpers.OpenMainWindow();
            var vm = (HandsLiftedApp.Core.ViewModels.MainViewModel)window.DataContext!;
            Assert.AreEqual(0, vm.Playlist.Items.Count);

            var addItem = window.FindByText<Button>("Add Item")!;
            window.Click(addItem);

            // Flyout content lives in its own PopupRoot, outside the window's visual tree.
            var section = addItem.FindFlyoutMenuItem("Section");
            Assert.IsNotNull(section, "'Section' menu item not found in the AddItem flyout");
            section.ClickInPopup();

            Assert.AreEqual(1, vm.Playlist.Items.Count, "clicking 'Section' did not add an item");
            window.Screenshot("after-add-section");
            window.Close();
        });
    }
}
