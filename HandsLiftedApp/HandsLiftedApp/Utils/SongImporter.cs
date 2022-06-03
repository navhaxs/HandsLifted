using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using HandsLiftedApp.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Utils
{
    public static class SongImporter
    {

        public static Song ImportSongFromTxt(string txtFilePath)
        {
            string text = System.IO.File.ReadAllText(txtFilePath);
            List<string> parsed = new List<string>(text.Split("\r\n\r\n\r\n").Select(str => str.Trim()));

            Song song = new Song()
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
                    song.Copyright = songPart.Substring(copyrightStart).Trim();

                    rest = songPart.Substring(partNameEOL, copyrightStart - partNameEOL).Trim();
                }
                else
                {
                    rest = songPart.Substring(partNameEOL).Trim();
                }

                song.Stanzas.Add(new Song.SongStanza(Guid.NewGuid(), partName, rest));

            }
            return song;
        }
    }
}
