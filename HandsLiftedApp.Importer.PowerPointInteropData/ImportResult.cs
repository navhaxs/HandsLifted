using ProtoBuf;

namespace HandsLiftedApp.Importer.PowerPointInteropData
{
    [ProtoContract]
    public class ImportResult
    {
        [ProtoMember(1)]
        public int Progress { get; set; }
        [ProtoMember(2)]
        public string Status { get; set; }
        [ProtoMember(3)]
        public string Error { get; set; }
        
        public override string ToString()
        {
            return Progress + " " + Status;
        }
    }
}