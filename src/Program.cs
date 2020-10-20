using System;
using System.Globalization;
using System.Threading;

namespace Fergun
{
    internal class Program
    {
        public static void Main()
        {
            // Exceptions in english
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            new FergunClient().InitializeAsync().GetAwaiter().GetResult();
        }
    }
}