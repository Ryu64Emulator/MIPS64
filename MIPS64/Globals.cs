using System;
using System.Collections.Generic;
using System.Text;

namespace MIPS64
{
    public class Globals
    {
        private List<byte> Data;
        private Dictionary<string, uint>           GlobalLabels;
        private Dictionary<(string, string), uint> ChildLabels;
        private Dictionary<string, string>         UserMacros;
        private string CurrentLabel = "";

        public ushort BASE;

        public Globals()
        {
            Data         = new List<byte>();
            GlobalLabels = new Dictionary<string, uint>();
            ChildLabels  = new Dictionary<(string, string), uint>();
            UserMacros   = new Dictionary<string, string>();

            BASE = 0x0000;
        }

        public void AddInst(uint Inst)
        {
            byte[] InstArray = BitConverter.GetBytes(Inst);
            Array.Reverse(InstArray);

            Data.AddRange(InstArray);
        }

        public void AddGlobalLabel(string LabelName, uint Offset)
        {
            GlobalLabels.Add(LabelName, Offset);
            CurrentLabel = LabelName;
        }

        public void AddChildLabel(string Parent, string Child, uint Offset)
        {
            ChildLabels.Add((Parent, Child), Offset);
        }

        public void AddUserMacro(string Name, string Value)
        {
            UserMacros.Add(Name, Value);
        }

        public bool TryGetLabel(string LabelName, out uint Offset)
        {
            if (LabelName[0] == '.' && !string.IsNullOrWhiteSpace(GetCurrentLabel()))
                return ChildLabels.TryGetValue((GetCurrentLabel(), LabelName), out Offset);

            return GlobalLabels.TryGetValue(LabelName, out Offset);
        }

        public int GetDataCount()
        {
            return Data.Count;
        }

        public string GetCurrentLabel()
        {
            return CurrentLabel;
        }

        public List<byte> GetAllData()
        {
            return Data;
        }

        public Dictionary<string, string> GetUserMacros()
        {
            return UserMacros;
        }
    }
}
