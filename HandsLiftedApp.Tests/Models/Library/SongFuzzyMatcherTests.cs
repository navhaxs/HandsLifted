using HandsLiftedApp.Core.Models.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.Library
{
    [TestClass]
    public class SongFuzzyMatcherTests
    {
        // Lyrics long enough that the letters b-l-o-r-p appear in order somewhere inside them.
        private const string Lyrics =
            "Blessed be your name in the land that is plentiful where streams of abundance flow " +
            "Blessed be your name when I'm found in the desert place though I walk through the wilderness";

        private static SongIndexEntry Entry(string title = "Blessed Be Your Name",
            string copyright = "Matt Redman", string lyrics = Lyrics) =>
            new("C:\\Songs\\song.xml", title, copyright, lyrics);

        [TestMethod]
        public void Score_NonsenseTerm_WhoseLettersAppearInOrderInLyrics_IsNotAMatch()
        {
            // "blorp" is not a word in the title, copyright or lyrics. Its letters only occur
            // in order by coincidence across the whole lyric text.
            Assert.AreEqual(0, SongFuzzyMatcher.Score(new[] { "blorp" }, Entry()));
        }

        [TestMethod]
        public void Score_TitleSubsequence_StillMatches()
        {
            // Abbreviated typing of the title is the intended fuzzy behaviour.
            Assert.IsTrue(SongFuzzyMatcher.Score(new[] { "blsd" }, Entry()) > 0);
        }

        [TestMethod]
        public void Score_LyricSubstring_StillMatches()
        {
            Assert.IsTrue(SongFuzzyMatcher.Score(new[] { "wilderness" }, Entry(title: "Other")) > 0);
        }

        [TestMethod]
        public void Score_LyricWordPrefix_StillMatches()
        {
            Assert.IsTrue(SongFuzzyMatcher.Score(new[] { "wilder" }, Entry(title: "Other")) > 0);
        }

        [TestMethod]
        public void Score_AnyUnmatchedToken_ExcludesTheSong()
        {
            Assert.AreEqual(0, SongFuzzyMatcher.Score(new[] { "blessed", "blorp" }, Entry()));
        }
    }
}
