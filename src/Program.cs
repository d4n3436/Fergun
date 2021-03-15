using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Fergun
{
    internal static class Program
    {
        public static async Task Main()
        {
            // Exceptions in english
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");

            Console.OutputEncoding = System.Text.Encoding.UTF8;

            await new FergunClient().InitializeAsync();
        }
    }
}