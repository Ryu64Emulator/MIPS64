using System;
using System.Collections.Generic;
using System.Text;

namespace MIPS64
{
    public class Globals
    {
        private List<uint> Instructions;
        private Dictionary<string, uint>           GlobalLabels;
        private Dictionary<(string, string), uint> ChildLabels;
        private string CurrentLabel = "";

        public ushort BASE;

        public Globals()
        {
            Instructions    = new List<uint>();
            GlobalLabels    = new Dictionary<string, uint>();
            ChildLabels     = new Dictionary<(string, string), uint>();

            BASE = 0x0000;
        }

        public void AddInst(uint Inst)
        {
            Instructions.Add(Inst);
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

        public bool TryGetLabel(string LabelName, out uint Offset)
        {
            if (LabelName[0] == '.')
                return ChildLabels.TryGetValue((GetCurrentLabel(), LabelName), out Offset);

            return GlobalLabels.TryGetValue(LabelName, out Offset);
        }

        public int GetInstCount()
        {
            return Instructions.Count;
        }

        public string GetCurrentLabel()
        {
            return CurrentLabel;
        }

        public List<uint> GetAllInsts()
        {
            return Instructions;
        }
    }
}
