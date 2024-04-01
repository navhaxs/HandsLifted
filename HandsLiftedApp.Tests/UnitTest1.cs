using Avalonia;
using HandsLiftedApp.Core.ViewModels;
using HandsLiftedApp.ViewModels;

namespace HandsLiftedApp.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var x = new AppPreferencesViewModel();
            
            PixelRect pixelRect1 = new PixelRect(0, 0, 1920, 1080);
            PixelRect pixelRect2 = new PixelRect(123, 567, 1920, 1080);
            
            Assert.AreEqual(new AppPreferencesViewModel.DisplayModel(pixelRect1),
                new AppPreferencesViewModel.DisplayModel(pixelRect1));
            Assert.AreNotEqual(new AppPreferencesViewModel.DisplayModel(pixelRect1),
                new AppPreferencesViewModel.DisplayModel(pixelRect2));
        }
    }
}