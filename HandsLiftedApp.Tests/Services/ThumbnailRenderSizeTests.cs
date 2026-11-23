using HandsLiftedApp.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class ThumbnailRenderSizeTests
{
    [TestMethod]
    public void ComputeWidth_RoundsUpToStepBucket()
    {
        // grid: 290 * 80% - 4 margin = 228 px at 100% scaling -> next multiple of 16 is 240.
        // song editor: 240 * 80% = 192, smaller than the grid.
        Assert.AreEqual(240, ThumbnailRenderSize.ComputeWidth(80, 290, 80, 1));
    }

    [TestMethod]
    public void ComputeWidth_ScalesWithDisplayScaling()
    {
        // 228 px * 2 = 456 -> 464
        Assert.AreEqual(464, ThumbnailRenderSize.ComputeWidth(80, 290, 80, 2));
    }

    [TestMethod]
    public void ComputeWidth_UsesSongEditorWidthWhenLarger()
    {
        // grid: 290 * 50% - 4 = 141; song editor: 240 * 150% = 360 -> rounded up to 368
        Assert.AreEqual(368, ThumbnailRenderSize.ComputeWidth(50, 290, 150, 1));
    }

    [TestMethod]
    public void ComputeWidth_ClampsToMinAndMax()
    {
        Assert.AreEqual(ThumbnailRenderSize.MinWidth, ThumbnailRenderSize.ComputeWidth(10, 290, 10, 1));
        Assert.AreEqual(ThumbnailRenderSize.MaxWidth, ThumbnailRenderSize.ComputeWidth(400, 290, 80, 3));
    }

    [TestMethod]
    public void ComputeWidth_NeverBelowOneBucket_ForTinyOrNegativeInput()
    {
        Assert.AreEqual(ThumbnailRenderSize.MinWidth, ThumbnailRenderSize.ComputeWidth(0, 290, 0, 1));
        Assert.AreEqual(ThumbnailRenderSize.MinWidth, ThumbnailRenderSize.ComputeWidth(-50, 290, -50, 0));
    }
}
