using System.Collections.Specialized;
using System.Text.Json;
using Avalonia.Controls;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.DevTools;
using HandsLiftedApp.UiTests.Support;

namespace HandsLiftedApp.UiTests;

/// <summary>Exercises the live-app devtools API (the same entry point the HTTP server uses) against a headless MainWindow.</summary>
[TestClass]
public class DevToolsApiTests
{
    private static async Task<DevResponse> Call(string pathAndQuery, bool post = false)
    {
        var q = pathAndQuery.IndexOf('?');
        return await DevToolsApi.Handle(new DevRequest
        {
            Method = post ? "POST" : "GET",
            Path = q < 0 ? pathAndQuery : pathAndQuery[..q],
            Query = System.Web.HttpUtility.ParseQueryString(q < 0 ? "" : pathAndQuery[(q + 1)..]),
        });
    }

    private static JsonElement Json(DevResponse r) => JsonDocument.Parse(r.Body).RootElement;

    [TestMethod]
    public async Task Tree_Find_And_State_Describe_The_Running_App()
    {
        await HeadlessSession.Run(async () =>
        {
            var window = UiTestHelpers.OpenMainWindow();
            Surfaces.WindowProvider = () => [window];

            // Deep enough to reach the bottom toolbar's "Add Item" button (the layout nests it ~10 levels down).
            var tree = await Call("/tree?depth=16");
            Assert.AreEqual(200, tree.Status, tree.BodyText);
            StringAssert.Contains(tree.BodyText, "MainWindow");
            StringAssert.Contains(tree.BodyText, "\"Add Item\"");

            var find = Json(await Call("/find?text=Add%20Item"));
            Assert.IsTrue(find.GetProperty("count").GetInt32() >= 1);

            var state = Json(await Call("/state"));
            Assert.IsTrue(state.GetProperty("started").GetBoolean());
            Assert.AreEqual(0, state.GetProperty("playlist").GetProperty("itemCount").GetInt32());

            var missing = await Call("/click?text=No%20Such%20Thing", post: true);
            Assert.AreEqual(404, missing.Status);

            var getClick = await Call("/click?text=Add%20Item");
            Assert.AreEqual(405, getClick.Status, "mutating endpoints must refuse GET");

            window.Close();
        });
    }

    [TestMethod]
    public async Task Click_Via_Accessibility_Api_Opens_Flyout_And_Picks_Menu_Item()
    {
        await HeadlessSession.Run(async () =>
        {
            var window = UiTestHelpers.OpenMainWindow();
            Surfaces.WindowProvider = () => [window];
            var vm = (MainViewModel)window.DataContext!;

            var click = await Call("/click?text=Add%20Item", post: true);
            Assert.AreEqual(200, click.Status, click.BodyText);
            UiTestHelpers.Settle();

            // The invoke-click must open the button's flyout like a real click does. (Headless popups are hosted in the
            // window's own overlay layer, not a separate PopupRoot, so /windows cannot list them as "popup" surfaces here:
            // assert on the flyout itself. The live app lists them; see tools\devtools.ps1.)
            var addItem = window.FindByText<Avalonia.Controls.Button>("Add Item")!;
            Assert.IsTrue(addItem.Flyout!.IsOpen, "the invoke-click did not open the Add Item flyout");

            // In headless the flyout content is reachable by the same selector machinery.
            var pick = await Call("/click?text=Section", post: true);
            Assert.AreEqual(200, pick.Status, pick.BodyText);
            Assert.AreEqual(1, vm.Playlist.Items.Count, "clicking the 'Section' menu item via the accessibility API did not add a section");

            var state = Json(await Call("/state"));
            Assert.AreEqual("SectionHeadingItem", state.GetProperty("playlist").GetProperty("items")[0].GetProperty("type").GetString());

            window.Close();
        });
    }

    [TestMethod]
    public async Task Screenshot_Props_And_ViewModel_Dump_Work()
    {
        await HeadlessSession.Run(async () =>
        {
            var window = UiTestHelpers.OpenMainWindow();
            Surfaces.WindowProvider = () => [window];

            var shot = await Call("/screenshot?raw=1");
            Assert.AreEqual("image/png", shot.ContentType);
            Assert.IsTrue(shot.Body.Length > 1000);
            Assert.AreEqual(0x89, shot.Body[0], "not a PNG");

            var props = Json(await Call("/props?text=Add%20Item&type=TextBlock"));
            Assert.IsTrue(props.GetProperty("properties").EnumerateObject().Any());

            var vmDump = Json(await Call("/vm?path=MainViewModel.Playlist&depth=1"));
            Assert.AreEqual("PlaylistInstance", vmDump.GetProperty("$type").GetString());

            var badPath = await Call("/vm?path=MainViewModel.Nope");
            Assert.AreEqual(404, badPath.Status);

            window.Close();
        });
    }
}
