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

            static string[] Words(string s) => s.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            static int PhysicalLines(string s) => s.Split('\n').Count(l => l.Length > 0);

            var report = new StringBuilder();
            int stanzas = 0, fourLine = 0, splitFourLine = 0, splitLines = 0, shrunkSlides = 0, slides = 0;
            foreach (var file in Directory.GetFiles(SongsDir, "*.xml"))
            {
                var doc = XDocument.Load(file);
                foreach (var stanza in doc.Descendants().Where(e => e.Name.LocalName == "SongStanza"))
                {
                    var name = stanza.Elements().First(e => e.Name.LocalName == "Name").Value;
                    var lyrics = stanza.Elements().First(e => e.Name.LocalName == "Lyrics").Value;
                    var lines = LyricReflow.NormalisedLines(lyrics);

                    var result = LyricReflow.Reflow(lyrics, ReflowMode.Smart);

                    // splitting a long line changes line breaks (and capitalises the new line's first letter), never the words
                    CollectionAssert.AreEqual(Words(string.Join(" ", lines)), Words(result),
                        $"{Path.GetFileName(file)} / {name}: words changed");
                    var again = LyricReflow.Reflow(result, ReflowMode.Smart);
                    CollectionAssert.AreEqual(Words(result), Words(again),
                        $"{Path.GetFileName(file)} / {name}: re-run changed the words");
                    Assert.AreEqual(PhysicalLines(result), PhysicalLines(again),
                        $"{Path.GetFileName(file)} / {name}: re-run split again");
                    if (PhysicalLines(result) > 3)
                        Assert.IsTrue(result.Split("\n\n").All(c => c.Split('\n').Length is >= 2 and <= 4),
                            $"{Path.GetFileName(file)} / {name}: chunk out of range");

                    if (lines.Count > 0)
                    {
                        stanzas++;
                        splitLines += PhysicalLines(result) - lines.Count;
                        foreach (var slide in result.Split("\n\n"))
                        {
                            slides++;
                            if (LyricReflow.PredictedScale(slide.Split('\n'), ReflowMetrics.Default) < 0.8) shrunkSlides++;
                        }
                        if (lines.Count == 4 && result.Contains("\n\n")) splitFourLine++;
                        if (lines.Count == 4) fourLine++;
                    }

                    if (lines.Count > 0)
                        report.AppendLine($"== {Path.GetFileNameWithoutExtension(file)} / {name} ({lines.Count} lines, {lines.Sum(l => l.Length)} chars)")
                              .AppendLine(result.Replace("\n\n", "\n   ---\n")).AppendLine();
                }
            }

            report.Insert(0, $"SUMMARY: {stanzas} stanzas, {fourLine} with exactly 4 lines, {splitFourLine} of those split 2+2 for density; " +
                             $"{splitLines} long lines split; {shrunkSlides}/{slides} slides predicted below 0.8 font scale (default theme)\n\n");
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "LyricReflowLibraryTests.out.txt"), report.ToString());
        }
    }
}
