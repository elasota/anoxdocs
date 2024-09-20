// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public class ConditionalFormattedStringCommand : IWindowCommand
    {
        public enum ECommandType
        {
            Invalid = 0,

            BodyCommand = 66,
            TitleCommand = 84,
        }

        public OptionalExpression Condition { get; private set; }
        public ByteString Text { get; private set; }
        public FormattingValue FormattingValue { get; private set; }
        public ECommandType CommandType { get; private set; }

        public WindowCommandType WindowCommandType { get { return WindowCommandType.ConditionalFormattedStringCommand; } }

        public ConditionalFormattedStringCommand(byte commandByte)
        {
            CommandType = (ECommandType)commandByte;
            Condition = new OptionalExpression();
            Text = new ByteString();
            FormattingValue = new FormattingValue();
        }

        public ConditionalFormattedStringCommand(ECommandType commandType, OptionalExpression condition, ByteString text, FormattingValue formattingValue)
        {
            CommandType = commandType;
            Condition = condition;
            Text = text;
            FormattingValue = formattingValue;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, CommandType.ToString());

            Condition.Load(inStream, indent + 1, disasmStream);
            Text.Load(inStream, indent + 1, disasmStream);
            FormattingValue.Load(inStream, indent + 1, disasmStream);
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte((byte)CommandType);
            Condition.Write(outStream);
            Text.Write(outStream);
            FormattingValue.Write(outStream);
        }
    }
}
