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
        private static string AppDirectory { get; } = AppContext.BaseDirectory;
        private string LogDirectory { get; }
        private string LogDirectoryName { get; }
        public string LogFile => Path.Combine(LogDirectory, $"{DateTime.Today:dd-MM-yyyy}.txt");
        private bool IsNewDay => !File.Exists(LogFile);
        private static TextWriter Writer { get; set; }

        private static readonly object _writerLock = new object();

        public LogService(DiscordSocketClient client, CommandService cmdService)
        {
            if (FergunClient.IsDebugMode)
            {
                LogDirectoryName = "logs_debug";
            }
            else
            {
                LogDirectoryName = "logs";
            }
            LogDirectory = Path.Combine(AppDirectory, LogDirectoryName);

            // Create the log directory if it doesn't exist
            // What would happen if the folder is deleted while logging..?
            if (!Directory.Exists(LogDirectory)) 
                Directory.CreateDirectory(LogDirectory);

            if (IsNewDay)
            {
                CheckYesterday();
            }
            Writer = TextWriter.Synchronized(File.AppendText(LogFile));

            client.Log += LogAsync;
            cmdService.Log += LogAsync;
        }

        private void CheckYesterday()
        {
            string yesterday = Path.Combine(LogDirectory, $"{DateTime.Today.AddDays(-1):dd-MM-yyyy}.txt");
            // If the yesterday log file exists, compress it.
            if (File.Exists(yesterday))
            {
                _ = Task.Run(() => CompressAsync(yesterday));
            }
        }

        public async Task LogAsync(LogMessage message)
        {
            if (IsNewDay)
            {
                lock (_writerLock)
                {
                    Writer = TextWriter.Synchronized(File.AppendText(LogFile));
                }
                CheckYesterday();
            }

            string logText = $"[{message.Source}/{message.Severity}] {message}";

            lock (_writerLock)
            {
                Console.WriteLine(logText);
                Writer.WriteLine(logText);
                Writer.Flush();
                //await File.AppendAllTextAsync(LogFile, logText + "\n");
            }
            await Task.CompletedTask;
        }

        static private async Task CompressAsync(string FilePath)
        {
            using (var msi = new MemoryStream(await File.ReadAllBytesAsync(FilePath)))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    await msi.CopyToAsync(gs);
                }
                await File.WriteAllBytesAsync(Path.ChangeExtension(FilePath, "gz"), mso.ToArray());
            }
            File.Delete(FilePath);
        }
    }
}