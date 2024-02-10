using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using DynamicData;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Data.Models;
using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;

namespace HandsLiftedApp.Core
{
    public class XmlSerializerForDummies
    {
        // Serialize Playlist to XML
        public static void SerializePlaylist(PlaylistInstance playlist, string filePath)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(Playlist));

            using (FileStream stream = new FileStream(filePath, FileMode.Create))
            {
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
                    return item;
                    // return new Item
                    // {
                    //     Slides = item.Slides.ConvertAll(slide => new SlideSerialized()),
                    // };
                }));

                serializer.Serialize(stream, playlistSerialized);
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

                    var Items = new ObservableCollection<Item>();
                    foreach (var deserializedItem in deserialized.Items)
                    {
                        Items.Add(deserializedItem);
                    }
                    
                    // Map properties from PlaylistSerialized to Playlist
                    PlaylistInstance playlist = new PlaylistInstance
                    {
                        Title = deserialized.Title,
                        Meta = deserialized.Meta,
                        LogoGraphicFile = deserialized.LogoGraphicFile,
                        Designs = deserialized.Designs,
                        // Date = deserialized.Date,
                        Items = Items
                    };

                    return playlist;
                }
            }

            throw new NotImplementedException();
        }
    }
}