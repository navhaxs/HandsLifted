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

            List<string> parsed = new List<string>(text.Split("\r\n\r\n\r\n").Select(str => str.Trim()));

            SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl> song = new SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>()
            {
                Title = parsed.First().Trim(),
            };

            List<string> songParts = parsed.GetRange(1, parsed.Count - 1).ToList();


            foreach (var (songPart, index) in songParts.WithIndex())
            {
                var partNameEOL = songPart.IndexOf(Environment.NewLine);
                var partName = songPart.Substring(0, partNameEOL);

                string rest;
                if (index == songParts.Count - 1)
                {
                    string doubleNewLine = Environment.NewLine + Environment.NewLine;
                    var copyrightStart = songPart.LastIndexOf(doubleNewLine);
                    song.Copyright = songPart.Substring(copyrightStart)
                        .Replace("For use solely with the SongSelect® Terms of Use. All rights reserved. www.ccli.com\r\nNote: Reproduction of this sheet music requires a CCLI Music Reproduction License.  Please report all copies.\r\n", "")
                        .Trim();

                    var regex = new Regex(@" \(Admin. by(.*)\)\r\n");
                    song.Copyright = regex.Replace(song.Copyright, "\r\n");

                    rest = songPart.Substring(partNameEOL, copyrightStart - partNameEOL).Trim();
                }
                else
                {
                    rest = songPart.Substring(partNameEOL).Trim();
                }

                // TODO add line breaks (new slide) every 4 lines by default

                song.Stanzas.Add(new SongStanza(Guid.NewGuid(), partName, rest));

            }

            song.ResetArrangement();

            return song;
        }
    }
}
