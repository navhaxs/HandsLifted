using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using DynamicData;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;

namespace HandsLiftedApp.Core
{
    public class HandsLiftedDocXmlSerializer
    {
        // Serialize Playlist to XML
        public static void SerializePlaylist(PlaylistInstance playlist, string filePath)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Playlist));
            Playlist playlistSerialized = new Playlist
            {
                Title = playlist.Title,
                Meta = playlist.Meta,
                LogoGraphicFile = playlist.LogoGraphicFile,
                Designs = playlist.Designs,
                Items = new ObservableCollection<Item>()
            };

            // TODO: convert all 'instance/runtime' classes to 'serialized document' classes
            playlistSerialized.Items.AddRange(playlist.Items.ToList().ConvertAll(item =>
            {
                if (item is LogoItemInstance i)
                {
                    return new LogoItem() { Title = i.Title };
                }
                else if (item is SongItemInstance songItemInstance)
                {
                    return new SongItem()
                    {
                        UUID = songItemInstance.UUID,
                        Title = songItemInstance.Title,
                        Arrangement = songItemInstance.Arrangement,
                        Arrangements = songItemInstance.Arrangements,
                        SelectedArrangementId = songItemInstance.SelectedArrangementId,
                        Stanzas = songItemInstance.Stanzas,
                        Copyright = songItemInstance.Copyright,
                        Design = songItemInstance.Design,
                        StartOnTitleSlide = songItemInstance.StartOnTitleSlide,
                        EndOnBlankSlide = songItemInstance.EndOnBlankSlide
                    };
                }
                else if (item is SlidesGroupItemInstance slidesGroupItemInstance)
                {
                    return new SlidesGroupItem()
                    {
                        UUID = slidesGroupItemInstance.UUID,
                        Title = slidesGroupItemInstance.Title,
                    };
                }
                else if (item is MediaGroupItemInstance mediaGroupItemInstance)
                {
                    return new MediaGroupItem()
                    {
                        UUID = mediaGroupItemInstance.UUID,
                        Title = mediaGroupItemInstance.Title,
                        Items = mediaGroupItemInstance.Items
                    };
                }

                return item;
                // return new Item
                // {
                //     Slides = item.Slides.ConvertAll(slide => new SlideSerialized()),
                // };
            }));

            MemoryStream memoryStream = new MemoryStream();
            serializer.Serialize(memoryStream, playlistSerialized);

            // only once all serialization is OK, now write to disk
            memoryStream.Position = 0;
            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
            {
                memoryStream.CopyTo(fileStream);
            }
        }

        // Deserialize Playlist from XML
        public static PlaylistInstance DeserializePlaylist(string filePath)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Playlist));

            using (FileStream stream = new FileStream(filePath, FileMode.Open))
            {
                var x = serializer.Deserialize(stream);
                if (x != null)
                {
                    Playlist deserialized = (Playlist)x;
                    
                    // Map properties from PlaylistSerialized to Playlist
                    PlaylistInstance playlist = new PlaylistInstance
                    {
                        Title = deserialized.Title,
                        Meta = deserialized.Meta,
                        LogoGraphicFile = deserialized.LogoGraphicFile,
                        Designs = deserialized.Designs,
                        // Date = deserialized.Date,
                        // Items = Items
                    };


                    var Items = new ObservableCollection<Item>();
                    foreach (var deserializedItem in deserialized.Items)
                    {
                        Item convereted;
                        if (deserializedItem is LogoItem i)
                        {
                            convereted = new LogoItemInstance(playlist) { Title = i.Title };
                        }
                        else if (deserializedItem is SongItem songItem)
                        {
                            var song = new SongItemInstance(playlist)
                            {
                                UUID = songItem.UUID,
                                Title = songItem.Title,
                                Arrangement = songItem.Arrangement,
                                Arrangements = songItem.Arrangements,
                                SelectedArrangementId = songItem.SelectedArrangementId,
                                Stanzas = songItem.Stanzas,
                                Copyright = songItem.Copyright,
                                Design = songItem.Design,
                                StartOnTitleSlide = songItem.StartOnTitleSlide,
                                EndOnBlankSlide = songItem.EndOnBlankSlide
                            };
                            // song.GenerateSlides();
                            convereted = song;
                        }
                        else if (deserializedItem is MediaGroupItem mediaGroupItem)
                        {
                            var g = new MediaGroupItemInstance(playlist)
                            {
                                UUID = mediaGroupItem.UUID,
                                Title = mediaGroupItem.Title,
                                Items = mediaGroupItem.Items
                            };
                            g.GenerateSlides();
                            convereted = g;
                        }
                        else
                        {
                            convereted = deserializedItem;
                        }

                        Items.Add(convereted);
                    }

                    playlist.Items = Items;
                    
                    return playlist;
                }
            }

            throw new NotImplementedException();
        }
    }
}