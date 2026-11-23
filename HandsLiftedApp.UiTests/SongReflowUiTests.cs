using Avalonia.Controls;
using HandsLiftedApp.Core;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Core.ViewModels.Editor;
using HandsLiftedApp.Core.Views.Editors;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.UiTests.Support;

namespace HandsLiftedApp.UiTests;

[TestClass]
public class SongReflowUiTests
{
    // The flyout MenuItem click is the classic "silently does nothing" trap, so click it for real.
    [TestMethod]
    public async Task Reflow_Flyout_Smart_RewritesLyrics_AndUndoRestores()
    {
        await HeadlessSession.Run(() =>
        {
            var main = UiTestHelpers.OpenMainWindow(); // sets up Globals (preferences etc.)

            var data = new SongItem { Title = "Test Song" };
            data.Stanzas.Add(new SongStanza(Guid.NewGuid(), "Verse 1", "a\nb\nc\nd\ne\nf"));
            data.Arrangement.Add(data.Stanzas[0].Id);
            Globals.Instance.SongLibraryIndex.Register(data, "irrelevant.xml", "irrelevant");
            var playlist = new PlaylistInstance();
            var song = new SongItemInstance(playlist) { SongId = data.UUID };
            var vm = new SongEditorViewModel(song, playlist);

            var window = new Window { Width = 520, Height = 700, Content = new SongLyricBlockEditor { DataContext = vm } };
            window.Show();
            UiTestHelpers.Settle();

            var reflow = window.FindByText<Button>("Reflow");
            Assert.IsNotNull(reflow, "Reflow button not found");
            window.Click(reflow);

            var smart = reflow.FindFlyoutMenuItem("Smart");
            Assert.IsNotNull(smart, "Smart menu item not found");
            smart.ClickInPopup();

            Assert.AreEqual("a\nb\nc\n\nd\ne\nf", song.Stanzas[0].Lyrics);
            window.Screenshot("song-reflow-smart");

            vm.UndoStack.Undo();
            Assert.AreEqual("a\nb\nc\nd\ne\nf", song.Stanzas[0].Lyrics);

            window.Close();
            main.Close();
        });
    }
}
