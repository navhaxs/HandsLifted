using System;
using System.IO;
using System.IO.Pipes;
using HandsLiftedApp.Importer.PowerPoint;
using HandsLiftedApp.Importer.PowerPointInteropData;
using NetOffice.Diagnostics;
using ProtoBuf;

namespace HandsLiftedApp.Importer.PowerPointInteropHost;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        while (true)
        {
            using (var server = new NamedPipeServerStream("MyPipe"))
            {
                Console.WriteLine("Waiting for connection...");
                server.WaitForConnection();

                var x = Serializer.Deserialize<ImportTask>(server);

                System.Diagnostics.Debug.Print(x.PPTXFilePath);

                Converter.RunPowerPointImportTask(null, x);
                
                using (var client = new NamedPipeClientStream(".", "MyPipeResult", PipeDirection.Out))
                {
                    client.Connect();

                    ImportResult importResult = new ImportResult();
                    Serializer.Serialize(client, importResult);
                }
            }
        }
    }
}