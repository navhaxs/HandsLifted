using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HandsLiftedApp.Core.Models.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.Library
{
    /// <summary>Runs Smart reflow over the real song library (when present) and dumps before/after for eyeballing.</summary>
    [TestClass]
    public class LyricReflowLibraryTests
    {
        private const string SongsDir = @"V:\Songs";

        [TestMethod]
        public void Smart_OverRealLibrary_HoldsInvariants_AndDumpsOutput()
        {
            if (!Directory.Exists(SongsDir)) return; // library not available on this machine

            var report = new StringBuilder();
            foreach (var file in Directory.GetFiles(SongsDir, "*.xml"))
            {
                var doc = XDocument.Load(file);
                foreach (var stanza in doc.Descendants().Where(e => e.Name.LocalName == "SongStanza"))
                {
                    var name = stanza.Elements().First(e => e.Name.LocalName == "Name").Value;
                    var lyrics = stanza.Elements().First(e => e.Name.LocalName == "Lyrics").Value;
                    var lines = LyricReflow.NormalisedLines(lyrics);

                    var result = LyricReflow.Reflow(lyrics, ReflowMode.Smart);

                    CollectionAssert.AreEqual(lines, result.Split('\n').Where(l => l.Length > 0).ToList(),
                        $"{Path.GetFileName(file)} / {name}: text changed");
                    Assert.AreEqual(result, LyricReflow.Reflow(result, ReflowMode.Smart),
                        $"{Path.GetFileName(file)} / {name}: not idempotent");
                    if (lines.Count > 4)
                        Assert.IsTrue(result.Split("\n\n").All(c => c.Split('\n').Length is >= 2 and <= 4),
                            $"{Path.GetFileName(file)} / {name}: chunk out of range");

                    if (lines.Count > 0)
                        report.AppendLine($"== {Path.GetFileNameWithoutExtension(file)} / {name} ({lines.Count} lines)")
                              .AppendLine(result.Replace("\n\n", "\n   ---\n")).AppendLine();
                }
            }

            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "LyricReflowLibraryTests.out.txt"), report.ToString());
        }
    }
}
