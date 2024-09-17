namespace AnoxAPE.Elements
{
    public class FlagsCommand : IWindowCommand
    {
        public enum FlagBit
        {
            Persist = 0,
            NoBackground = 1,
            NoScroll = 2,
            NoGrab = 3,
            NoRelease = 4,
            Subtitle = 5,
            Passive2D = 29,
            Passive = 30,
        }

        public uint Flags { get; private set; }

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Flags; } }

        public bool HasFlag(FlagBit flag)
        {
            uint mask = (uint)1 << (int)flag;
            return (Flags & mask) != 0;
        }

        public void ClearFlag(FlagBit flag)
        {
            uint mask = (uint)1 << (int)flag;
            Flags -= (Flags & mask);
        }

        public void SetFlag(FlagBit flag)
        {
            uint mask = (uint)1 << (int)flag;
            Flags |= mask;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            Flags = inStream.ReadUInt32();

            if (disasmStream != null)
            {
                disasmStream.WriteLineIndented(indent, "FlagsCommand");

                uint scanFlags = Flags;
                string flagsDesc = "";

                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.Persist, "persist");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.NoBackground, "nobackground");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.NoScroll, "noscroll");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.NoGrab, "nograb");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.NoRelease, "norelease");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.Subtitle, "subtitle");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.Passive2D, "passive2d");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.Passive, "passive");
                FlagUtil.AddUnknownFlags(scanFlags, ref flagsDesc);

                disasmStream.WriteIndent(indent + 1);
                disasmStream.WriteString($"Flags({flagsDesc})\n");
            }
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte(76);
            outStream.WriteUInt32(Flags);
        }
    }
}
