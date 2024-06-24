using HandsLiftedApp.Core.Utils;

namespace HandsLiftedApp.Tests
{
    [TestClass]
    public class FileReferenceTests
    {
        // NOTES:
        // - case sensitive
        // TODO - return warnings?
         
        [TestMethod]
        public void TestToAbsolutePath()
        {
            // Converting a relative path to absolute path - when input is a relative path, expand it
            Assert.AreEqual(@"C:\Presentations\Assets\Logo.png",
                RelativeFilePathResolver.ToAbsolutePath(@"C:\Presentations\", @"Assets\Logo.png"));
            
            // Converting a relative path to absolute path - when input is already an absolute path, do nothing  
            Assert.AreEqual(@"C:\Presentations\Assets\Logo.png",
                RelativeFilePathResolver.ToAbsolutePath(@"C:\Presentations\", @"C:\Presentations\Assets\Logo.png"));
        }
        
        [TestMethod]
        public void TestToRelativePath()
        {
            // Converting an absolute path to relative path - when root path matches, do replace
            Assert.AreEqual(@"Assets\Logo.png",
                RelativeFilePathResolver.ToRelativePath(@"C:\Presentations\", @"C:\Presentations\Assets\Logo.png"));
            
            // Converting an absolute path to relative path - when root path does not match, do nothing
            Assert.AreEqual(@"C:\Presentations\Assets\Logo.png",
                RelativeFilePathResolver.ToRelativePath(@"D:\SomeOtherDirectory\", @"C:\Presentations\Assets\Logo.png"));
        }
    }
}