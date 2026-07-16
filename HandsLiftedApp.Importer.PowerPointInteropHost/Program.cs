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
        try
        {
            var pipeName = args.Length > 0 ? args[0] : "HandsLifted.PowerPointInterop";
            Console.Error.WriteLine($"PowerPointInteropHost connecting to pipe '{pipeName}'...");

            _pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            _pipe.Connect(15_000);

            Console.Error.WriteLine("Connected to pipe.");

            _reader = new StreamReader(_pipe);
            _writer = new StreamWriter(_pipe) { AutoFlush = true };

            // Start background listener
            var listenerTask = Task.Run(ListenLoop);

            // Block the main thread until the listener finishes
            listenerTask.Wait();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("PowerPointInteropHost crashed: " + ex);
        }
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
        ImportTask? cmd = null;
        try
        {
            cmd = JsonSerializer.Deserialize<ImportTask>(json);
            if (cmd == null)
                throw new InvalidOperationException("Deserialized ImportTask was null.");

            Console.Error.WriteLine($"Importing: {cmd.InputFile}");

            Converter.RunPowerPointImportTask(cmd, new ProgressReporter());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("PowerPoint import task failed: " + ex);
            SendToServer(new ImportStats
            {
                Task = cmd,
                JobStatus = ImportStats.JobStatusEnum.CompletionFailure,
                StatusMessage = ex.Message,
                CompletionTime = DateTime.Now
            });
        }
        finally
        {
            // delay to allow IPC comms to flush???
            Environment.Exit(0);
        }
    }
}