using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Avalonia.Media;
using Newtonsoft.Json;

namespace HandsLiftedApp.Data.Data.Models.Types
{
    [JsonConverter(typeof(XmlFontWeightJsonConverter))]
    public class XmlFontWeight : IXmlSerializable
    {
        private FontWeight m_value = FontWeight.Regular;

        public XmlFontWeight()
        {
        }

        public XmlFontWeight(FontWeight source)
        {
            m_value = source;
        }

        // public static implicit operator XmlFontWeight(FontWeight o)
        // {
        //     return o == null ? new XmlFontWeight() : new XmlFontWeight(o);
        // }

        public static explicit operator XmlFontWeight(FontWeight o)
        {
            return o == null ? new XmlFontWeight() : new XmlFontWeight(o);
        }

        // Add a conversion from XmlFontWeight to FontWeight if needed
        public static implicit operator FontWeight(XmlFontWeight xmlFontWeight)
        {
            return xmlFontWeight.m_value;
        }
        
        // Add a conversion from XmlFontWeight to int if needed
        public static implicit operator int(XmlFontWeight xmlFontWeight)
        {
            return (int)xmlFontWeight.m_value;
        }

        public static implicit operator XmlFontWeight(int fontWeightAsNumber)
        {
            try
            {
                return new XmlFontWeight((FontWeight)fontWeightAsNumber);
            }
            catch (Exception e)
            {
                // Log
                return new XmlFontWeight(FontWeight.Normal);
            }
        }

        public static implicit operator XmlFontWeight(Int64 fontWeightAsNumber)
        {
            try
            {
                return new XmlFontWeight((FontWeight)fontWeightAsNumber);
            }
            catch (Exception e)
            {
                // Log
                return new XmlFontWeight(FontWeight.Normal);
            }
        }

        public static implicit operator XmlFontWeight(string fontWeightAsNumber)
        {
            try
            {
                Enum.TryParse(fontWeightAsNumber, out FontWeight asFontWeight);
                return new XmlFontWeight(asFontWeight);
            }
            catch (Exception e)
            {
                // Log
                return new XmlFontWeight(FontWeight.Normal);
            }
        }

        // public static implicit operator FontWeight(XmlFontWeight o)
        // {
        //     return o == null ? default(FontWeight) : o.m_value;
        // }

        public static implicit operator string(XmlFontWeight o)
        {
            return o == null ? default(FontWeight).ToString() : o.m_value.ToString();
        }

        // public static implicit operator XmlFontWeight(FontWeight x)
        // {  return new XmlFontWeight(x); }

        public override string ToString()
        {
            return m_value.ToString();
        }

        public void WriteXml(XmlWriter writer)
        {
            string asString = m_value.ToString(); // convert FontWeight to string
            writer.WriteString(asString);
        }

        public void ReadXml(XmlReader reader)
        {
            string asString = reader.ReadElementContentAsString();

            try
            {
                int fontWeightAsNumber;
                bool isNumeric = int.TryParse(asString, out fontWeightAsNumber);

                if (isNumeric)
                {
                    this.m_value = (FontWeight)fontWeightAsNumber;
                }
                else
                {
                    Enum.TryParse(asString, out FontWeight asFontWeight);
                    this.m_value = asFontWeight;
                }
            }
            catch (Exception e)
            {
                this.m_value = FontWeight.Normal;
            }
        }

        public XmlSchema GetSchema()
        {
            return null;
        }
    }
}