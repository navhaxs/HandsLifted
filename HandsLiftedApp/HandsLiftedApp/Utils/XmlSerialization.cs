using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Models.ItemExtensionState;
using HandsLiftedApp.Models.ItemState;
using HandsLiftedApp.Models.SlideState;
using SkiaSharp;
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace HandsLiftedApp.Utils
{
    /// <summary>
    /// Functions for performing common XML Serialization operations.
    /// <para>Only public properties and variables will be serialized.</para>
    /// <para>Use the [XmlIgnore] attribute to prevent a property/variable from being serialized.</para>
    /// <para>Object to be serialized must have a parameterless constructor.</para>
    /// </summary>
    public static class XmlSerialization
    {
        /// <summary>
        /// Writes the given object instance to an XML file.
        /// <para>Only Public properties and variables will be written to the file. These can be any type though, even other classes.</para>
        /// <para>If there are public properties/variables that you do not want written to the file, decorate them with the [XmlIgnore] attribute.</para>
        /// <para>Object type must have a parameterless constructor.</para>
        /// </summary>
        /// <typeparam name="T">The type of object being written to the file.</typeparam>
        /// <param name="filePath">The file path to write the object instance to.</param>
        /// <param name="objectToWrite">The object instance to write to the file.</param>
        /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
        public static void WriteToXmlFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T),
    new Type[] { typeof(SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>), typeof(SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>) });



                var settings = new XmlWriterSettings
                {
                    NewLineChars = "\n",
                    NewLineHandling = NewLineHandling.Replace,
                    Indent = true,
                };
                writer = new StreamWriter(filePath, append);
                using (XmlWriter xmlWriter = XmlWriter.Create(writer, settings))
                {
                    serializer.Serialize(xmlWriter, objectToWrite);
                }
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }

        /// <summary>
        /// Reads an object instance from an XML file.
        /// <para>Object type must have a parameterless constructor.</para>
        /// </summary>
        /// <typeparam name="T">The type of object to read from the file.</typeparam>
        /// <param name="filePath">The file path to read the object instance from.</param>
        /// <returns>Returns a new instance of the object read from the XML file.</returns>
        public static T ReadFromXmlFile<T>(string filePath) where T : new()
        {
            TextReader stream = null;
            try
            {




                var serializer = new XmlSerializer(typeof(T),
                  new Type[] { typeof(SongItem<SongTitleSlideStateImpl, SongSlideStateImpl, ItemStateImpl>), typeof(SlidesGroupItem<ItemStateImpl, ItemAutoAdvanceTimerStateImpl>) });

                stream = new StreamReader(filePath);

                XmlReaderSettings settings = new XmlReaderSettings()
                {
                    //Norm = false // for unix-newlines. may be undesired 
                };
                settings.Async = true;

                using (XmlReader xmlReader = XmlReader.Create(stream, settings))
                {
                    return (T)serializer.Deserialize(xmlReader);
                }
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }
        }
    }
}
