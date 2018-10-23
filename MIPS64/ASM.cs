using System;
using System.Collections.Generic;

namespace MIPS64
{
    public class ASM
    {
        private Globals       globals;
        private OperandParser Parser;

        public static Dictionary<string, Inst>            Insts;
        public static Dictionary<string, (int, string[])> Macros;

        public struct Inst
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

        static ASM()
        {
            Insts  = new Dictionary<string, Inst>();
            Macros = new Dictionary<string, (int, string[])>();

            InitializeInsts();
            InitializeMacros();
        }

        public ASM(Globals globals)
        {
            this.globals = globals;
            Parser       = new OperandParser(globals);
        }

        public void ParseLine(string Line)
        {
            Line = Line.ToUpper();

            string[] words = Line.Split(' ');

            if (words.Length == 0 || string.IsNullOrWhiteSpace(words[0])
                    || IsLineComment(words) || IsLinePreP(words) || IsLineLabel(words))
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

        private bool IsLineComment(string[] words)
        {
            return words[0][0] == ';' || words[0][0] == '#' || (words[0].Length >= 2 && words[0][0] == '/' && words[0][1] == '/');
        }

        private bool IsLinePreP(string[] words)
        {
            return words[0].Length >= 2 && words[0][0] == '[' && words[words.Length - 1][words[words.Length - 1].Length - 1] == ']';
        }

        private bool IsLineLabel(string[] words)
        {
            return words[0].Length >= 2 && words[0][words[0].Length - 1] == ':';
        }

        private void ParseMacro(string[] words, string[] macroinsts, int ExpectedNumberOfArgs)
        {
            string ArgString = "";

            for (int i = 1; i < words.Length; ++i)
                ArgString += words[i];

            string[] Args;

            if (ArgString == "")
                Args = new string[] { };
            else
                Args = ArgString.Split(',');

            if (Args.Length != ExpectedNumberOfArgs) throw new ArgumentException($"Macro \"{words[0]}\" needs {ExpectedNumberOfArgs} arguments.");

            foreach (string inst in macroinsts)
                ParseLine(string.Format(inst, Args));
        }

        private void ParseInstruction(string[] words, Inst Desc)
        {
            uint Inst = Desc.BaseInst;

            List<object> OperandList = new List<object>();

            string ArgString = "";

            for (int i = 1; i < words.Length; ++i)
                ArgString += words[i];
            string[] Args;

            if (ArgString == "")
                Args = new string[] { };
            else
                Args = ArgString.Split(',');

            bool hasOffset = Desc.Args.Length >= 2 && Desc.Args[Desc.Args.Length - 2] == OperandParser.OperandType.Offset;

            int ArgsLength = !hasOffset ? Desc.Args.Length : (Desc.Args.Length - 1);

            if (Args.Length != ArgsLength)
                throw new ArgumentException($"Instruction \"{words[0]}\" needs {Desc.Args.Length} arguments.");

            for(int i = 0; i < ArgsLength; ++i)
                OperandList.Add(Parser.ParseOperand(Args[i], Desc.Args[i], Args, i));

            if (hasOffset) OperandList.Add(Parser.ParseOperand(Args[Args.Length - 1].Split('(')[1].Split(')')[0], OperandParser.OperandType.GPRegister));

            for (int i = 0; i < Desc.ArgTypes.Length; ++i)
            {
                switch (Desc.ArgTypes[i])
                {
                    case InstArgType.op1:
                        Inst |= (uint)((byte)OperandList[i] & 0x1F) << 21;
                        break;
                    case InstArgType.op2:
                        Inst |= (uint)((byte)OperandList[i] & 0x1F) << 16;
                        break;
                    case InstArgType.op3:
                        Inst |= (uint)((byte)OperandList[i] & 0x1F) << 11;
                        break;
                    case InstArgType.op4:
                        Inst |= (uint)((byte)OperandList[i] & 0x1F) << 6;
                        break;
                    case InstArgType.imm:
                        Inst |= (uint)((ushort)OperandList[i] & 0xFFFF);
                        break;
                    case InstArgType.target:
                        Inst |= (uint)OperandList[i] & 0xFFFFFF;
                        break;
                }
            }

            Console.WriteLine($"0x{globals.GetDataCount():x}: {Convert.ToString(Inst, 2).PadLeft(32, '0')}");

            globals.AddInst(Inst);
        }

        private uint TMP_InstCount = 0;

        private void UpdateInstructionCount(string[] words)
        {
            if (Insts.TryGetValue(words[0], out Inst inst))
                ++TMP_InstCount;
            else if (Macros.TryGetValue(words[0], out (int, string[]) Macro))
                TMP_InstCount += (uint)Macro.Item2.Length;
        }

        public void ParseLabel(string Label)
        {
            Label = Label.ToUpper();

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

        private static void InitializeInsts()
        {
            // Other Instructions
            Insts.Add("NOP", new Inst(0b00000000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { },
                                                                         new InstArgType[] { }));
            // Load / Store Instructions
            Insts.Add("LB", new Inst(0b10000000000000000000000000000000, new OperandParser.OperandType[] 
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LBU", new Inst(0b10010000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LD", new Inst(0b11011100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LDL", new Inst(0b01101000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LDR", new Inst(0b01101100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LH", new Inst(0b10000100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LHU", new Inst(0b10010100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LL", new Inst(0b11000000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LLD", new Inst(0b11010000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LW", new Inst(0b10001100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LWL", new Inst(0b10001000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LWR", new Inst(0b10011000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("LWU", new Inst(0b10011100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("SB", new Inst(0b10100000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("SC", new Inst(0b11100000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("SCD", new Inst(0b11110000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("SD", new Inst(0b11111100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("SDL", new Inst(0b10110000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("SDR", new Inst(0b10110100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("SH", new Inst(0b10100100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("SW", new Inst(0b10101100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("SWL", new Inst(0b10101000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("SWR", new Inst(0b10111000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Offset,
                                                                           OperandParser.OperandType.Base },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm, InstArgType.op1 }));
            Insts.Add("SYNC", new Inst(0b00000000000000000000000000001111, new OperandParser.OperandType[]
                                                                         { },
                                                                         new InstArgType[] { }));
            // Arithmetic Instructions
            Insts.Add("ADD", new Inst(0b00000000000000000000000000100000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("ADDU", new Inst(0b00000000000000000000000000100001, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("ADDI", new Inst(0b00100000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number16bit },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.op1, InstArgType.imm }));
            Insts.Add("ADDIU", new Inst(0b00100100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number16bit },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.op1, InstArgType.imm }));
            Insts.Add("AND", new Inst(0b00000000000000000000000000100100, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("ANDI", new Inst(0b00110000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number16bit },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.op1, InstArgType.imm }));
            Insts.Add("DADD", new Inst(0b00000000000000000000000000101100, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("DADDU", new Inst(0b00000000000000000000000000101101, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("DADDI", new Inst(0b01100000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number16bit },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.op1, InstArgType.imm }));
            Insts.Add("DADDIU", new Inst(0b01100100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number16bit },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.op1, InstArgType.imm }));
            Insts.Add("DDIV", new Inst(0b00000000000000000000000000011110, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op1, InstArgType.op2 }));
            Insts.Add("DDIVU", new Inst(0b00000000000000000000000000011111, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op1, InstArgType.op2 }));
            Insts.Add("DIV", new Inst(0b00000000000000000000000000011010, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op1, InstArgType.op2 }));
            Insts.Add("DIVU", new Inst(0b00000000000000000000000000011011, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op1, InstArgType.op2 }));
            Insts.Add("DMULT", new Inst(0b00000000000000000000000000011100, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op1, InstArgType.op2 }));
            Insts.Add("DMULTU", new Inst(0b00000000000000000000000000011101, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op1, InstArgType.op2 }));
            Insts.Add("DSLL", new Inst(0b00000000000000000000000000111000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number5bit },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op4 }));
            Insts.Add("DSLL32", new Inst(0b00000000000000000000000000111100, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number5bit },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op4 }));
            Insts.Add("DSLLV", new Inst(0b00000000000000000000000000010100, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op1 }));
            Insts.Add("DSRA", new Inst(0b00000000000000000000000000111011, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number5bit },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op4 }));
            Insts.Add("DSRA32", new Inst(0b00000000000000000000000000111111, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number5bit },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op4 }));
            Insts.Add("DSRAV", new Inst(0b00000000000000000000000000010111, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op1 }));
            Insts.Add("DSRL", new Inst(0b00000000000000000000000000111010, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number5bit },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op4 }));
            Insts.Add("DSRL32", new Inst(0b00000000000000000000000000111110, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number5bit },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op4 }));
            Insts.Add("DSRLV", new Inst(0b00000000000000000000000000010110, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op1 }));
            Insts.Add("DSUB", new Inst(0b00000000000000000000000000101110, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("DSUBU", new Inst(0b00000000000000000000000000101111, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("LUI",   new Inst(0b00111100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.Number16bit },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.imm }));
            Insts.Add("MFHI", new Inst(0b00000000000000000000000000010000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3 }));
            Insts.Add("MFLO", new Inst(0b00000000000000000000000000010010, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3 }));
            Insts.Add("MTHI", new Inst(0b00000000000000000000000000010001, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op1 }));
            Insts.Add("MTLO", new Inst(0b00000000000000000000000000010011, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op1 }));
            Insts.Add("MULT", new Inst(0b00000000000000000000000000011000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op1, InstArgType.op2 }));
            Insts.Add("MULTU", new Inst(0b00000000000000000000000000011001, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op1, InstArgType.op2 }));
            Insts.Add("NOR", new Inst(0b00000000000000000000000000100111, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("OR", new Inst(0b00000000000000000000000000100101, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("ORI", new Inst(0b00110100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number16bit },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.op1, InstArgType.imm }));
            Insts.Add("SLL", new Inst(0b00000000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number5bit },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op4 }));
            Insts.Add("SLLV", new Inst(0b00000000000000000000000000000100, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op1 }));
            Insts.Add("SLT", new Inst(0b00000000000000000000000000101010, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("SLTU", new Inst(0b00000000000000000000000000101011, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("SLTI", new Inst(0b00101000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number16bit },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.op1, InstArgType.imm }));
            Insts.Add("SLTIU", new Inst(0b00101100000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number16bit },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.op1, InstArgType.imm }));
            Insts.Add("SRA", new Inst(0b00000000000000000000000000000011, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number5bit },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op4 }));
            Insts.Add("SRAV", new Inst(0b00000000000000000000000000000111, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op1 }));
            Insts.Add("SRL", new Inst(0b00000000000000000000000000000010, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number5bit },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op4 }));
            Insts.Add("SRLV", new Inst(0b00000000000000000000000000000110, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op2, InstArgType.op1 }));
            Insts.Add("SUB", new Inst(0b00000000000000000000000000100010, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("SUBU", new Inst(0b00000000000000000000000000100011, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("XOR", new Inst(0b00000000000000000000000000100110, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op3, InstArgType.op1, InstArgType.op2 }));
            Insts.Add("XORI", new Inst(0b00111000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.Number16bit },
                                                                         new InstArgType[] { InstArgType.op2, InstArgType.op1, InstArgType.imm }));
            // Jump / Branch Instructions
            Insts.Add("BEQ", new Inst(0b00100000000000000000000000000000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister, OperandParser.OperandType.GPRegister,
                                                                           OperandParser.OperandType.BranchTarget },
                                                                         new InstArgType[] { InstArgType.op1, InstArgType.op2, InstArgType.imm }));
            Insts.Add("JR", new Inst(0b00000000000000000000000000001000, new OperandParser.OperandType[]
                                                                         { OperandParser.OperandType.GPRegister },
                                                                         new InstArgType[] { InstArgType.op1 }));
        }

        private static void InitializeMacros()
        {
            Macros.Add("B", (1, new string[] { "BEQ $R0, $R0, {0}" }));
            Macros.Add("GOTO", (2, new string[] 
            {
                "LUI {0}, BASE",
                "ADDIU {0}, {0}, {1}",
                "JR {0}"
            }));
            Macros.Add("CALL", (2, new string[] 
            {
                "LUI {0}, BASE",
                "ADDIU {0}, {0}, {1}",
                "JALR {0}"
            }));
            Macros.Add("RET",  (0, new string[] { $"JR $RA" }));
        }
    }
}
