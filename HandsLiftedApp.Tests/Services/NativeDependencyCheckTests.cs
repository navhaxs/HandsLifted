using HandsLiftedApp.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class NativeDependencyCheckTests
{
    private static readonly NativeDependency[] Required =
    {
        new("a.dll", "feature A"),
        new("b.dll", "feature B"),
    };

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nativecheck-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [TestMethod]
    public void FindMissing_ReturnsOnlyTheFilesThatAreAbsent()
    {
        var dir = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.dll"), "");

            var missing = NativeDependencyCheck.FindMissing(dir, Required);

            CollectionAssert.AreEqual(new[] { "b.dll" }, missing.Select(m => m.FileName).ToArray());
        }
        finally { Directory.Delete(dir, true); }
    }

    [TestMethod]
    public void BuildWarning_IsNullWhenNothingIsMissing()
    {
        Assert.IsNull(NativeDependencyCheck.BuildWarning(@"C:\app", Array.Empty<NativeDependency>()));
    }

    [TestMethod]
    public void BuildWarning_NamesEachMissingFileItsFeatureAndTheFolder()
    {
        var warning = NativeDependencyCheck.BuildWarning(@"C:\app", Required);

        Assert.IsNotNull(warning);
        StringAssert.Contains(warning.Message, @"C:\app");
        StringAssert.Contains(warning.Message, "a.dll");
        StringAssert.Contains(warning.Message, "feature A");
        StringAssert.Contains(warning.Message, "b.dll");
        StringAssert.Contains(warning.Message, "feature B");
    }

    [TestMethod]
    public void RequiredOnWindows_CoversLibmpvAndNdi()
    {
        var names = NativeDependencyCheck.RequiredOnWindows.Select(d => d.FileName).ToArray();
        CollectionAssert.Contains(names, "libmpv-2.dll");
        CollectionAssert.Contains(names, "Processing.NDI.Lib.x64.dll");
    }
}
