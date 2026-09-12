using System;
using System.IO;
using Serilog.Events;
using YamlDotNet.Serialization;

namespace HandsLiftedApp.Core
{
    // Loaded before Serilog is set up, so failures here can't go through Log.
    public class LoggingConfig
    {
        public string? SeqUrl { get; set; }
        public bool EnableLogConsole { get; set; } = true;
        public bool EnableLogFile { get; set; } = true;
        public LogEventLevel? LogLevel { get; set; }

        private static LoggingConfig? _instance;
        public static LoggingConfig Instance => _instance ??= Load();

        private static LoggingConfig Load()
        {
            try
            {
                Directory.CreateDirectory(Constants.APP_DATA_DIR);

                if (File.Exists(Constants.LOGGING_CONFIG_FILEPATH))
                {
                    var yaml = File.ReadAllText(Constants.LOGGING_CONFIG_FILEPATH);
                    var deserializer = new DeserializerBuilder().Build();
                    return deserializer.Deserialize<LoggingConfig>(yaml) ?? new LoggingConfig();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load logging.yml, using defaults: {ex}");
            }

            return new LoggingConfig();
        }
    }
}
