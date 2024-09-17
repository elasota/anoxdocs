namespace AnoxAPE.Elements
{
    public class SimpleStringCommand : IWindowCommand
    {
        public enum ECommandType
        {
            StartConsoleCommand = 65,
            FontCommand = 70,
            FinishConsoleCommand = 78,
            NextWindowCommand = 79,
            StyleCommand = 87,
        }

        public ECommandType CommandType { get; private set; }
        public ByteString CommandStr { get; private set; }

        public WindowCommandType WindowCommandType { get { return WindowCommandType.SimpleStringCommand; } }

        public SimpleStringCommand(byte commandByte)
        {
            CommandType = (ECommandType)commandByte;
            CommandStr = new ByteString();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, CommandType.ToString());

            CommandStr.Load(inStream, indent + 1, disasmStream);
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte((byte)CommandType);
            CommandStr.Write(outStream);
        }
    }
}
