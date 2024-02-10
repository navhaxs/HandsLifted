using HandsLiftedApp.Data.Models.Items;
using HandsLiftedApp.Data.Slides;
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Avalonia.Logging;

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

        // todo scan this...
        static Type[] TYPES = new Type[] {
                      typeof(SongItem),
                      typeof(SlidesGroupItem),
                      typeof(LogoItem),
                      typeof(SectionHeadingItem),
                      typeof(GoogleSlidesGroupItem),
                      typeof(PowerPointSlidesGroupItem),
                      typeof(PDFSlidesGroupItem),
                      //
                      typeof(ImageSlide),
                      typeof(VideoSlide),
                  };

        /// <summary>
        /// Writes the given object instance to an XML file.
        /// <para>Only Public properties and variables will be written to the file. These can be any type though, even other classes.</para>
        /// <para>If there are public properties/variables that you do not want written to the file, decorate them with the [XmlIgnore] attribute.</para>
        /// <para>Object type must have a parameterless constructor.</para>
        /// </summary>
        /// <typeparam name="T">The type of object being written to the file.</typeparam>
        /// <param name="filePath">The file path to write the object instance to.</param>
        /// <param name="objectToWrite">The object instance to write to the file.</param>
        ///
        ///
        ///
        /// 
        public static void WriteToXmlFile<T>(string filePath, T objectToWrite) where T : new()
        {
            TextWriter writer = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T),
                    TYPES);
                //var serializer = new XmlSerializer(typeof(T),
                //    HandsLiftedApp.Data.Constants.Namespace);

                var settings = new XmlWriterSettings
                {
                    NewLineChars = "\n",
                    NewLineHandling = NewLineHandling.Replace,
                    Indent = true,
                };

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    writer = new StreamWriter(memoryStream);
                    using (XmlWriter xmlWriter = XmlWriter.Create(writer, settings))
                    {
                        serializer.Serialize(xmlWriter, objectToWrite);
                    }

                    // serialization was successful - only now do we write to disk
                    using (FileStream file = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                        memoryStream.WriteTo(file);
                }
            }
            catch (Exception ex)
            {
                // Logger
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }

        public static T ReadFromXmlFile<T>(Stream stream) where T : new()
        {
            try
            {
                var serializer = new XmlSerializer(typeof(T),
                    TYPES);

                XmlReaderSettings settings = new XmlReaderSettings()
                {
                    //Norm = false // for unix-newlines. may be undesired 
                };
                settings.Async = true;

                using (XmlReader xmlReader = XmlReader.Create(stream, settings))
                {
                    var x = (T)serializer.Deserialize(xmlReader);

                    return x;
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
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
                TYPES);

                stream = new StreamReader(filePath);

                XmlReaderSettings settings = new XmlReaderSettings()
                {
                    //Norm = false // for unix-newlines. may be undesired 
                };
                settings.Async = true;

                using (XmlReader xmlReader = XmlReader.Create(stream, settings))
                {
                    var x = (T)serializer.Deserialize(xmlReader);

                    return x;
                }
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }
        }
    }
}
