// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public enum WindowCommandType
    {
        ConditionalFormattedStringCommand,
        Talk,
        Dimensions,
        Image,
        Flags,
        SubWindow,
        Choice,
        SimpleStringCommand,
        XYPrintFX,
        Switch,
        Background,
        Cam,
    }

    public interface IWindowCommand
    {
        public void Load(InputStream inStream, int indent, OutputStream? disasmStream);
        public void WriteWithID(OutputStream outStream);
        public WindowCommandType WindowCommandType { get; }
    }
}
