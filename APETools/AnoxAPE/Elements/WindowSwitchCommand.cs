// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public class WindowSwitchCommand : IWindowCommand
    {
        public enum ECommandType
        {
            StartSwitchCommand = 49,
            ThinkSwitchCommand = 50,
            FinishSwitchCommand = 51,
        }

        public ECommandType CommandType { get; private set; }
        public uint Label { get; private set; }


        public WindowCommandType WindowCommandType { get { return WindowCommandType.Switch; } }

        public WindowSwitchCommand(byte commandByte)
        {
            CommandType = (ECommandType)commandByte;
        }

        public WindowSwitchCommand(ECommandType commandType, uint label)
        {
            CommandType = commandType;
            Label = label;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            inStream.MarkSwitchLabel();

            Label = inStream.ReadUInt32();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, $"{CommandType.ToString()}({Label})");
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte((byte)CommandType);
            outStream.WriteUInt32(Label);
        }
    }
}
