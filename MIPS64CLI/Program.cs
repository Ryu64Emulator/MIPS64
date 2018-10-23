using System;

namespace MIPS64CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
                throw new ArgumentException("Usage: [input file] [output file]");

            MIPS64.FileParser FP = new MIPS64.FileParser(args[0]);
            FP.AssembleAndCreateRaw(args[1]);
            Console.WriteLine("Done!");
        }
    }
}
