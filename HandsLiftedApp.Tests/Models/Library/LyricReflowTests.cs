using System;
using System.Linq;
using HandsLiftedApp.Core.Models.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Models.Library
{
    [TestClass]
    public class LyricReflowTests
    {
        private static string Lines(int n) => string.Join("\n", Enumerable.Range(1, n).Select(i => "L" + i));

        private static int[] Sizes(string s) =>
            s.Split("\n\n").Select(c => c.Split('\n').Length).ToArray();

        private static void AssertSizes(string result, params int[] expected) =>
            CollectionAssert.AreEqual(expected, Sizes(result), "sizes were " + string.Join(",", Sizes(result)));

        [TestMethod]
        public void Fixed2_6Lines_ThreeChunksOfTwo() =>
            AssertSizes(LyricReflow.Reflow(Lines(6), ReflowMode.Fixed(2)), 2, 2, 2);

        [TestMethod]
        public void Fixed3_7Lines_RebalancesToAvoidOrphan() =>
            AssertSizes(LyricReflow.Reflow(Lines(7), ReflowMode.Fixed(3)), 3, 2, 2);

        [TestMethod]
        public void Fixed2_5Lines_Is2_3() =>
            AssertSizes(LyricReflow.Reflow(Lines(5), ReflowMode.Fixed(2)), 2, 3);

        [TestMethod]
        public void Fixed4_8Lines_Is4_4() =>
            AssertSizes(LyricReflow.Reflow(Lines(8), ReflowMode.Fixed(4)), 4, 4);

        [TestMethod]
        public void Fixed3_3Lines_StaysWhole() =>
            AssertSizes(LyricReflow.Reflow(Lines(3), ReflowMode.Fixed(3)), 3);

        [TestMethod]
        public void Fixed4_2Lines_StaysWhole() =>
            AssertSizes(LyricReflow.Reflow(Lines(2), ReflowMode.Fixed(4)), 2);

        [TestMethod]
        public void Fixed2_OverwritesExistingBreaks() =>
            AssertSizes(LyricReflow.Reflow("a\nb\nc\nd\n\ne\nf", ReflowMode.Fixed(2)), 2, 2, 2);

        [TestMethod]
        public void HandlesCrLfAndWhitespaceBlanks()
        {
            var r = LyricReflow.Reflow("a\r\nb\r\n  \r\n\r\nc\r\nd\r\n", ReflowMode.Fixed(2));
            Assert.AreEqual("a\nb\n\nc\nd", r);
            Assert.IsFalse(r.Contains('\r'));
        }

        [TestMethod]
        public void EmptyOrNull_ReturnsEmpty()
        {
            Assert.AreEqual("", LyricReflow.Reflow(null, ReflowMode.Fixed(2)));
            Assert.AreEqual("", LyricReflow.Reflow("", ReflowMode.Fixed(2)));
            Assert.AreEqual("", LyricReflow.Reflow("  \n \n", ReflowMode.Fixed(2)));
        }

        [TestMethod]
        public void TextPreserved()
        {
            var input = "  a b \n\n c\nd  \ne\nf\ng";
            var r = LyricReflow.Reflow(input, ReflowMode.Fixed(3));
            var outLines = r.Split('\n').Where(l => l.Length > 0);
            CollectionAssert.AreEqual(LyricReflow.NormalisedLines(input), outLines.ToList());
        }

        [TestMethod]
        public void Idempotent_Fixed()
        {
            for (var n = 1; n <= 12; n++)
                foreach (var m in new[] { 2, 3, 4 })
                {
                    var mode = ReflowMode.Fixed(m);
                    var once = LyricReflow.Reflow(Lines(n), mode);
                    Assert.AreEqual(once, LyricReflow.Reflow(once, mode), $"n={n} m={m}");
                }
        }

        private static string Smart(string s) => LyricReflow.Reflow(s, ReflowMode.Smart);

        [TestMethod]
        public void Smart_6Lines_Is3_3() => AssertSizes(Smart(Lines(6)), 3, 3);

        [TestMethod]
        public void Smart_8Lines_Is4_4() => AssertSizes(Smart(Lines(8)), 4, 4);

        [TestMethod]
        public void Smart_4Lines_StaysWhole() => AssertSizes(Smart(Lines(4)), 4);

        [TestMethod]
        public void Smart_3Lines_StaysWhole() => AssertSizes(Smart(Lines(3)), 3);

        [TestMethod]
        public void Smart_1Line_StaysWhole() => AssertSizes(Smart(Lines(1)), 1);

        [TestMethod]
        public void Smart_9Lines_Is3_3_3() => AssertSizes(Smart(Lines(9)), 3, 3, 3);

        [TestMethod]
        public void Smart_7Lines_Is4_3() => AssertSizes(Smart(Lines(7)), 4, 3);

        [TestMethod]
        public void Smart_20Lines_AllChunks2To4() =>
            Assert.IsTrue(Sizes(Smart(Lines(20))).All(s => s >= 2 && s <= 4));

        [TestMethod]
        public void Smart_NeverOrphansOneLine()
        {
            for (var n = 5; n <= 16; n++)
                Assert.IsTrue(Sizes(Smart(Lines(n))).All(s => s >= 2), "n=" + n);
        }

        [TestMethod]
        public void Smart_5Lines_RhymeDecidesSplit()
        {
            AssertSizes(Smart("x light\nx night\nx sight\ny hand\ny stand"), 3, 2);
            AssertSizes(Smart("y hand\ny stand\nx light\nx night\nx sight"), 2, 3);
        }

        // Lines(n) all "rhyme" (digits are stripped), so also cover stanzas where nothing rhymes.
        [TestMethod]
        public void Smart_NoRhymes_FallsBackToEvenSplit()
        {
            string Plain(int n) => string.Join("\n", new[] { "apple", "stone", "river", "cloud", "wing", "mark", "bread", "glow", "tent" }.Take(n).Select(w => "I see " + w));
            AssertSizes(Smart(Plain(5)), 3, 2);
            AssertSizes(Smart(Plain(6)), 3, 3);
            AssertSizes(Smart(Plain(7)), 4, 3);
            AssertSizes(Smart(Plain(8)), 4, 4);
            AssertSizes(Smart(Plain(9)), 3, 3, 3);
        }

        [TestMethod]
        public void Smart_RepeatedOpening_StartsNewSlide()
        {
            const string verse = "Your word is a lamp unto my feet\nYour way is the only way for me\n" +
                                 "It's a narrow road that leads to life\nBut I want to be on it\n" +
                                 "It's a narrow road but the mercy's wide\n'Cause You're good on Your promise";
            AssertSizes(Smart(verse), 4, 2);
        }

        // ---- character density: a slide over LyricReflow.SlideCharCap total characters is too heavy ----

        /// <summary>One line of exactly <paramref name="len"/> characters (distinct per i, no repeated openings).</summary>
        private static string Line(int i, int len)
        {
            var head = $"L{i} ";
            return head + new string('x', len - head.Length);
        }

        private static string Dense(int count, int len) =>
            string.Join("\n", Enumerable.Range(1, count).Select(i => Line(i, len)));

        private static int[] CharsPerSlide(string s) =>
            s.Split("\n\n").Select(c => c.Split('\n').Sum(l => l.Length)).ToArray();

        [TestMethod]
        public void Smart_4DenseLines_SplitInHalf() =>
            AssertSizes(Smart(Dense(4, 45)), 2, 2); // 180 chars, the "Chorus 3" case

        [TestMethod]
        public void Smart_4Lines_JustOverCap_Splits() =>
            AssertSizes(Smart(Dense(4, 36)), 2, 2); // 144 > 140

        [TestMethod]
        public void Smart_4Lines_ExactlyAtCap_StaysWhole() =>
            AssertSizes(Smart(Dense(4, 35)), 4); // 140 = cap, not over it

        [TestMethod]
        public void Smart_4Lines_UnderCap_StaysWhole_EvenWithRhymeAndRepeatedOpening()
        {
            // rhyming pair + a repeated opening would tempt a 2+2 split; the cap says it fits, so it must not split
            const string verse = "I walk in the light now\nI walk in the night now\nI walk in the dawn\nI walk in the day";
            Assert.IsTrue(verse.Replace("\n", "").Length <= 140);
            AssertSizes(Smart(verse), 4);
        }

        [TestMethod]
        public void Smart_3DenseLines_StayWhole_NoOrphan() =>
            AssertSizes(Smart(Dense(3, 60)), 3);

        [TestMethod]
        public void Smart_6DenseLines_SplitsFinerThanEvenThrees()
        {
            // 3 x 50 = 150 per slide is over the cap, so 3+3 loses to 2+2+2
            AssertSizes(Smart(Dense(6, 50)), 2, 2, 2);
        }

        [TestMethod]
        public void Smart_8DenseLines_NoSlideOverCapWhenAvoidable()
        {
            var r = Smart(Dense(8, 45));
            Assert.IsTrue(CharsPerSlide(r).All(c => c <= LyricReflow.SlideCharCap), string.Join(",", CharsPerSlide(r)));
            Assert.IsTrue(Sizes(r).All(s => s >= 2));
        }

        [TestMethod]
        public void Smart_ShortLines_UnaffectedByCap()
        {
            AssertSizes(Smart(Dense(8, 30)), 4, 4); // 120 per slide, under the cap
            AssertSizes(Smart(Dense(6, 30)), 3, 3);
        }

        [TestMethod]
        public void Smart_Dense_Idempotent_AndTextPreserved()
        {
            for (var n = 1; n <= 12; n++)
            {
                var input = Dense(n, 48);
                var once = Smart(input);
                Assert.AreEqual(once, Smart(once), "n=" + n);
                CollectionAssert.AreEqual(LyricReflow.NormalisedLines(input),
                    once.Split('\n').Where(l => l.Length > 0).ToList(), "n=" + n);
            }
        }

        [TestMethod]
        public void Smart_Idempotent()
        {
            for (var n = 0; n <= 12; n++)
            {
                var once = Smart(Lines(n));
                Assert.AreEqual(once, Smart(once), "n=" + n);
            }
        }

        [TestMethod]
        public void Smart_TextPreserved()
        {
            var input = Lines(9);
            CollectionAssert.AreEqual(LyricReflow.NormalisedLines(input),
                Smart(input).Split('\n').Where(l => l.Length > 0).ToList());
        }

        [TestMethod]
        public void Smart_OverwritesExistingBreaks() =>
            AssertSizes(Smart("a\nb\n\nc\nd\n\ne\nf"), 3, 3);

        [TestMethod]
        public void Rhymes_Basics()
        {
            Assert.IsTrue(LyricReflow.Rhymes("In the light", "Through the night"));
            Assert.IsFalse(LyricReflow.Rhymes("In the light", "Hand in hand"));
            Assert.IsFalse(LyricReflow.Rhymes("...", "night"));
            Assert.IsFalse(LyricReflow.Rhymes("", "night"));
        }

        [TestMethod]
        public void Fixed_InvalidN_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ReflowMode.Fixed(1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ReflowMode.Fixed(5));
        }
    }
}
