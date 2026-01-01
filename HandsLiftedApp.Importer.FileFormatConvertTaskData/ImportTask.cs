using ProtoBuf;

namespace HandsLiftedApp.Importer.FileFormatConvertTaskData;

[ProtoContract]
public class ImportTask
{
    [ProtoMember(1)]
    public required string InputFile { get; set; }

    [ProtoMember(2)]
    public required string OutputDirectory { get; set; }

    [ProtoMember(3)]
    public required ExportFileFormatType ExportFileFormat { get; set; } = ExportFileFormatType.PNG;
    
    public enum ExportFileFormatType
    {
        PNG,
        PDF
    }
}