﻿// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public class QuotedStringOperand : IExpressionOperand
    {
        public ByteString Value { get; private set; }

        public QuotedStringOperand()
        {
            Value = new ByteString();
        }

        public QuotedStringOperand(ByteString value)
        {
            Value = value;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "QuotedStringOperand");

            Value.Load(inStream, indent + 1, disasmStream);
        }

        public void Write(OutputStream outStream)
        {
            Value.Write(outStream);
        }
    }
}
