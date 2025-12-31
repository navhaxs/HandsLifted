using ProtoBuf;

namespace HandsLiftedApp.Importer.PowerPointInteropData;

[ProtoContract]
public class ImportTask
{
    [ProtoMember(1)]
    public string pptxFile { get; set; }

    public string OutputDirectory
    {
        get
        {
            return Path.GetDirectoryName(pptxFile);
        }
    }

    [ProtoMember(2)]
    public ExportFileFormatType ExportFileFormat { get; set; } = ExportFileFormatType.PNG;
    
    public enum ExportFileFormatType
    {
        PNG,
        PDF
    }
}