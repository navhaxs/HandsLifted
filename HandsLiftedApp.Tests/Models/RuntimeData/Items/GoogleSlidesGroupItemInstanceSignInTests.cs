using System.Threading.Tasks;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Tests.TestSupport;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.RuntimeData.Items;

[TestClass]
public class GoogleSlidesGroupItemInstanceSignInTests
{
    [TestMethod]
    public Task AbandonSignIn_ClearsIsBusy() => DispatcherTestThread.Run(() =>
    {
        var item = new GoogleSlidesGroupItemInstance(new PlaylistInstance()) { IsBusy = true };

        item.AbandonSignIn();

        Assert.IsFalse(item.IsBusy, "an abandoned sign-in must release the item so the user can sync again");
        return Task.CompletedTask;
    });
}
