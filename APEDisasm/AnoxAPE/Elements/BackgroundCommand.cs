using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnoxAPE.Elements
{
    public class BackgroundCommand : IWindowCommand
    {
        public uint Color1 { get; private set; }
        public uint Color2 { get; private set; }
        public uint Color3 { get; private set; }
        public uint Color4 { get; private set; }

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Background; } }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "BackgroundCommand");

            Color1 = inStream.ReadUInt32();
            Color2 = inStream.ReadUInt32();
            Color3 = inStream.ReadUInt32();
            Color4 = inStream.ReadUInt32();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent + 1, $"Color1({ColorToHex(Color1)}) Color2({ColorToHex(Color2)}) Color3({ColorToHex(Color3)})  Color4({ColorToHex(Color4)})");
        }

        public BackgroundCommand()
        {
        }

        public BackgroundCommand(uint color1, uint color2, uint color3, uint color4)
        {
            Color1 = color1;
            Color2 = color2;
            Color3 = color3;
            Color4 = color4;
        }

        public static string ColorToHex(uint colorValue)
        {
            string nibbles = "0123456789abcdef";
            string result = "";

            for (int i = 0; i < 4; i++)
            {
                int b = (int)(colorValue & 0xff);
                colorValue >>= 8;

                string byteStr = "";
                for (int j = 0; j < 2; j++)
                {
                    byteStr = nibbles[(int)(b & 0xfu)] + byteStr;
                    b >>= 4;
                }

                result = result + byteStr;
            }

            return result;
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte(68);
            outStream.WriteUInt32(Color1);
            outStream.WriteUInt32(Color2);
            outStream.WriteUInt32(Color3);
            outStream.WriteUInt32(Color4);
        }
    }
}
