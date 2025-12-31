using ProtoBuf;

namespace HandsLiftedApp.Importer.PowerPointInteropData
{
    [ProtoContract]
    public class ImportStats
    {
        [ProtoMember(1)]
        public ImportTask Task { get; set; }
        
        [ProtoMember(2)]
        public double JobPercentage { get; set; }

        [ProtoMember(3)]
        public JobStatusEnum JobStatus { get; set; }

        [ProtoMember(4)]
        public DateTime CompletionTime { get; set; }

        [ProtoMember(5)]
        public string FileName { get; set; }

        public enum JobStatusEnum
        {
            Running,
            CompletionSuccess,
            CompletionFailure,
        }
    }
}