using ProtoBuf;

namespace HandsLiftedApp.Importer.PowerPointInteropData;

[ProtoContract]
public class ImportTask
{
    [ProtoMember(1)]
    public string PPTXFilePath { get; set; }

    public string OutputDirectory
    {
        get
        {
            return Path.GetDirectoryName(PPTXFilePath);
        }
    }
}