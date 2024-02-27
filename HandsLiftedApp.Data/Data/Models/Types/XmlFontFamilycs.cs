using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Avalonia.Media;

namespace HandsLiftedApp.Data.Data.Models.Types
{
    public class XmlFontFamily : IXmlSerializable
    {
        private FontFamily m_value = FontFamily.Parse("Arial");

        public XmlFontFamily() { }
        public XmlFontFamily(FontFamily source) { m_value = source; }

        public static implicit operator XmlFontFamily(FontFamily? o)
        {
            return o == null ? new XmlFontFamily() : new XmlFontFamily(o);
        }

        public static implicit operator FontFamily(XmlFontFamily o)
        {
            return o == null ? default(FontFamily) : o.m_value;
        }
        
        public void WriteXml(XmlWriter writer)
        {
            string fontFamilyAsString = m_value.ToString(); // convert Color to string
            writer.WriteString(fontFamilyAsString);
        }

        public void ReadXml(XmlReader reader)
        {
            string fontFamilyAsString = reader.ReadElementContentAsString();

            try
            {
                this.m_value = FontFamily.Parse(fontFamilyAsString);
            }
            catch (Exception e)
            {
                // Log
                this.m_value = FontFamily.DefaultFontFamilyName;
            }
        }

        public XmlSchema GetSchema()
        {
            return null;
        }
    }
}
