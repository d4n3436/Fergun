using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Services
{
    public class LogService
    {
        private static readonly string _appDirectory = AppContext.BaseDirectory;
        private readonly string _logDirectoryPath;
        private readonly string _logDirectoryName;
        private static TextWriter _writer;
        private static readonly object _writerLock = new object();

        public LogService(DiscordSocketClient client, CommandService cmdService)
        {
            _logDirectoryName = FergunClient.IsDebugMode ? "logs_debug" : "logs";
            _logDirectoryPath = Path.Combine(_appDirectory, _logDirectoryName);

            // Create the log directory if it doesn't exist
            // What would happen if the folder is deleted while logging..?
            if (!Directory.Exists(_logDirectoryPath))
                Directory.CreateDirectory(_logDirectoryPath);

            if (IsNewDay())
            {
                CheckYesterday();
            }
            _writer = TextWriter.Synchronized(File.AppendText(GetlogFile()));

            client.Log += LogAsync;
            cmdService.Log += LogAsync;
        }

        private void CheckYesterday()
        {
            string yesterday = Path.Combine(_logDirectoryPath, $"{DateTime.Today.AddDays(-1):dd-MM-yyyy}.txt");
            // If the yesterday log file exists, compress it.
            if (File.Exists(yesterday))
            {
                _ = Task.Run(() => CompressAsync(yesterday));
            }
        }

        private bool IsNewDay() => !File.Exists(GetlogFile());

        private string GetlogFile() => Path.Combine(_logDirectoryPath, $"{DateTime.Today:dd-MM-yyyy}.txt");

        public async Task LogAsync(LogMessage message)
        {
            if (IsNewDay())
            {
                lock (_writerLock)
                {
                    _writer = TextWriter.Synchronized(File.AppendText(GetlogFile()));
                }
                CheckYesterday();
            }

            string logText = $"[{message.Source}/{message.Severity}] {message}";

            lock (_writerLock)
            {
                switch (message.Severity)
                {
                    case LogSeverity.Critical:
                    case LogSeverity.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case LogSeverity.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case LogSeverity.Info:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    case LogSeverity.Verbose:
                    case LogSeverity.Debug:
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                }
                Console.WriteLine(logText);
                Console.ResetColor();
                _writer.WriteLine(logText);
                _writer.Flush();
                //await File.AppendAllTextAsync(LogFile, logText + "\n");
            }
            await Task.CompletedTask;
        }

        static private async Task CompressAsync(string filePath)
        {
            using (var msi = new MemoryStream(await File.ReadAllBytesAsync(filePath)))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    await msi.CopyToAsync(gs);
                }
                await File.WriteAllBytesAsync(Path.ChangeExtension(filePath, "gz"), mso.ToArray());
            }
            File.Delete(filePath);
        }
    }
}