﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MIPS64
{
    public class FormatCreator
    {
        public enum Format
        {
            RAW,
            Z64
        }

        public static void CreateFormat(string Output, Format format, params Globals[] Globals)
        {
            switch (format)
            {
                case Format.RAW:
                    CreateRAW(Output, Globals[0]);
                    return;
                case Format.Z64:
                    CreateZ64(Output, Globals[0], Globals[1]);
                    return;
            }
        }

        private static void CreateZ64(string Output, Globals bootCodeGlob, Globals gameCodeGlob)
        {
            // TODO: Add a way to make .Z64 files
        }

        private static void CreateRAW(string Output, Globals globals)
        {
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
