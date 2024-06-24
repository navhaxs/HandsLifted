using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HandsLiftedApp.Core
{
    public static class SongImporter
    {
        // TODO make this config item
        public static readonly string[] PART_NAME_TOKENS =
        {
            "Intro",
            "Chorus",
            "Verse",
            "Tag",
            "Interlude",
            "Instrumental",
            "Bridge",
            "Ending",
            "Prechorus",
            "Pre chorus",
            "Pre-chorus",
            "Refrain",
            "Outro",
            "Hook",
            "Vamp",
            "Copyright"
        };

        public static bool isPartName(string input)
        {
            string processedInput = input.ToLower();

            if (processedInput.EndsWith(":"))
            {
                processedInput = processedInput[..^1];
            }

            if (processedInput.StartsWith("[") && processedInput.EndsWith("]"))
            {
                processedInput = processedInput[1..^1];
                return true;
            }

            return PART_NAME_TOKENS.Any(token =>
            {
                string processedToken = token.ToLower();
                var regex = new Regex(@$"^({processedToken})( \d)?$");

                return regex.Match(processedInput).Success;
            });
        }

        public static string stripPartName(string input)
        {
            var regex = new Regex(@"\[(.*)\]");
            var match = regex.Match(input);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return input;
        }

        public static SongItemInstance createSongItemFromTxtFile(string txtFilePath)
        {
            string text = File.ReadAllText(txtFilePath);
            return CreateSongItemFromStringData(text);
        }

        // note:
        // CRLF == \r\n
        // LF   == \n
        public static string NormalizeLineEndingsToCRLF(string text) =>
            text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

        public static SongItemInstance CreateSongItemFromStringData(string raw)
        {
            string text = NormalizeLineEndingsToCRLF(raw);
            List<string> parsed = new List<string>(text.Split("\r\n\r\n").Select(str => str.Trim()));

            SongItemInstance song = new SongItemInstance(Globals.Instance.MainViewModel?.Playlist)
            {
                Title = parsed.First().Trim(),
            };

            // todo: stanza builder
            string? lastStanzaBody = null;
            string? lastStanzaPartName = null;

            List<string> paragraphs = parsed.GetRange(1, parsed.Count - 1).ToList();

            foreach (var (paragraph, index) in paragraphs.WithIndex())
            {
                // get part name from first line
                var partNameEOL = paragraph.IndexOf("\r\n");

                if (partNameEOL == -1) //???
                {
                    if (isPartName(paragraph))
                    {
                        // flush existing builder
                        if (lastStanzaPartName != null && lastStanzaBody != null)
                        {
                            song.Stanzas.Add(createStanza(lastStanzaPartName, lastStanzaBody));
                        }

                        lastStanzaPartName = stripPartName(paragraph);
                        lastStanzaBody = "";
                    }
                    else
                    {
                        lastStanzaBody += "\r\n\r\n" + paragraph;
                    }
                }
                else
                {
                    var partName = paragraph.Substring(0, partNameEOL);

                    if (isPartName(partName))
                    {
                        // this is the start of a new song stanza
                        partName = stripPartName(partName);

                        // flush existing builder
                        if (lastStanzaPartName != null && lastStanzaBody != null)
                        {
                            if (lastStanzaPartName == "Copyright")
                            {
                                song.Copyright = lastStanzaBody;
                            }
                            else
                            {
                                song.Stanzas.Add(createStanza(lastStanzaPartName, lastStanzaBody));
                            }
                        }

                        lastStanzaPartName = partName;
                        lastStanzaBody = paragraph.Substring(partNameEOL).Trim();
                    }
                    else if (index == paragraphs.Count - 1 && (paragraph.Contains("CCLI") || paragraph.Contains("©")))
                    {
                        // this is the final paragraph AND includes CCLI/©
                        song.Copyright = paragraph.Trim();
                        var regex = new Regex(@" \(Admin. by(.*)\)\r\n");
                        song.Copyright = regex.Replace(song.Copyright, "\r\n");
                    }
                    else
                    {
                        if (lastStanzaPartName != null && lastStanzaBody != null)
                        {
                            lastStanzaBody = lastStanzaBody + "\r\n\r\n" + paragraph.Trim();
                        }
                        else
                        {
                            lastStanzaPartName = "Verse"; // ungrouped
                            lastStanzaBody = paragraph.Trim();
                        }
                    }
                }
            }

            // flush final
            if (lastStanzaPartName != null && lastStanzaBody != null)
            {
                if (lastStanzaPartName == "Copyright")
                {
                    song.Copyright = lastStanzaBody;
                }
                else
                {
                    song.Stanzas.Add(createStanza(lastStanzaPartName, lastStanzaBody));
                }
            }

            song.ResetArrangement();

            return song;
        }

        public static string songItemToFreeText(SongItemInstance songItem)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine(songItem.Title);
            stringBuilder.AppendLine("");
            foreach (var stanza in songItem.Stanzas)
            {
                stringBuilder.AppendLine($"[{stanza.Name}]");
                stringBuilder.AppendLine(stanza.Lyrics);
                stringBuilder.AppendLine("");
            }

            if (songItem.Copyright.Length > 0)
            {
                stringBuilder.AppendLine("[Copyright]");
                stringBuilder.AppendLine(songItem.Copyright);
            }

            return stringBuilder.ToString().Trim();
        }

        private static SongStanza createStanza(string partName, string stanzaBody)
        {
            // add line breaks (new slide) every 6 lines by default (hack)
            //if (!stanzaBody.Contains(Environment.NewLine + Environment.NewLine)
            //    && numLines(stanzaBody) > 6)
            //{

            //    List<string> stanzaParagraphs = new List<string>();
            //    List<string> stanzaParagraph = new List<string>();
            //    string[] lines = stanzaBody.Split(Environment.NewLine);
            //    foreach (var (line, lineIdx) in lines.WithIndex())
            //    {
            //        if (stanzaParagraph.Count < 4 || stanzaParagraph.Last() == line || lineIdx == (lines.Count() - 1))
            //        {
            //            stanzaParagraph.Add(line);
            //        }
            //        else
            //        {
            //            stanzaParagraphs.Add(string.Join(Environment.NewLine, stanzaParagraph));
            //            stanzaParagraph.Clear();
            //            stanzaParagraph.Add(line);
            //        }
            //    }

            //    if (stanzaParagraph.Count > 0)
            //    {
            //        stanzaParagraphs.Add(string.Join(Environment.NewLine, stanzaParagraph));
            //        stanzaParagraph.Clear();
            //    }

            //    stanzaBody = string.Join(Environment.NewLine + Environment.NewLine, stanzaParagraphs);
            //}

            return new SongStanza(Guid.NewGuid(), partName, stanzaBody);
        }

        private static int numLines(string input)
        {
            return ((input.Length - input.Replace(Environment.NewLine, string.Empty).Length) /
                    Environment.NewLine.Length) + 1;
        }
    }
}