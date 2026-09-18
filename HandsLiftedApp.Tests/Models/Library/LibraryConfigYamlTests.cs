using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HandsLiftedApp.Core.Models.Library.Config;
using YamlDotNet.Serialization;

namespace HandsLiftedApp.Tests.Models.Library;

[TestClass]
public class LibraryConfigYamlTests
{
    // LibraryItems switched from List<T> to ObservableCollection<T> so the Setup window's
    // library table can bind to it directly. Confirms YamlDotNet still round-trips it.
    [TestMethod]
    public void RoundTrips_ObservableCollection_LibraryItems()
    {
        var config = new LibraryConfig();
        config.LibraryItems.Add(new LibraryConfig.LibraryDefinition
        {
            Label = "Songs", Directory = @"C:\Songs", Type = LibraryType.Song
        });
        config.LibraryItems.Add(new LibraryConfig.LibraryDefinition
        {
            Label = "Media", Directory = @"C:\Media", Type = LibraryType.Media
        });

        var serializer = new SerializerBuilder().Build();
        var yaml = serializer.Serialize(config);

        var deserializer = new DeserializerBuilder().Build();
        var roundTripped = deserializer.Deserialize<LibraryConfig>(new StringReader(yaml));

        Assert.AreEqual(2, roundTripped.LibraryItems.Count);
        Assert.AreEqual("Songs", roundTripped.LibraryItems[0].Label);
        Assert.AreEqual(@"C:\Songs", roundTripped.LibraryItems[0].Directory);
        Assert.AreEqual(LibraryType.Song, roundTripped.LibraryItems[0].Type);
        Assert.AreEqual("Media", roundTripped.LibraryItems[1].Label);
        Assert.AreEqual(LibraryType.Media, roundTripped.LibraryItems[1].Type);
    }
}
