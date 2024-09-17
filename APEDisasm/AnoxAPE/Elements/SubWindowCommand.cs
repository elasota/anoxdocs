namespace AnoxAPE.Elements
{
    public class SubWindowCommand : IWindowCommand
    {
        public uint Label { get; private set; }

        public WindowCommandType WindowCommandType { get { return WindowCommandType.SubWindow; } }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            inStream.ExpectUInt32(0);
            inStream.ExpectUInt32(0);
            Label = inStream.ReadUInt32();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, $"SubWindowCommand({Label})");
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte(72);
            outStream.WriteUInt64(0);
            outStream.WriteUInt32(Label);
        }
    }
}
