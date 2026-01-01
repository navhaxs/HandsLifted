using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Tasks;
using HandsLiftedApp.Importer.PowerPoint;
using HandsLiftedApp.Importer.FileFormatConvertTaskData;

namespace HandsLiftedApp.Importer.PowerPointInteropHost;

class Program
{
    private static NamedPipeClientStream? _pipe;
    private static StreamReader? _reader;
    private static StreamWriter? _writer;

    [STAThread]
    public static void Main(string[] args)
    {
        _pipe = new NamedPipeClientStream(".", "HandsLifted.PowerPointInterop", PipeDirection.InOut);
        _pipe.Connect();

        _reader = new StreamReader(_pipe);
        _writer = new StreamWriter(_pipe) { AutoFlush = true };

        // Start background listener
        var listenerTask = Task.Run(ListenLoop);

        // Block the main thread until the listener finishes
        listenerTask.Wait();
    }
    
    public static void SendToServer(object message)
    {
        var json = JsonSerializer.Serialize(message);
        _writer?.WriteLine(json);
    }

    private static async Task ListenLoop()
    {
        while (_pipe!.IsConnected)
        {
            var msg = await _reader!.ReadLineAsync();
            if (msg == null)
                break;

            HandleMainAppCommand(msg);
        }
    }

    public class ProgressReporter : IProgress<ImportStats>
    {
        public void Report(ImportStats value)
        {
            SendToServer(value);
        }
    }

    private static void HandleMainAppCommand(string json)
    {
        var cmd = JsonSerializer.Deserialize<ImportTask>(json);

        System.Diagnostics.Debug.Print(cmd.InputFile);

        Converter.RunPowerPointImportTask(cmd, new ProgressReporter());

        // delay to allow IPC comms to flush???
        Environment.Exit(0);
    }
}