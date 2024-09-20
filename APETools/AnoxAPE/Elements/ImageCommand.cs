// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
using System.IO;
using System;

namespace AnoxAPE.Elements
{
    public class ImageCommand : IWindowCommand
    {
        public enum EImageFlagBit
        {
            Stretch = 0,
            Tile = 1,
            Solid = 2,
        }

        public OptionalExpression Condition { get; private set; }
        public ByteString FileName { get; private set; }
        public OptionalExpression XPos { get; private set; }
        public OptionalExpression YPos { get; private set; }
        public OptionalExpression Width { get; private set; }
        public OptionalExpression Height { get; private set; }
        public uint Flags { get; private set; }

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Image; } }

        public bool HasFlag(EImageFlagBit flagBit)
        {
            uint mask = (uint)1 << (int)flagBit;
            return (Flags & mask) != 0;
        }

        public ImageCommand()
        {
            Condition = new OptionalExpression();
            FileName = new ByteString();
            XPos = new OptionalExpression();
            YPos = new OptionalExpression();
            Width = new OptionalExpression();
            Height = new OptionalExpression();
            Flags = 0;
        }

        public ImageCommand(OptionalExpression condition, ByteString fileName, OptionalExpression xpos, OptionalExpression ypos, OptionalExpression width, OptionalExpression height, IEnumerable<EImageFlagBit> flags)
        {
            Condition = condition;
            FileName = fileName;
            XPos = xpos;
            YPos = ypos;
            Width = width;
            Height = height;

            Flags = 0;

            foreach (EImageFlagBit flagBit in flags)
            {
                uint mask = (uint)1 << (int)flagBit;
                Flags |= mask;
            }
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "ImageCommand");

            Condition.Load(inStream, indent + 1, disasmStream);
            FileName.Load(inStream, indent + 1, disasmStream);
            XPos.Load(inStream, indent + 1, disasmStream);
            YPos.Load(inStream, indent + 1, disasmStream);
            Width.Load(inStream, indent + 1, disasmStream);
            Height.Load(inStream, indent + 1, disasmStream);

            Flags = inStream.ReadUInt32();

            if (disasmStream != null)
            {
                string flagsDesc = "";
                if (HasFlag(EImageFlagBit.Stretch))
                    flagsDesc += ",stretch";
                if (HasFlag(EImageFlagBit.Tile))
                    flagsDesc += ",tile";
                if (HasFlag(EImageFlagBit.Solid))
                    flagsDesc += ",solid";

                uint moreFlags = (Flags & 0xFFFFFFF8u);
                if (moreFlags != 0)
                    flagsDesc += $",Unknown({moreFlags})";

                if (flagsDesc.Length > 0)
                    flagsDesc = flagsDesc.Substring(1);

                disasmStream.WriteIndent(indent + 1);
                disasmStream.WriteString($"ImageFlags({flagsDesc})\n");
            }
        }

        public bool CanEmitAsBackground()
        {
            float f = 0;

            if (XPos.Expression == null || YPos.Expression == null)
                return false;

            if (!XPos.Expression.TryResolveFloatConstant(out f) || f != 0.0f)
                return false;

            if (!YPos.Expression.TryResolveFloatConstant(out f) || f != 0.0f)
                return false;

            if (Width.Expression != null)
                return false;

            if (Height.Expression != null)
                return false;

            if (HasFlag(EImageFlagBit.Solid))
                return false;

            return true;
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte(73);

            Condition.Write(outStream);
            FileName.Write(outStream);
            XPos.Write(outStream);
            YPos.Write(outStream);
            Width.Write(outStream);
            Height.Write(outStream);

            outStream.WriteUInt32(Flags);
        }
    }
}
