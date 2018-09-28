using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MIPS64
{
    public class OperandParser
    {
        private Globals globals;
        private Dictionary<string, byte> RegisterLookup;

        public enum OperandType
        {
            Register,
            Number16bit,
            Number26bit,
            Base,
            Offset
        }

        public OperandParser(Globals globals)
        {
            this.globals = globals;

            RegisterLookup = new Dictionary<string, byte>();

            InitializeRegisterLookup();
        }

        private void InitializeRegisterLookup()
        {
            RegisterLookup.Add("$R0", 0);
            RegisterLookup.Add("$At", 1);
            RegisterLookup.Add("$V0", 2);
            RegisterLookup.Add("$V1", 3);
            RegisterLookup.Add("$A0", 4);
            RegisterLookup.Add("$A1", 5);
            RegisterLookup.Add("$A2", 6);
            RegisterLookup.Add("$A3", 7);
            RegisterLookup.Add("$T0", 8);
            RegisterLookup.Add("$T1", 9);
            RegisterLookup.Add("$T2", 10);
            RegisterLookup.Add("$T3", 11);
            RegisterLookup.Add("$T4", 12);
            RegisterLookup.Add("$T5", 13);
            RegisterLookup.Add("$T6", 14);
            RegisterLookup.Add("$T7", 15);
            RegisterLookup.Add("$S0", 16);
            RegisterLookup.Add("$S1", 17);
            RegisterLookup.Add("$S2", 18);
            RegisterLookup.Add("$S3", 19);
            RegisterLookup.Add("$S4", 20);
            RegisterLookup.Add("$S5", 21);
            RegisterLookup.Add("$S6", 22);
            RegisterLookup.Add("$S7", 23);
            RegisterLookup.Add("$T8", 24);
            RegisterLookup.Add("$T9", 25);
            RegisterLookup.Add("$K0", 26);
            RegisterLookup.Add("$K1", 27);
            RegisterLookup.Add("$GP", 28);
            RegisterLookup.Add("$SP", 29);
            RegisterLookup.Add("$S8", 30);
            RegisterLookup.Add("$RA", 31);
        }

        public decimal ParseOperand(string Operand, OperandType ExpectedType)
        {
            switch (ExpectedType)
            {
                case OperandType.Register:
                    return ParseRegister(Operand);
                case OperandType.Number16bit:
                    return ParseNumber16Bit(Operand);
                case OperandType.Number26bit:
                    return ParseNumber26Bit(Operand);
                case OperandType.Offset:
                    return ParseOffset(Operand);
            }

            throw new ArgumentException($"Expected type {ExpectedType.ToString()} got \"{Operand}\" instead.");
        }

        public ushort ParseOffset(string Offset)
        {
            string[] SplitOffset = Offset.Split('(');
            if (SplitOffset.Length < 2 || SplitOffset[1] == ")") throw new ArgumentException($"Can't parse offset \"{Offset}\".");

            return ParseNumber16Bit(SplitOffset[0]);
        }

        public byte ParseRegister(string Register)
        {
            if (RegisterLookup.TryGetValue(Register, out byte OutputValue))
                return OutputValue;
            throw new ArgumentException($"Expected type Register got \"{Register}\" instead.");
        }

        public ushort ParseNumber16Bit(string Number)
        {
            return (ushort)(ParseNumber(Number) & 0xFFFF);
        }

        public uint ParseNumber26Bit(string Number)
        {
            return ParseNumber(Number) & 0xFFFFFF;
        }

        public uint ParseNumber(string Number)
        {
            if (string.IsNullOrWhiteSpace(Number)) throw new ArgumentException("Number cannot be empty!");

            if (globals.TryGetLabel(Number, out uint Offset))
                return Offset;

            if (Number == "BASE")
                return globals.BASE;

            if (Number.Length >= 3)
            {
                if (Number.Substring(0, 2) == "0X")
                {
                    if (int.TryParse(Number.Substring(2), NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out int Res))
                        return (uint)Res;

                    throw new ArgumentException($"\"{Number}\" is not a valid Hex Number.");
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
                        throw new ArgumentException($"\"{Number}\" is not a valid Binary Number.");
                    }

                    return (uint)Res;
                }
            }
            else
            {
                if (int.TryParse(Number, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out int Res))
                    return (uint)Res;

                throw new ArgumentException($"\"{Number}\" is not a valid Integer Number.");
            }

            throw new ArgumentException($"Expected number, not \"{Number}\" (Perhaps you referenced a Label that doesn't exist?).");
        }
    }
}
