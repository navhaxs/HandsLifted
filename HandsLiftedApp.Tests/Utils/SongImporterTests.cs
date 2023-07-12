namespace HandsLiftedApp.Utils.Tests
{
    [TestClass()]
    public class SongImporterTests
    {
        [TestMethod()]
        [DataRow("Intro")]
        [DataRow("Chorus")]
        [DataRow("Verse")]
        [DataRow("Tag")]
        [DataRow("Interlude")]
        [DataRow("Instrumental")]
        [DataRow("Bridge")]
        [DataRow("Ending")]
        [DataRow("Prechorus")]
        [DataRow("Pre chorus")]
        [DataRow("Pre Chorus")]
        [DataRow("Pre-chorus")]
        [DataRow("Pre-Chorus")]
        [DataRow("Refrain")]
        [DataRow("Outro")]
        [DataRow("Hook")]
        public void simpleValidPartNames(string validPartName)
        {
            Assert.IsTrue(SongImporter.isPartName(validPartName));
            Assert.IsTrue(SongImporter.isPartName(validPartName.ToUpper()));
        }

        // what about [Verse 1]
        // what about Verse 1:
        // capitalization
        [TestMethod()]
        [DataRow("Intro")]
        [DataRow("Chorus")]
        [DataRow("Verse")]
        [DataRow("Tag")]
        [DataRow("Interlude")]
        [DataRow("Instrumental")]
        [DataRow("Bridge")]
        [DataRow("Ending")]
        [DataRow("Prechorus")]
        [DataRow("PRECHORUS")]
        [DataRow("Pre chorus")]
        [DataRow("Pre Chorus")]
        [DataRow("PRE CHORUS")]
        [DataRow("Pre-chorus")]
        [DataRow("Pre-Chorus")]
        [DataRow("PRE-CHORUS")]
        [DataRow("Refrain")]
        [DataRow("Outro")]
        [DataRow("Hook")]
        public void complexValidPartNames(string validPartName)
        {
            Assert.IsTrue(SongImporter.isPartName($"{validPartName} 1"));
            Assert.IsTrue(SongImporter.isPartName($"{validPartName.ToUpper()} 1"));
            Assert.IsTrue(SongImporter.isPartName($"[{validPartName} 1]"));
            Assert.IsTrue(SongImporter.isPartName($"[{validPartName.ToUpper()} 1]"));
            Assert.IsTrue(SongImporter.isPartName($"{validPartName} 1:"));
            Assert.IsTrue(SongImporter.isPartName($"{validPartName.ToUpper()} 1:"));
        }

        [TestMethod()]
        public void invalidPartNames()
        {
            Assert.IsFalse(SongImporter.isPartName("My soul sings now my soul sings"));
            Assert.IsFalse(SongImporter.isPartName("Hallelujah"));
        }

        [TestMethod()]
        public void testCreateSongItemFromStringData()
        {
            string input = @"It Is Well With My Soul

Verse 1
When peace like a river
Attendeth my way
When sorrows like sea billows roll
Whatever my lot
Thou hast taught me to say
It is well
It is well with my soul

Chorus
It is well with my soul
It is well
It is well with my soul

Verse 2
Tho' Satan should buffet
Tho' trials should come
Let this blest assurance control
That Christ hath regarded
My helpless estate
And hath shed His own blood
For my soul

Verse 3
My sin O the bliss
Of this glorious tho't
My sin not in part but the whole
Is nailed to the cross
And I bear it no more
Praise the Lord
Praise the Lord O my soul

Verse 4
And Lord haste the day
When the faith shall be sight
The clouds be rolled back as a scroll
The trump shall resound
And the Lord shall descend
Even so it is well
With my soul

Horatio Gates Spafford, Philip Paul Bliss
CCLI Song #25376
Words: Public Domain; Music: Public Domain
For use solely with the SongSelect® Terms of Use.  All rights reserved. www.ccli.com
CCLI License #999999";
            Data.Models.Items.SongItem<Models.SlideState.SongTitleSlideStateImpl, Models.SlideState.SongSlideStateImpl, Models.ItemState.ItemStateImpl> songItem = SongImporter.createSongItemFromStringData(input);

            Assert.AreEqual(songItem.Title, "It Is Well With My Soul");
        }
    }
}