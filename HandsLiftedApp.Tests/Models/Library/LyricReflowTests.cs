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

        [TestMethod]
        public void Fixed_InvalidN_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ReflowMode.Fixed(1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ReflowMode.Fixed(5));
        }
    }
}
