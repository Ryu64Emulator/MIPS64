using System;
using System.Collections.Generic;
using System.Globalization;

namespace MIPS64
{
    public class OperandParser
    {
        private Globals globals;
        private Dictionary<string, byte> GPRegisterLookup;
        private Dictionary<string, byte> COP0RegisterLookup;

        public enum OperandType
        {
            GPRegister,
            COP0Register,
            Number16bit,
            Number26bit,
            Number5bit,
            BranchTarget,
            Base,
            Offset,
            StringWithSpaces,
            StringWithoutSpaces,
            StringOfAnythingAfter
        }

        public OperandParser(Globals globals)
        {
            this.globals = globals;

            GPRegisterLookup   = new Dictionary<string, byte>();
            COP0RegisterLookup = new Dictionary<string, byte>();

            InitializeRegisterLookup();
        }

        private void InitializeRegisterLookup()
        {
            // General Purpose Registers
            GPRegisterLookup.Add("$R0", 0);
            GPRegisterLookup.Add("$At", 1);
            GPRegisterLookup.Add("$V0", 2);
            GPRegisterLookup.Add("$V1", 3);
            GPRegisterLookup.Add("$A0", 4);
            GPRegisterLookup.Add("$A1", 5);
            GPRegisterLookup.Add("$A2", 6);
            GPRegisterLookup.Add("$A3", 7);
            GPRegisterLookup.Add("$T0", 8);
            GPRegisterLookup.Add("$T1", 9);
            GPRegisterLookup.Add("$T2", 10);
            GPRegisterLookup.Add("$T3", 11);
            GPRegisterLookup.Add("$T4", 12);
            GPRegisterLookup.Add("$T5", 13);
            GPRegisterLookup.Add("$T6", 14);
            GPRegisterLookup.Add("$T7", 15);
            GPRegisterLookup.Add("$S0", 16);
            GPRegisterLookup.Add("$S1", 17);
            GPRegisterLookup.Add("$S2", 18);
            GPRegisterLookup.Add("$S3", 19);
            GPRegisterLookup.Add("$S4", 20);
            GPRegisterLookup.Add("$S5", 21);
            GPRegisterLookup.Add("$S6", 22);
            GPRegisterLookup.Add("$S7", 23);
            GPRegisterLookup.Add("$T8", 24);
            GPRegisterLookup.Add("$T9", 25);
            GPRegisterLookup.Add("$K0", 26);
            GPRegisterLookup.Add("$K1", 27);
            GPRegisterLookup.Add("$GP", 28);
            GPRegisterLookup.Add("$SP", 29);
            GPRegisterLookup.Add("$S8", 30);
            GPRegisterLookup.Add("$RA", 31);

            // Co-Processor 0 Registers
            COP0RegisterLookup.Add("$INDEX",    0);
            COP0RegisterLookup.Add("$RANDOM",   1);
            COP0RegisterLookup.Add("$ENTRYLO0", 2);
            COP0RegisterLookup.Add("$ENTRYLO1", 3);
            COP0RegisterLookup.Add("$CONTEXT",  4);
            COP0RegisterLookup.Add("$PAGEMASK", 5);
            COP0RegisterLookup.Add("$WIRED",    6);
            COP0RegisterLookup.Add("$RSVD0",    7);
            COP0RegisterLookup.Add("$BADVADDR", 8);
            COP0RegisterLookup.Add("$COUNT",    9);
            COP0RegisterLookup.Add("$ENTRYHI",  10);
            COP0RegisterLookup.Add("$COMPARE",  11);
            COP0RegisterLookup.Add("$STATUS",   12);
            COP0RegisterLookup.Add("$CAUSE",    13);
            COP0RegisterLookup.Add("$EPC",      14);
            COP0RegisterLookup.Add("$PREVID",   15);
            COP0RegisterLookup.Add("$CONFIG",   16);
            COP0RegisterLookup.Add("$LLADDR",   17);
            COP0RegisterLookup.Add("$WATCHLO",  18);
            COP0RegisterLookup.Add("$WATCHHI",  19);
            COP0RegisterLookup.Add("$XCONTEXT", 20);
            COP0RegisterLookup.Add("$RSVD1",    21);
            COP0RegisterLookup.Add("$RSVD2",    22);
            COP0RegisterLookup.Add("$RSVD3",    23);
            COP0RegisterLookup.Add("$RSVD4",    24);
            COP0RegisterLookup.Add("$RSVD5",    25);
            COP0RegisterLookup.Add("$PERR",     26);
            COP0RegisterLookup.Add("$CACHEERR", 27);
            COP0RegisterLookup.Add("$TAGLO",    28);
            COP0RegisterLookup.Add("$TAGHI",    29);
            COP0RegisterLookup.Add("$ERROREPC", 30);
            COP0RegisterLookup.Add("$RSVD6",    31);
        }

        public object ParseOperand(string Operand, OperandType ExpectedType, string[] AllOperands = null, int CurrentPos = 0)
        {
            if (globals.GetUserMacros().TryGetValue(Operand, out string UMacro))
                return UMacro;

            switch (ExpectedType)
            {
                case OperandType.GPRegister:
                    return ParseGPRegister(Operand.ToUpper());
                case OperandType.COP0Register:
                    return ParseCOP0Register(Operand.ToUpper());
                case OperandType.Number16bit:
                    return ParseNumber16Bit(Operand.ToUpper());
                case OperandType.Number26bit:
                    return ParseNumber26Bit(Operand.ToUpper());
                case OperandType.Number5bit:
                    return ParseNumber5Bit(Operand.ToUpper());
                case OperandType.BranchTarget:
                    return ParseBranchTarget(Operand.ToUpper());
                case OperandType.Offset:
                    return ParseOffset(Operand.ToUpper());
                case OperandType.StringWithoutSpaces:
                    return Operand;
                case OperandType.StringWithSpaces:
                    if (AllOperands != null)
                    {
                        if (Operand[0] == '"')
                        {
                            string Result = "";
                            for (int i = CurrentPos; i < AllOperands.Length; ++i)
                            {
                                Result += AllOperands[i] + ' ';
                                if (AllOperands[i][AllOperands[i].Length - 1] == '"') break;
                            }

                            Result = Result.Substring(1, Result.Length - 3);
                            Result.Trim();

                            return Result;
                        }
                        else
                        {
                            throw new ArgumentException($"String expected, but \"{Operand}\" does not start with quotes, therefore, is not a String.");
                        }
                    }
                    else
                    {
                        throw new ArgumentException($"No \"AllOperands\" String array supplied with this \"ParseOperand\" method, therefore, it is impossible to parse a String.");
                    }
                case OperandType.StringOfAnythingAfter:
                    if (AllOperands != null)
                    {
                        string Result = "";
                        for (int i = CurrentPos; i < AllOperands.Length; ++i)
                            Result += AllOperands[i] + ' ';

                        Result.Trim();

                        return Result;
                    }
                    else
                    {
                        throw new ArgumentException($"No \"AllOperands\" String array supplied with this \"ParseOperand\" method, therefore, it is impossible to parse a String.");
                    }
            }

            throw new ArgumentException($"Expected type {ExpectedType.ToString()} got \"{Operand}\" instead.");
        }

        private ushort ParseOffset(string Offset)
        {
            string[] SplitOffset = Offset.Split('(');
            if (SplitOffset.Length < 2 || SplitOffset[1] == ")") throw new ArgumentException($"Can't parse offset \"{Offset}\".");

            return ParseNumber16Bit(SplitOffset[0]);
        }

        private byte ParseGPRegister(string Register)
        {
            if (GPRegisterLookup.TryGetValue(Register, out byte OutputValue))
                return OutputValue;
            throw new ArgumentException($"Expected type Register got \"{Register}\" instead.");
        }

        private byte ParseCOP0Register(string Register)
        {
            if (COP0RegisterLookup.TryGetValue(Register, out byte OutputValue))
                return OutputValue;
            throw new ArgumentException($"Expected type COP0 Register got \"{Register}\" instead.");
        }

        private ushort ParseBranchTarget(string Target)
        {
            if (string.IsNullOrWhiteSpace(Target)) throw new ArgumentException("Branch Target cannot be empty!");

            if (globals.TryGetLabel(Target, out uint Offset))
                return (ushort)((((int)Offset - globals.GetDataCount()) >> 2) & 0xFFFF);

            ushort Result;
            try
            {
                Result = (ushort)(ParseNumber(Target) & 0xFFFF);
                return Result;
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"{e.Message} (Perhaps you referenced a Label that doesn't exist?)");
            }
        }

        private ushort ParseNumber16Bit(string Number)
        {
            return (ushort)(ParseNumberWithLabelCheck(Number) & 0xFFFF);
        }

        private uint ParseNumber26Bit(string Number)
        {
            return ParseNumberWithLabelCheck(Number) & 0xFFFFFF;
        }

        private byte ParseNumber5Bit(string Number)
        {
            return (byte)(ParseNumberWithLabelCheck(Number) & 0x1F);
        }

        private uint ParseNumberWithLabelCheck(string Number)
        {
            if (string.IsNullOrWhiteSpace(Number)) throw new ArgumentException("Number cannot be empty!");

            if (globals.TryGetLabel(Number, out uint Offset))
                return Offset;

            uint Result;
            try
            {
                Result = ParseNumber(Number);
                return Result;
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"{e.Message} (Perhaps you referenced a Label that doesn't exist?)");
            }
        }

        private uint ParseNumber(string Number)
        {
            if (string.IsNullOrWhiteSpace(Number)) throw new FormatException("Number cannot be empty!");

            if (Number == "BASE")
                return globals.BASE;

            if (Number.Length >= 3)
            {
                if (Number.Substring(0, 2) == "0X")
                {
                    if (int.TryParse(Number.Substring(2), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out int Res))
                        return (uint)Res;

                    throw new FormatException($"\"{Number}\" is not a valid Hex Number.");
                }
                else if (Number.Substring(0, 2) == "0B")
                {
                    int Res = 0;

                    try
                    {
                        Res = Convert.ToInt32(Number.Substring(2), 2);
                    }
                    catch
                    {
                        throw new FormatException($"\"{Number}\" is not a valid Binary Number.");
                    }

                    return (uint)Res;
                }
            }
            else
            {
                if (int.TryParse(Number, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out int Res))
                    return (uint)Res;

                throw new FormatException($"\"{Number}\" is not a valid Integer Number.");
            }

            throw new ArgumentException($"Expected number, not \"{Number}\".");
        }
    }
}
