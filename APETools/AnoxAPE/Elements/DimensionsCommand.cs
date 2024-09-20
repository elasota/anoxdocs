// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public class DimensionsCommand : IWindowCommand
    {
        public OptionalExpression XPos { get; private set; }
        public OptionalExpression YPos { get; private set; }
        public OptionalExpression Width { get; private set; }
        public OptionalExpression Height { get; private set; }

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Dimensions; } }

        public DimensionsCommand()
        {
            XPos = new OptionalExpression();
            YPos = new OptionalExpression();
            Width = new OptionalExpression();
            Height = new OptionalExpression();
        }

        public DimensionsCommand(OptionalExpression xpos, OptionalExpression ypos, OptionalExpression width, OptionalExpression height)
        {
            XPos = xpos;
            YPos = ypos;
            Width = width;
            Height = height;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "DimensionsCommand");

            XPos.Load(inStream, indent + 1, disasmStream);
            YPos.Load(inStream, indent + 1, disasmStream);
            Width.Load(inStream, indent + 1, disasmStream);
            Height.Load(inStream, indent + 1, disasmStream);
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte(71);
            XPos.Write(outStream);
            YPos.Write(outStream);
            Width.Write(outStream);
            Height.Write(outStream);
        }
    }
}
