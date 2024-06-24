using System.Xml.Serialization;

namespace HandsLiftedApp.Data.Types
{
    public class FileReference
    {
        [XmlIgnore]
        public string FilePath { get; set; }
    }
}