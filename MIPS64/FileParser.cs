using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MIPS64
{
    public class FileParser
    {
        private Globals globals;
        private ASM     asm;
        private string  Filename;
        private PreProcessor PreP;

        public FileParser(string Filename)
        {
            globals = new Globals();
            asm     = new ASM(globals);
            PreP    = new PreProcessor(globals);
            this.Filename = Filename;
        }

        public void Assemble(string Output)
        {
            if (!File.Exists(Filename)) throw new FileNotFoundException($"The file \"{Filename}\" doesn't exist.");

            string[] fileLines = File.ReadAllLines(Filename);

            foreach (string Line in fileLines)
            {
                asm.ParseLabel(Line.ToUpper()); // Parse Labels
                PreP.PreProcess(Line.ToUpper()); // Pre Process
            }

            foreach (string Line in fileLines)
                asm.ParseLine(Line.ToUpper()); // Assemble

            byte[] Data = new byte[globals.GetInstCount() * 4];

            for (int i = 0; i < Data.Length; i += 4)
            {
                for (int j = 0; j < 4; ++j)
                {
                    byte[] Bytes = BitConverter.GetBytes(globals.GetAllInsts()[i / 4]);
                    Array.Reverse(Bytes);
                    Data[i + j] = Bytes[j];
                }
            }

            File.WriteAllBytes(Output, Data);
        }
    }
}
