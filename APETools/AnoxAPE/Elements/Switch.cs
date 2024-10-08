﻿// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public class Switch
    {
        public uint Label { get; private set; }
        public SwitchCommandList CommandList { get; private set; }

        public Switch(uint label)
        {
            Label = label;
            CommandList = new SwitchCommandList();
        }

        public Switch(uint label, SwitchCommandList commandList)
        {
            Label = label;
            CommandList = commandList;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
            {
                disasmStream.WriteLine($"FilePosition({inStream.Position})");
                disasmStream.WriteLineIndented(indent, $"Switch({Label})");
            }

            CommandList.Load(inStream, indent + 1, disasmStream);
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteUInt32(Label);
            CommandList.Write(outStream);
        }
    }
}
