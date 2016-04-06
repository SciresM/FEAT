using System;
using System.IO;

namespace ctpktool
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("usage: {0} input.ctpk/input_folder", AppDomain.CurrentDomain.FriendlyName);
                Environment.Exit(0);
            }

            if (Directory.Exists(args[0]))
            {
                Ctpk.Create(args[0]);
            }
            else if (File.Exists(args[0]))
            {
                Ctpk.Read(args[0]);
            }
            else
            {
                Console.WriteLine("Could not find path or file '{0}'", args[0]);
            }
        }
    }
}
