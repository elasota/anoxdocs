// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public class OptionalString
    {
        public ByteString? Value { get; private set; }

        public OptionalString()
        {
        }

        public OptionalString(ByteString? value)
        {
            Value = value;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            uint length = inStream.ReadUInt32();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "OptionalString");

            if (length > 0)
            {
                Value = new ByteString();
                Value.LoadWithLength(length, inStream, indent + 1, disasmStream);
            }
        }

        public void Write(OutputStream outStream)
        {
            if (Value == null)
                outStream.WriteUInt32(0);
            else
                Value.Write(outStream);
        }
    }
}
