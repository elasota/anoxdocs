using System.IO;
using System;

namespace AnoxAPE.Elements
{
    public class TalkCommand : IWindowCommand
    {
        public ByteString Animation1 { get; private set; }
        public OptionalString Animation2 { get; private set; }
        public ByteString Name1 { get; private set; }
        public ByteString Name2 { get; private set; }
        public uint Stay1Flag { get; private set; }
        public uint Stay2Flag { get; private set; }

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Talk; } }

        public TalkCommand()
        {
            Animation1 = new ByteString();
            Animation2 = new OptionalString();
            Name1 = new ByteString();
            Name2 = new ByteString();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "TalkCommand");

            Animation1.Load(inStream, indent + 1, disasmStream);
            Animation2.Load(inStream, indent + 1, disasmStream);
            Name1.Load(inStream, indent + 1, disasmStream);
            Name2.Load(inStream, indent + 1, disasmStream);
            Stay1Flag = inStream.ReadUInt32();
            Stay2Flag = inStream.ReadUInt32();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent + 1, $"TalkParams(Stay1Flag={Stay1Flag},Stay2Flag={Stay2Flag})");
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte(89);
            Animation1.Write(outStream);
            Animation2.Write(outStream);
            Name1.Write(outStream);
            Name2.Write(outStream);
            outStream.WriteUInt32(Stay1Flag);
            outStream.WriteUInt32(Stay2Flag);
        }
    }
}
