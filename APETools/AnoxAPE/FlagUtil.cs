// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE
{
    internal class FlagUtil
    {
        public static void DetectFlag(ref uint flags, ref string flagsList, int flagPos, string flagName)
        {
            uint mask = ((uint)1) << flagPos;
            uint maskedFlag = flags & mask;
            if (maskedFlag != 0)
            {
                flags -= maskedFlag;
                if (flagsList.Length > 0)
                    flagsList += ",";
                flagsList += flagName;
            }
        }

        public static void AddUnknownFlags(uint flags, ref string flagsList)
        {
            int flagPos = 0;
            while (flags != 0)
            {
                if ((flags & 1) != 0)
                {
                    if (flagsList.Length > 0)
                        flagsList += ",";
                    flagsList += $"UnknownFlag{flagPos}";
                }

                flags = flags >> 1;
                flagPos++;
            }
        }
    }
}
