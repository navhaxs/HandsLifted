using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Extensions;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.Models.SlideState;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HandsLiftedApp.Utils
{
    public static class SongImporter
    {

        public static SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl> createSongItemFromTxt(string txtFilePath)
        {
            string text = File.ReadAllText(txtFilePath);

            List<string> parsed = new List<string>(text.Split("\n\n").Select(str => str.Trim()));

            SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl> song = new SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>()
            {
                Title = parsed.First().Trim(),
            };

            List<string> songParts = parsed.GetRange(1, parsed.Count - 1).ToList();


            foreach (var (songPart, index) in songParts.WithIndex())
            {
                var partNameEOL = songPart.IndexOf("\n");
                var partName = songPart.Substring(0, partNameEOL);

                string stanzaBody;
                if (index == songParts.Count - 1)
                {
                    string doubleNewLine = Environment.NewLine + Environment.NewLine;
                    var copyrightStart = songPart.LastIndexOf(doubleNewLine);
                    song.Copyright = songPart.Substring(copyrightStart)
                        .Replace("For use solely with the SongSelect® Terms of Use. All rights reserved. www.ccli.com\r\nNote: Reproduction of this sheet music requires a CCLI Music Reproduction License.  Please report all copies.\r\n", "")
                        .Trim();

                    var regex = new Regex(@" \(Admin. by(.*)\)\r\n");
                    song.Copyright = regex.Replace(song.Copyright, "\r\n");

                    stanzaBody = songPart.Substring(partNameEOL, copyrightStart - partNameEOL).Trim();
                }
                else
                {
                    stanzaBody = songPart.Substring(partNameEOL).Trim();
                }

                // TODO add line breaks (new slide) every 4 lines by default

                if (!stanzaBody.Contains(Environment.NewLine + Environment.NewLine)
                    && numLines(stanzaBody) > 6) {

                    List<string> stanzaParagraphs = new List<string>();
                    List<string> stanzaParagraph = new List<string>();
                    //foreach (var line in stanzaBody.Split(Environment.NewLine)) {
                    string[] lines = stanzaBody.Split(Environment.NewLine);
                    foreach (var (line, lineIdx) in lines.WithIndex()) {
                        if (stanzaParagraph.Count < 4 || stanzaParagraph.Last() == line || lineIdx == (lines.Count() - 1)) {
                            stanzaParagraph.Add(line);
                        }
                        else {
                            stanzaParagraphs.Add(string.Join(Environment.NewLine, stanzaParagraph));
                            stanzaParagraph.Clear();
                            stanzaParagraph.Add(line);
                        }
                    }

                    if (stanzaParagraph.Count > 0) {
                        stanzaParagraphs.Add(string.Join(Environment.NewLine, stanzaParagraph));
                        stanzaParagraph.Clear();
                    }


                    //var output = stanzaBody.Split(Environment.NewLine)
                    //    .Chunk(4)
                    //    .Select(chunk => string.Join(Environment.NewLine, chunk));

                    stanzaBody = string.Join(Environment.NewLine + Environment.NewLine, stanzaParagraphs);
                }

                song.Stanzas.Add(new SongStanza(Guid.NewGuid(), partName, stanzaBody));


            }

            song.ResetArrangement();

            return song;
        }

        private static int numLines(string input) {
            return ((input.Length - input.Replace(Environment.NewLine, string.Empty).Length) / Environment.NewLine.Length) + 1;
        }
    }
}
