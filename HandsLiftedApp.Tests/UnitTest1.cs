using Avalonia;
using HandsLiftedApp.ViewModels;

namespace HandsLiftedApp.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var x = new PreferencesViewModel();

            PixelRect pixelRect1 = new PixelRect(0, 0, 1920, 1080);
            PixelRect pixelRect2 = new PixelRect(123, 567, 1920, 1080);

            Assert.AreEqual(new PreferencesViewModel.DisplayModel(pixelRect1),
                new PreferencesViewModel.DisplayModel(pixelRect1));
            Assert.AreNotEqual(new PreferencesViewModel.DisplayModel(pixelRect1),
                new PreferencesViewModel.DisplayModel(pixelRect2));
        }
    }
}