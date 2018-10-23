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

        public Globals GetGlobals()
        {
            return globals;
        }

        public void Include(string IncFilename, List<string> Lines)
        {
            if (!File.Exists(IncFilename)) throw new FileNotFoundException($"The included file \"{IncFilename}\" doesn't exist.");

            string[] fileLinesArray = File.ReadAllLines(IncFilename);

            List<string> fileLines = new List<string>();

            foreach (string Line in fileLinesArray)
            {
                if (string.IsNullOrWhiteSpace(Line)) continue;

                if (ParseInclude(Line, fileLines)) continue;

                fileLines.Add(Line);
            }

            Lines.AddRange(fileLines);
        }

        public void AssembleAndCreateRaw(string Output)
        {
            Assemble();

            FormatCreator.CreateFormat(Output, FormatCreator.Format.RAW, globals);
        }

        public byte[] Assemble()
        {
            if (!File.Exists(Filename)) throw new FileNotFoundException($"The file \"{Filename}\" doesn't exist.");

            string[] fileLinesArray = File.ReadAllLines(Filename);

            List<string> fileLines = new List<string>();

            foreach (string Line in fileLinesArray)
            {
                if (string.IsNullOrWhiteSpace(Line)) continue;

                if (ParseInclude(Line, fileLines)) continue;

                fileLines.Add(Line);
            }

            foreach (string Line in fileLines)
            {
                asm.ParseLabel(Line); // Parse Labels
                PreP.PreProcess(Line); // Pre Process
            }

            foreach (string Line in fileLines)
            {
                asm.ParseLine(Line); // Assemble
            }

            return globals.GetAllData().ToArray();
        }

        private bool ParseInclude(string Line, List<string> Lines)
        {
            if (Line[0] == '!')
            {
                string[] words = Line.Split(' ');

                string ArgString = "";
                for (int i = 1; i < words.Length; ++i)
                    ArgString += words[i];

                string[] Args;

                if (ArgString == "")
                    Args = new string[] { };
                else
                    Args = ArgString.Split(',');

                OperandParser OpParse = new OperandParser(globals);

                if (words[0].ToUpper() == "!INCLUDE")
                {
                    string FilePath = (string)OpParse.ParseOperand(Args[0], OperandParser.OperandType.StringWithSpaces, Args, 0);

                    Include(FilePath, Lines);
                    return true;
                }
            }
            return false;
        }
    }
}
