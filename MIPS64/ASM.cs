using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace MIPS64
{
    public class ASM
    {
        private Globals       globals;
        private OperandParser Parser;

        private Dictionary<string, Inst>     Insts;
        private Dictionary<string, (int, string[])> Macros;

        private struct Inst
        {
            public uint BaseInst;
            public OperandParser.OperandType[] Args;
            public InstArgType[] ArgTypes;

            public Inst(uint BaseInst, OperandParser.OperandType[] Args, InstArgType[] ArgTypes)
            {
                this.BaseInst  = BaseInst;
                this.Args      = Args;
                this.ArgTypes  = ArgTypes;
            }
        }

        public enum InstArgType
        {
            op1,
            op2,
            op3,
            op4,
            imm,
            target
        }

        public ASM(Globals globals)
        {
            this.globals = globals;
            Parser       = new OperandParser(globals);
            Insts        = new Dictionary<string, Inst>();
            Macros       = new Dictionary<string, (int, string[])>();

            InitializeInsts();
            InitializeMacros();
        }

        public void ParseLine(string Line)
        {
            string[] words = Line.Split(' ');

            if (words.Length == 0 || string.IsNullOrWhiteSpace(words[0])
                    || words[0][0] == ';' || words[0][0] == '#' || (words[0].Length >= 2 && words[0][0] == '/' && words[0][1] == '/') 
                    || (words[0].Length >= 2 && words[0][0] == '[' && words[words.Length - 1][words[words.Length - 1].Length - 1] == ']')
                    || (words[0].Length >= 2 && words[0][words[0].Length - 1] == ':'))
                return;

            if (Insts.TryGetValue(words[0], out Inst inst))
            {
                // Instruction
                ParseInstruction(words, inst);
            }
            else if (Macros.TryGetValue(words[0], out (int, string[]) Macro))
            {
                // Macro
                ParseMacro(words, Macro.Item2, Macro.Item1);
            }
            else
            {
                throw new ArgumentException($"Failed to parse \"{Line}\".");
            }
        }

        private void ParseMacro(string[] words, string[] macroinsts, int ExpectedNumberOfArgs)
        {
            string ArgString = "";

            for (int i = 1; i < words.Length; ++i)
                ArgString += words[i];

            string[] Args = ArgString.Split(',');

            if (Args.Length != ExpectedNumberOfArgs) throw new ArgumentException($"Macro \"{words[0]}\" needs {ExpectedNumberOfArgs} arguments.");

            foreach (string inst in macroinsts)
                ParseLine(string.Format(inst, Args));
        }

        private void ParseInstruction(string[] words, Inst Desc)
        {
            uint Inst = Desc.BaseInst;

            List<decimal> OperandList = new List<decimal>();

            string ArgString = "";

            for (int i = 1; i < words.Length; ++i)
                ArgString += words[i];

            string[] Args = ArgString.Split(',');

            bool hasOffset = false;

            foreach (OperandParser.OperandType Type in Desc.Args)
            {
                if (Type == OperandParser.OperandType.Offset)
                {
                    hasOffset = true;
                    break;
                }
            }

            int ArgsLength = (!hasOffset) ? Desc.Args.Length : (Desc.Args.Length - 1);

            if (Args.Length != ArgsLength)
                throw new ArgumentException($"Instruction \"{words[0]}\" needs {Desc.Args.Length} arguments.");

            for(int i = 0; i < ArgsLength; ++i)
                OperandList.Add(Parser.ParseOperand(Args[i], Desc.Args[i]));

            if (hasOffset) OperandList.Add(Parser.ParseRegister(Args[Args.Length - 1].Split('(')[1].Split(')')[0]));

            for (int i = 0; i < Desc.ArgTypes.Length; ++i)
            {
                switch (Desc.ArgTypes[i])
                {
                    case InstArgType.op1:
                        Inst |= ((uint)OperandList[i] & 0x1F) << 21;
                        break;
                    case InstArgType.op2:
                        Inst |= ((uint)OperandList[i] & 0x1F) << 16;
                        break;
                    case InstArgType.op3:
                        Inst |= ((uint)OperandList[i] & 0x1F) << 11;
                        break;
                    case InstArgType.op4:
                        Inst |= ((uint)OperandList[i] & 0x1F) << 6;
                        break;
                    case InstArgType.imm:
                        Inst |= (uint)OperandList[i] & 0xFFFF;
                        break;
                    case InstArgType.target:
                        Inst |= (uint)OperandList[i] & 0xFFFFFF;
                        break;
                }
            }

            Console.WriteLine($"0x{globals.GetInstCount() * 4:x}: {Convert.ToString(Inst, 2).PadLeft(32, '0')}");

            globals.AddInst(Inst);
        }

        private uint TMP_InstCount = 0;

        private void UpdateInstructionCount(string[] words)
        {
            if (Insts.TryGetValue(words[0], out Inst inst))
            {
                ++TMP_InstCount;
            }
            else if (Macros.TryGetValue(words[0], out (int, string[]) Macro))
            {
                TMP_InstCount += (uint)Macro.Item2.Length;
            }
        }

        public void ParseLabel(string Label)
        {
            if (string.IsNullOrWhiteSpace(Label)) return;

            string[] words = Label.Split(' ');

            if (words.Length <= 0 || words[0][words[0].Length - 1] != ':')
            {
                UpdateInstructionCount(words);
                return;
            }

            if (words[0][0] == '.' && globals.GetCurrentLabel() != null)
                globals.AddChildLabel(globals.GetCurrentLabel(), words[0].Substring(0, words[0].Length - 1), TMP_InstCount * 4);
            else
                globals.AddGlobalLabel(words[0].Substring(0, words[0].Length - 1), TMP_InstCount * 4);
        }

        private void InitializeInsts()
        {
            Insts.Add("LB",    new Inst(0b10000000000000000000000000000000, new OperandParser.OperandType[] 
                                                                         { OperandParser.OperandType.Register, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LBU", new Inst(0b10010000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.Register, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LD", new Inst(0b11011100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.Register, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LDL", new Inst(0b01101000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.Register, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LDR", new Inst(0b01101100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.Register, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LH", new Inst(0b10000100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.Register, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LHU", new Inst(0b10010100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.Register, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LL", new Inst(0b11000000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.Register, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LLD", new Inst(0b11010000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.Register, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LW", new Inst(0b10001100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.Register, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LUI",   new Inst(0b00111100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.Register, OperandParser.OperandType.Number16bit },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm }));
            Insts.Add("ADDIU", new Inst(0b00100100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.Register, OperandParser.OperandType.Register,
                                                                           OperandParser.OperandType.Number16bit },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.op1, InstArgType.imm }));
            Insts.Add("JR",    new Inst(0b00000000000000000000000000001000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.Register },
                                                                         new InstArgType[] { InstArgType.op1 }));
        }

        private void InitializeMacros()
        {
            Macros.Add("GOTO", (2, new string[] {
                "LUI {0}, BASE",
                "ADDIU {0}, {0}, {1}",
                "JR {0}" }));
            Macros.Add("CALL", (2, new string[] {
                $"LUI {"{0}"}, {globals.BASE}",
                "ADDIU {0}, {0}, {1}",
                "JALR {0}" }));
            Macros.Add("RET",  (0, new string[] { $"JR $RA" }));
        }
    }
}
