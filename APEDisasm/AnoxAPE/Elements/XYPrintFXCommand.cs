using System.IO;
using System;

namespace AnoxAPE.Elements
{
    public class XYPrintFXCommand : IWindowCommand
    {
        // Can be emitted as <number>
        public OptionalExpression XCoord { get; private set; }
        public OptionalExpression YCoord { get; private set; }
        public OptionalExpression Alpha { get; private set; }
        public OptionalExpression Red { get; private set; }
        public OptionalExpression Green { get; private set; }
        public OptionalExpression Blue { get; private set; }
        public OptionalString Font { get; private set; }
        public ByteString Message { get; private set; }
        public OptionalExpression Condition { get; private set; }
        public FormattingValue FormattingValue { get; private set; }


        public WindowCommandType WindowCommandType { get { return WindowCommandType.XYPrintFX; } }

        public XYPrintFXCommand()
        {
            XCoord = new OptionalExpression();
            YCoord = new OptionalExpression();
            Alpha = new OptionalExpression();
            Red = new OptionalExpression();
            Green = new OptionalExpression();
            Blue = new OptionalExpression();
            Font = new OptionalString();
            Message = new ByteString();
            Condition = new OptionalExpression();
            FormattingValue = new FormattingValue();
        }


        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "XYPrintFXCommand");

            XCoord.Load(inStream, indent + 1, disasmStream);
            YCoord.Load(inStream, indent + 1, disasmStream);
            Alpha.Load(inStream, indent + 1, disasmStream);
            Red.Load(inStream, indent + 1, disasmStream);
            Green.Load(inStream, indent + 1, disasmStream);
            Blue.Load(inStream, indent + 1, disasmStream);
            Font.Load(inStream, indent + 1, disasmStream);
            Message.Load(inStream, indent + 1, disasmStream);
            Condition.Load(inStream, indent + 1, disasmStream);
            FormattingValue.Load(inStream, indent + 1, disasmStream);
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte(80);
            XCoord.Write(outStream);
            YCoord.Write(outStream);
            Alpha.Write(outStream);
            Red.Write(outStream);
            Green.Write(outStream);
            Blue.Write(outStream);
            Font.Write(outStream);
            Message.Write(outStream);
            Condition.Write(outStream);
            FormattingValue.Write(outStream);
        }
    }
}
