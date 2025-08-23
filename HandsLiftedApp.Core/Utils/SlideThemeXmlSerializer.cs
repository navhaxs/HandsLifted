using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using HandsLiftedApp.Data.SlideTheme;
using Serilog;

namespace HandsLiftedApp.Core.Utils
{
    /// <summary>
    /// Utility helper for serializing/deserializing BaseSlideTheme to/from XML with error handling.
    /// </summary>
    public static class SlideThemeXmlSerializer
    {
        private static readonly XmlSerializer ThemeSerializer = new XmlSerializer(typeof(BaseSlideTheme));

        /// <summary>
        /// Serialize a BaseSlideTheme instance to the provided stream as XML.
        /// </summary>
        /// <param name="theme">Theme instance to serialize.</param>
        /// <param name="output">Writable stream to receive XML.</param>
        /// <returns>True if successful; false if an XML-related error occurred.</returns>
        public static async Task<bool> TrySerializeAsync(BaseSlideTheme theme, Stream output)
        {
            try
            {
                // XmlWriter for async-friendly write; XmlSerializer itself is sync, so Wrap in Task.Run if needed.
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    Async = true,
                    OmitXmlDeclaration = false,
                    NewLineOnAttributes = false
                };

                using (var writer = XmlWriter.Create(output, settings))
                {
                    ThemeSerializer.Serialize(writer, theme);
                    // Ensure writer flushes to the underlying stream
                    await writer.FlushAsync();
                }
                return true;
            }
            catch (InvalidOperationException ex)
            {
                // XmlSerializer wraps many XML issues in InvalidOperationException
                Log.Error(ex, "Failed to serialize BaseSlideTheme to XML");
                return false;
            }
            catch (XmlException ex)
            {
                Log.Error(ex, "XML error during BaseSlideTheme serialization");
                return false;
            }
        }

        /// <summary>
        /// Deserialize a BaseSlideTheme from the provided stream containing XML.
        /// </summary>
        /// <param name="input">Readable stream containing XML.</param>
        /// <param name="theme">On success, the deserialized theme; otherwise null.</param>
        /// <returns>True if successful; false if an XML-related error occurred.</returns>
        public static bool TryDeserialize(Stream input, out BaseSlideTheme? theme)
        {
            theme = null;
            try
            {
                var obj = ThemeSerializer.Deserialize(input);
                theme = obj as BaseSlideTheme;
                if (theme == null)
                {
                    Log.Error("Deserialized XML did not produce a BaseSlideTheme instance");
                    return false;
                }
                return true;
            }
            catch (InvalidOperationException ex)
            {
                Log.Error(ex, "Failed to deserialize BaseSlideTheme from XML");
                return false;
            }
            catch (XmlException ex)
            {
                Log.Error(ex, "XML error during BaseSlideTheme deserialization");
                return false;
            }
        }
    }
}
