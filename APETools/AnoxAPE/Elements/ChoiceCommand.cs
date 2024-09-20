// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public class ChoiceCommand : IWindowCommand
    {
        public OptionalExpression Condition { get; private set; }
        public ByteString Str { get; private set; }
        public FormattingValue FormattingValue { get; private set; }
        public uint Label { get; private set; }


        public WindowCommandType WindowCommandType { get { return WindowCommandType.Choice; } }

        public ChoiceCommand()
        {
            Condition = new OptionalExpression();
            Str = new ByteString();
            FormattingValue = new FormattingValue();
        }

        public ChoiceCommand(OptionalExpression condition, ByteString str, FormattingValue formattingValue, uint label)
        {
            Condition = condition;
            Str = str;
            FormattingValue = formattingValue;
            Label = label;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "ChoiceCommand");

            Condition.Load(inStream, indent + 1, disasmStream);
            Str.Load(inStream, indent + 1, disasmStream);
            FormattingValue.Load(inStream, indent + 1, disasmStream);

            Label = inStream.ReadUInt32();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent + 1, $"Label({Label})");
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte(67);

            Condition.Write(outStream);
            Str.Write(outStream);
            FormattingValue.Write(outStream);

            outStream.WriteUInt32(Label);
        }
    }
}
