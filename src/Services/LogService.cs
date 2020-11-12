﻿using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
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
        private readonly string _logDirectoryName;
        private static TextWriter _writer;
        private readonly object _writerLock = new object();
        private readonly Timer _timer;
        private bool _disposed;

        public LogService()
        {
            _logDirectoryName = FergunClient.IsDebugMode ? "logs_debug" : "logs";
            _logDirectoryPath = Path.Combine(_appDirectory, _logDirectoryName);

            // Create the log directory if it doesn't exist
            // What would happen if the folder is deleted while logging..?
            if (!Directory.Exists(_logDirectoryPath))
                Directory.CreateDirectory(_logDirectoryPath);

            _timer = new Timer(OnNewDay, null, GetNextMidnight(), Timeout.InfiniteTimeSpan);

            _ = CompressYesterdayLogsAsync();
            _writer = TextWriter.Synchronized(File.AppendText(GetLogFile()));
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

        private void OnNewDay(object state)
        {
            lock (_writerLock)
            {
                _writer = TextWriter.Synchronized(File.AppendText(GetLogFile()));
            }
            _ = CompressYesterdayLogsAsync();

            // Reset the timer for more accuracy.
            _timer.Change(GetNextMidnight(), Timeout.InfiniteTimeSpan);
        }

        private string GetLogFile() => Path.Combine(_logDirectoryPath, $"{DateTimeOffset.UtcNow:dd-MM-yyyy}.txt");

        private static TimeSpan GetNextMidnight() => DateTime.UtcNow.AddDays(1).Date - DateTime.UtcNow;

        private async Task CompressYesterdayLogsAsync()
        {
            string yesterday = Path.Combine(_logDirectoryPath, $"{DateTimeOffset.UtcNow.AddDays(-1):dd-MM-yyyy}.txt");
            // If the yesterday log file exists, compress it.
            if (File.Exists(yesterday))
            {
                await CompressAsync(yesterday);
            }
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

        private static string GetText(LogMessage message)
        {
            string msg = message.Message;
            string exMessage = message.Exception?.ToString();
            int padWidth = 32;
            int capacity = 30 + padWidth + (msg?.Length ?? 0) + (exMessage?.Length ?? 0);

            var builder = new StringBuilder($"[{DateTimeOffset.UtcNow:HH:mm:ss}] [{message.Source}/{message.Severity}]".PadRight(padWidth), capacity);

            if (!string.IsNullOrEmpty(msg))
            {
                for (int i = 0; i < msg.Length; i++)
                {
                    //Strip control chars
                    if (!char.IsControl(msg[i]))
                        builder.Append(msg[i]);
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
            else if (disposing)
            {
                _writer.Dispose();
                _writer = null;
                _timer.Dispose();
                _disposed = true;
            }
        }
    }
}