using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Fergun.Services
{
    public class LogService : IDisposable
    {
        private static readonly string _appDirectory = AppContext.BaseDirectory;
        private readonly string _logDirectoryPath;
        private static TextWriter _writer;
        private readonly object _writerLock = new object();
        private int _currentDay;
        private bool _disposed;

        public LogService()
        {
            string logDirectoryName = FergunClient.IsDebugMode ? "logs_debug" : "logs";
            _logDirectoryPath = Path.Combine(_appDirectory, logDirectoryName);

            // Create the log directory if it doesn't exist
            // What would happen if the folder is deleted while logging..?
            if (!Directory.Exists(_logDirectoryPath))
                Directory.CreateDirectory(_logDirectoryPath);

            _currentDay = DateTimeOffset.UtcNow.Day;
            _writer = TextWriter.Synchronized(File.AppendText(GetLogFile()));
            CompressYesterdayLogs();
        }

        public LogService(DiscordSocketClient client, CommandService cmdService) : this()
        {
            client.Log += LogAsync;
            cmdService.Log += LogAsync;
        }

        public async Task LogAsync(LogMessage message)
        {
            string logText = GetText(message);

            lock (_writerLock)
            {
                if (_currentDay != DateTimeOffset.UtcNow.Day)
                {
                    _currentDay = DateTimeOffset.UtcNow.Day;
                    _writer = TextWriter.Synchronized(File.AppendText(GetLogFile()));
                    CompressYesterdayLogs();
                }

                switch (message.Severity)
                {
                    case LogSeverity.Critical:
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        break;

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
            }
            await Task.CompletedTask;
        }

        private string GetLogFile() => Path.Combine(_logDirectoryPath, $"{DateTimeOffset.UtcNow:dd-MM-yyyy}.txt");

        private void CompressYesterdayLogs()
        {
            string yesterday = Path.Combine(_logDirectoryPath, $"{DateTimeOffset.UtcNow.AddDays(-1):dd-MM-yyyy}.txt");
            // If the yesterday log file exists, compress it.
            if (File.Exists(yesterday))
            {
                _ = CompressAsync(yesterday);
            }
        }

        private static async Task CompressAsync(string filePath)
        {
            await using (var msi = new MemoryStream(await File.ReadAllBytesAsync(filePath)))
            await using (var mso = new MemoryStream())
            {
                await using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    await msi.CopyToAsync(gs);
                }
                await File.WriteAllBytesAsync(Path.ChangeExtension(filePath, "gz"), mso.ToArray());
            }
            File.Delete(filePath);
        }

        private static string GetText(LogMessage message)
        {
            string msg = message.Message;
            string exMessage = message.Exception?.ToString();
            const int padWidth = 32;
            int capacity = 30 + padWidth + (msg?.Length ?? 0) + (exMessage?.Length ?? 0);

            var builder = new StringBuilder($"[{DateTimeOffset.UtcNow:HH:mm:ss}] [{message.Source}/{message.Severity}]".PadRight(padWidth), capacity);

            if (!string.IsNullOrEmpty(msg))
            {
                foreach (var ch in msg)
                {
                    //Strip control chars
                    if (!char.IsControl(ch))
                        builder.Append(ch);
                }
            }
            if (exMessage != null)
            {
                if (!string.IsNullOrEmpty(msg))
                {
                    builder.Append(':');
                    builder.AppendLine();
                }
                builder.Append(exMessage);
            }

            return builder.ToString();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(LogService), "Service has been disposed.");
            }

            if (!disposing) return;
            _writer.Dispose();
            _disposed = true;
        }
    }
}