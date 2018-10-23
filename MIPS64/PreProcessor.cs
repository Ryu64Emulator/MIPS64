using System;
using System.Collections.Generic;
using System.Text;

namespace MIPS64
{
    public class PreProcessor
    {
        private Globals       globals;
        private OperandParser Parser;
        private Dictionary<string, PreP> PreProcessors;

        private delegate void MacroMethod(object[] Operands);

        private struct PreP
        {
            public OperandParser.OperandType[] Types;
            public MacroMethod Method;

            public PreP(OperandParser.OperandType[] Types, MacroMethod Method)
            {
                this.Types  = Types;
                this.Method = Method;
            }
        }

        public PreProcessor(Globals globals)
        {
            this.globals  = globals;
            Parser        = new OperandParser(globals);
            PreProcessors = new Dictionary<string, PreP>();
            InitializePreProcessors();
        }

        public void PreProcess(string Line)
        {
            if (string.IsNullOrWhiteSpace(Line) || (Line.Length >= 2 && (Line[0] != '[' || Line[Line.Length - 1] != ']'))) return;

            ParsePreProcessorMacro(Line);
        }

        private void ParsePreProcessorMacro(string PreP)
        {
            string PrePNameAndArgs = PreP.Substring(1, PreP.Length - 2);
            string[] words = PrePNameAndArgs.Split(' ');

            string ArgString = "";

            for (int i = 1; i < words.Length; ++i)
                ArgString += words[i];

            string[] Args;

            if (ArgString == "")
                Args = new string[] { };
            else
                Args = ArgString.Split(',');

            List<object> Operands = new List<object>();

            if (PreProcessors.TryGetValue(words[0].ToUpper(), out PreP PP))
            {
                if (Args.Length != PP.Types.Length) throw new ArgumentException($"The Pre-Processor \"{words[0]}\" requires {PP.Types.Length} arguments.");

                for (int i = 0; i < Args.Length; ++i)
                    Operands.Add(Parser.ParseOperand(Args[i], PP.Types[i], Args, i));

                PP.Method(Operands.ToArray());
            }
            else
            {
                throw new ArgumentException($"\"{words[0]}\" is not a valid Pre-Processor.");
            }
        }

        private void InitializePreProcessors()
        {
            PreProcessors.Add("BASE", new PreP(new OperandParser.OperandType[] { OperandParser.OperandType.Number16bit }, 
                (object[] op) => { globals.BASE = (ushort)op[0]; }));
            PreProcessors.Add("DEFINE", new PreP(new OperandParser.OperandType[] { OperandParser.OperandType.StringWithoutSpaces,
                                                                                   OperandParser.OperandType.StringOfAnythingAfter },
                (object[] op) => { globals.AddUserMacro((string)op[0], (string)op[1]); }));
        }
    }
}
