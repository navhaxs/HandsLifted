using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Tasks;
using HandsLiftedApp.Importer.FileFormatConvertTaskData;

namespace PowerPointConverterSampleApp
{
    public static class NativePowerPointInteropService
    {
        public static event EventHandler? OnCompletion;
        
        private static Process? _helperProcess;

        public static void StartHelper()
        {
            Process[] existingProcesses = Process.GetProcessesByName("HandsLiftedApp.Importer.PowerPointInteropHost");
            if (existingProcesses.Length > 0)
            {
                _helperProcess = existingProcesses[0];
            }
            else
            {
                var debugPath = (Debugger.IsAttached)
                    ? "..\\..\\..\\..\\..\\HandsLiftedApp.Importer.PowerPointInteropHost\\bin\\Debug\\net8.0-windows"
                    : "";
                
                var helperPath = Path.Combine(
                    AppContext.BaseDirectory,
                    debugPath,
                    "HandsLiftedApp.Importer.PowerPointInteropHost.exe"
                );

                var psi = new ProcessStartInfo
                {
                    FileName = helperPath,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _helperProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            }

            _helperProcess.Exited += (s, e) =>
            {
                // Handle unexpected crash or exit
                Console.WriteLine("Helper process exited");
            };

            if (existingProcesses.Length == 0)
            {
                _helperProcess.Start();
            }
        }

        private static NamedPipeServerStream? _pipe;
        private static StreamWriter? _writer;

        public static async Task StartPipeServerAsync(ViewModel viewModel)
        {
            _pipe = new NamedPipeServerStream(
                "HandsLifted.PowerPointInterop",
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous
            );

            await _pipe.WaitForConnectionAsync();

            _writer = new StreamWriter(_pipe) { AutoFlush = true };

            _ = Task.Run(async () =>
            {
                using var reader = new StreamReader(_pipe);
                while (_pipe.IsConnected)
                {
                    var msg = await reader.ReadLineAsync();
                    if (msg != null)
                    {
                        HandleHelperMessage(msg, viewModel);
                    }
                }
            });
        }

        private static void HandleHelperMessage(string msg, ViewModel viewModel)
        {
            // JSON decode, update UI, etc.
            Console.WriteLine("Helper says: " + msg);
            try
            {
                var decoded = JsonSerializer.Deserialize<ImportStats>(msg);
                if (decoded != null)
                {
                    viewModel.ProgressStatusMessage = decoded.StatusMessage;
                    viewModel.ProgressValue = decoded.JobPercentage;
                    viewModel.Status = ApplicationState.Busy;
                    viewModel.OutputFilePath = decoded.OutputFilePath;

                    if (decoded.JobStatus == ImportStats.JobStatusEnum.CompletionSuccess)
                    {
                        OnCompletion?.Invoke(viewModel, EventArgs.Empty);
                    }
                }
            }
            catch (Exception)
            {
                
            }
        }

        public static void SendToHelper(object message)
        {
            var json = JsonSerializer.Serialize(message);
            _writer?.WriteLine(json);
        }
    }
}