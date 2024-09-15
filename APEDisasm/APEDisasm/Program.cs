using APEDisasm;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Xml.Linq;

namespace APEDisasm
{
    internal class FlagUtil
    {
        public static void DetectFlag(ref uint flags, ref string flagsList, int flagPos, string flagName)
        {
            uint mask = ((uint)1) << flagPos;
            uint maskedFlag = flags & mask;
            if (maskedFlag != 0)
            {
                flags -= maskedFlag;
                if (flagsList.Length > 0)
                    flagsList += ",";
                flagsList += flagName;
            }
        }

        public static void AddUnknownFlags(uint flags, ref string flagsList)
        {
            int flagPos = 0;
            while (flags != 0)
            {
                if ((flags & 1) != 0)
                {
                    if (flagsList.Length > 0)
                        flagsList += ",";
                    flagsList += $"UnknownFlag{flagPos}";
                }

                flags = flags >> 1;
                flagPos++;
            }
        }
    }

    internal class ByteString
    {
        public byte[] Bytes { get; private set; }

        public ByteString()
        {
            Bytes = new byte[0];
        }

        public void LoadWithLength(uint length, InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (length == 0 || length > 32768)
                inStream.ReportError("Invalid length of string");

            Bytes = inStream.ReadBytes(length - 1);
            if (inStream.ReadByte() != 0)
                inStream.ReportError("String was not null-terminated");

            if (disasmStream != null)
            {
                disasmStream.WriteIndent(indent);
                disasmStream.WriteString("String '");
                disasmStream.WriteBytes(Bytes);
                disasmStream.WriteString("'\n");
            }
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            LoadWithLength(inStream.ReadUInt32(), inStream, indent, disasmStream);
        }
    }

    internal class OptionalString
    {
        public ByteString? Value { get; private set; }

        public OptionalString()
        {
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
    }

    internal class InputStream
    {
        private Stream _stream;

        public InputStream(Stream stream)
        {
            _stream = stream;
        }

        public byte[] ReadBytes(uint count)
        {
            long readStart = _stream.Position;

            byte[] bytes = new byte[count];
            if (_stream.Read(bytes, 0, (int)count) != count)
                throw new Exception($"Failed to read {count} bytes at file position {readStart}");

            return bytes;
        }

        public uint ReadUInt32()
        {
            byte[] bytes = ReadBytes(4);

            uint result = bytes[0];
            result |= ((uint)bytes[1] << 8);
            result |= ((uint)bytes[2] << 16);
            result |= ((uint)bytes[3] << 24);

            return result;
        }

        public ushort ReadUInt16()
        {
            byte[] bytes = ReadBytes(2);
            return (ushort)(bytes[0] | (bytes[1] << 8));
        }

        public void ExpectUInt32(uint expectedValue)
        {
            long readStart = _stream.Position;
            uint u32Value = ReadUInt32();

            if (u32Value != expectedValue)
                throw new Exception($"Expected U32 value {expectedValue} at position {readStart} but value was {u32Value}");
        }

        public byte ReadByte()
        {
            long readStart = _stream.Position;

            int b = _stream.ReadByte();
            if (b == -1)
                throw new IOException($"Failed to read 4 bytes at file position {readStart}");

            return (byte)b;
        }

        public ulong ReadUInt64()
        {
            byte[] bytes = ReadBytes(8);

            ulong result = bytes[0];
            result |= ((ulong)bytes[1] << 8);
            result |= ((ulong)bytes[2] << 16);
            result |= ((ulong)bytes[3] << 24);
            result |= ((ulong)bytes[4] << 32);
            result |= ((ulong)bytes[5] << 40);
            result |= ((ulong)bytes[6] << 48);
            result |= ((ulong)bytes[7] << 56);

            return result;
        }

        public float ReadFloat()
        {
            uint u32 = ReadUInt32();

            byte[] u32Bytes = BitConverter.GetBytes(u32);

            return BitConverter.ToSingle(u32Bytes);
        }

        public bool IsAtEOF()
        {
            return _stream.Position == _stream.Length;
        }

        public void ReportError(string err)
        {
            throw new Exception($"{err}, last read ended at {_stream.Position}");
        }
    }

    internal class OutputStream
    {
        private Stream _stream;

        public OutputStream(Stream stream)
        {
            _stream = stream;
        }

        public void WriteBytes(byte[] bytes)
        {
            _stream.Write(bytes, 0, bytes.Length);
        }

        public void WriteIndent(int indentLevel)
        {
            if (indentLevel > 0)
            {
                byte[] bytes = new byte[4];

                bytes[0] = 32;
                bytes[1] = 32;
                bytes[2] = 32;
                bytes[3] = 32;

                for (int i = 0; i < indentLevel; i++)
                    _stream.Write(bytes, 0, 4);
            }
        }

        public void WriteLineIndented(int indentLevel, string value)
        {
            WriteIndent(indentLevel);

            byte[] encoded = System.Text.Encoding.UTF8.GetBytes(value);
            _stream.Write(encoded, 0, encoded.Length);
            _stream.WriteByte(10);
        }

        public void WriteString(string value)
        {
            byte[] encoded = System.Text.Encoding.UTF8.GetBytes(value);
            _stream.Write(encoded, 0, encoded.Length);
        }
    }

    internal class TypedFormattingValue
    {
        public enum EFormattingValueType
        {
            Float = 4,
            VariableName = 5,
            String = 16,
            StringVariableName = 17,
        }

        public EFormattingValueType Type { get; private set; }
        public IExpressionOperand Value { get; private set; }

        public TypedFormattingValue()
        {
            Value = new InvalidOperand();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            byte typeByte = inStream.ReadByte();

            if (typeByte == 4 || typeByte == 5 || typeByte == 16 || typeByte == 17)
            {
                Type = (EFormattingValueType)typeByte;

                if (typeByte == 4)
                    Value = new FloatOperand();
                else if (typeByte == 5 || typeByte == 17)
                    Value = new StringOperand();
                else if (typeByte == 16)
                    Value = new QuotedStringOperand();
                else
                    throw new Exception("Internal error");

                if (disasmStream != null)
                    disasmStream.WriteLineIndented(indent, $"TypedFormattingValue({Type.ToString()})");
            }
            else
                inStream.ReportError("Invalid type byte for TypedFormattingValue");

            Value.Load(inStream, indent + 1, disasmStream);
        }
    }

    internal class FormattingValue
    {
        public IReadOnlyList<TypedFormattingValue> Values { get; private set; }

        private List<TypedFormattingValue> _values;

        public FormattingValue()
        {
            _values = new List<TypedFormattingValue>();
            Values = _values;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            byte doneByte = inStream.ReadByte();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "FormattingValue");

            while (doneByte == 0)
            {
                TypedFormattingValue value = new TypedFormattingValue();
                value.Load(inStream, indent + 1, disasmStream);
                _values.Add(value);

                doneByte = inStream.ReadByte();
            }

            if (doneByte != 255)
                inStream.ReportError("Invalid FormattingValue done sequence");

            byte afterDoneByte = inStream.ReadByte();

            if (afterDoneByte != 255)
                inStream.ReportError("Invalid FormattingValue done sequence");
        }
    }

    internal class ExpressionSegment
    {
        public ExpressionValue? Expression { get; private set; }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "ExpressionSegment");

            ulong existsFlag = inStream.ReadUInt64();

            if (existsFlag == 1)
            {
                Expression = new ExpressionValue();
                Expression.Load(inStream, indent + 1, disasmStream);

                inStream.ExpectUInt32(0);
                inStream.ExpectUInt32(0);
            }
            else if (existsFlag != 0)
                inStream.ReportError("Unexpected flag in expression segment");
        }
    }

    internal interface IWindowCommand
    {
        public void Load(InputStream inStream, int indent, OutputStream? disasmStream);
    }

    internal class SwitchCommand
    {
        public enum ECommandType
        {
            SetFocusCommand = 0,
            IfCommand = 1,
            SetUnsetCommand = 2,
            VariableCommand = 3,
            GotoCommand = 4,
            GoSubCommand = 5,
            ConsoleCommand = 6,
            EchoCommand = 7,
            TargetCommand = 8,
            PathTargetCommand = 9,
            ExternCommand = 10,
            WhileCommand = 11,
            PlayAmbientCommand = 12,
            LoopAmbientCommand = 13,
            StopAmbientCommand = 14,
            PlaySceneCommand = 15,
            LoopSceneCommand = 16,
            StopSceneCommand = 17,
            ChainScriptsCommand = 18,
            CloseWindowCommand = 19,
            LoadAPECommand = 20,

        }

        public OptionalString Str { get; private set; }
        public ECommandType CommandType { get; private set; }
        public FormattingValue FormattingValue { get; private set; }
        public OptionalExpression Condition { get; private set; }

        public SwitchCommand(byte commandByte)
        {
            CommandType = (ECommandType)commandByte;
            Str = new OptionalString();
            FormattingValue = new FormattingValue();
            Condition = new OptionalExpression();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, CommandType.ToString());

            Str.Load(inStream, indent + 1, disasmStream);
            FormattingValue.Load(inStream, indent + 1, disasmStream);
            Condition.Load(inStream, indent + 1, disasmStream);
        }
    }

    internal class WindowSwitchCommand : IWindowCommand
    {
        public enum ECommandType
        {
            StartSwitchCommand = 49,
            ThinkSwitchCommand = 50,
            FinishSwitchCommand = 51,
        }

        public ECommandType CommandType { get; private set; }
        public uint Label { get; private set; }

        public WindowSwitchCommand(byte commandByte)
        {
            CommandType = (ECommandType)commandByte;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            Label = inStream.ReadUInt32();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, $"{CommandType.ToString()}({Label})");
        }
    }

    internal class SimpleStringCommand : IWindowCommand
    {
        public enum ECommandType
        {
            StartConsoleCommand = 65,
            FontCommand = 70,
            FinishConsoleCommand = 78,
            NextWindowCommand = 79,
            StyleCommand = 87,
        }

        public ECommandType CommandType { get; private set; }
        public ByteString CommandStr { get; private set; }

        public SimpleStringCommand(byte commandByte)
        {
            CommandType = (ECommandType)commandByte;
            CommandStr = new ByteString();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, CommandType.ToString());

            CommandStr.Load(inStream, indent + 1, disasmStream);
        }
    }

    internal class BodyCommand : IWindowCommand
    {
        public OptionalExpression Condition { get; private set; }
        public ByteString BodyStr { get; private set; }
        public FormattingValue FormattingValue { get; private set; }

        public BodyCommand()
        {
            Condition = new OptionalExpression();
            BodyStr = new ByteString();
            FormattingValue = new FormattingValue();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "BodyCommand");

            Condition.Load(inStream, indent + 1, disasmStream);
            BodyStr.Load(inStream, indent + 1, disasmStream);
            FormattingValue.Load(inStream, indent + 1, disasmStream);
        }
    }

    internal class BackgroundCommand : IWindowCommand
    {
        public uint Color1 { get; private set; }
        public uint Color2 { get; private set; }
        public uint Color3 { get; private set; }
        public uint Color4 { get; private set; }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "BackgroundCommand");

            Color1 = inStream.ReadUInt32();
            Color2 = inStream.ReadUInt32();
            Color3 = inStream.ReadUInt32();
            Color4 = inStream.ReadUInt32();

            disasmStream.WriteLineIndented(indent + 1, $"Color1({ColorToHex(Color1)}) Color2({ColorToHex(Color2)}) Color3({ColorToHex(Color1)})  Color4({ColorToHex(Color4)})");
        }

        private static string ColorToHex(uint colorValue)
        {
            string nibbles = "0123456789abcdef";
            string result = "";

            for (int i = 0; i < 8; i++)
            {
                result = nibbles[(int)(colorValue & 0xfu)] + result;
                colorValue >>= 4;
            }

            return result;
        }
    }

    internal class ChoiceCommand : IWindowCommand
    {
        public OptionalExpression Condition { get; private set; }
        public ByteString Str { get; private set; }
        public FormattingValue FormattingValue { get; private set; }
        public uint Label { get; private set; }

        public ChoiceCommand()
        {
            Condition = new OptionalExpression();
            Str = new ByteString();
            FormattingValue = new FormattingValue();
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
    }

    internal interface IExpressionOperand
    {
        public void Load(InputStream inStream, int indent, OutputStream? disasmStream);
    }

    internal class ExpressionValueOperand : IExpressionOperand
    {
        public ExpressionValue Expression { get; private set; }

        public ExpressionValueOperand()
        {
            Expression = new ExpressionValue();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            Expression.Load(inStream, indent, disasmStream);
        }
    }

    internal class FloatOperand : IExpressionOperand
    {
        public float Value { get; private set; }

        public FloatOperand()
        {
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            Value = inStream.ReadFloat();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, $"FloatOperand({Value})");
        }
    }

    internal class StringOperand : IExpressionOperand
    {
        public ByteString Value { get; private set; }

        public StringOperand()
        {
            Value = new ByteString();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "StringOperand");

            Value.Load(inStream, indent + 1, disasmStream);
        }
    }

    internal class QuotedStringOperand : IExpressionOperand
    {
        public ByteString Value { get; private set; }

        public QuotedStringOperand()
        {
            Value = new ByteString();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "QuotedStringOperand");

            Value.Load(inStream, indent + 1, disasmStream);
        }
    }

    internal class InvalidOperand : IExpressionOperand
    {
        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            throw new Exception("Operand was invalid");
        }
    }

    internal class ExpressionValue
    {
        public enum EOperator
        {
            Or = 1,
            And = 2,
            Xor = 3,
            Gt = 4,
            Lt = 5,
            Ge = 6,
            Le = 7,
            Eq = 8,
            Add = 9,
            Sub = 10,
            Mul = 11,
            Div = 12,
            Neq = 13,
        }

        public EOperator Operator { get; private set; }
        public ulong LeftPrefix { get; private set; }
        public IExpressionOperand Left { get; private set; }
        public ulong RightPrefix { get; private set; }
        public IExpressionOperand Right { get; private set; }

        public ExpressionValue()
        {
            Left = new InvalidOperand();
            Right = Left;
        }

        private static void ValidateFlags(InputStream inStream, uint exprFlags)
        {
            if ((exprFlags & 0x15) == 0x01)
                inStream.ReportError("Expression element was an untyped variable");
            if ((exprFlags & 0x15) == 0x14)
                inStream.ReportError("Expression element was an a float and string");
        }

        private static IExpressionOperand OperandFromFlags(uint flags)
        {
            if (flags == 0)
                return new ExpressionValueOperand();
            if (flags == 4)
                return new FloatOperand();
            if (flags == 5 || flags == 17)
                return new StringOperand();
            if (flags == 16)
                return new QuotedStringOperand();

            throw new Exception("Internal error: Unhandled expr flags");
        }

        private static string OperandTypeNameFromFlags(uint flags)
        {
            if (flags == 0)
                return "Expression";
            if (flags == 4)
                return "Float";
            if (flags == 5)
                return "FloatVar";
            if (flags == 16)
                return "StringLit";
            if (flags == 17)
                return "StringVar";

            throw new Exception("Internal error: Unhandled expr flags");
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            byte exprOperator = inStream.ReadByte();
            byte exprFlags = inStream.ReadByte();

            if (exprOperator == 0 || exprOperator > 13)
                inStream.ReportError($"Unknown expr operator {exprOperator}");

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, $"ExpressionValue({Operator.ToString()})");

            uint leftSideFlags = (exprFlags & 0x15u);
            uint rightSideFlags = (uint)((exprFlags >> 1) & 0x15u);

            if ((exprFlags & 0xc0) != 0)
                inStream.ReportError("Expression element had unknown flags");

            ValidateFlags(inStream, leftSideFlags);
            ValidateFlags(inStream, rightSideFlags);

            if ((leftSideFlags & 0x15) == 1)
                inStream.ReportError("Left side flags were invalid");

            Left = OperandFromFlags(leftSideFlags);
            Right = OperandFromFlags(rightSideFlags);

            LeftPrefix = inStream.ReadUInt64();

            if (disasmStream != null)
            {
                string leftType = OperandTypeNameFromFlags(leftSideFlags);
                string rightType = OperandTypeNameFromFlags(rightSideFlags);

                disasmStream.WriteLineIndented(indent + 1, $"ExpressionType({leftType}, {rightType})");
                disasmStream.WriteLineIndented(indent + 1, $"LeftTreePos({LeftPrefix})");
            }

            Left.Load(inStream, indent + 1, disasmStream);

            RightPrefix = inStream.ReadUInt64();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent + 1, $"LeftTreePos({RightPrefix})");

            Right.Load(inStream, indent + 1, disasmStream);
        }
    }

    internal class OptionalExpression
    {
        public ExpressionValue? Expression { get; private set; }

        public OptionalExpression()
        {
            Expression = null;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "OptionalExpression");

            ulong exprFlag = inStream.ReadUInt64();
            if (exprFlag == 0)
                return;
            else if (exprFlag == 1)
            {
                Expression = new ExpressionValue();

                Expression.Load(inStream, indent + 1, disasmStream);

                inStream.ExpectUInt32(0);
                inStream.ExpectUInt32(0);
            }
            else
                inStream.ReportError($"Unexpected flag {exprFlag} in OptionalExpression");
        }
    }

    internal class TitleCommand : IWindowCommand
    {
        public OptionalExpression Condition { get; private set; }
        public ByteString Text { get; private set; }
        public FormattingValue FormattingValue { get; private set; }

        public TitleCommand()
        {
            Condition = new OptionalExpression();
            Text = new ByteString();
            FormattingValue = new FormattingValue();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "TitleCommand");

            Condition.Load(inStream, indent + 1, disasmStream);
            Text.Load(inStream, indent + 1, disasmStream);
            FormattingValue.Load(inStream, indent + 1, disasmStream);
        }
    }

    internal class XYPrintFXCommand : IWindowCommand
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
    }

    internal class CamCommand : IWindowCommand
    {
        public ByteString Name { get; private set; }
        public OptionalString From { get; private set; }
        public OptionalString To { get; private set; }
        public OptionalString Owner { get; private set; }
        public ushort Yaw { get; private set; }
        public ushort Pitch { get; private set; }
        public ushort Fov { get; private set; }
        public ushort Far { get; private set; }
        public ushort Near { get; private set; }
        public ushort Fwd { get; private set; }
        public ushort Speed { get; private set; }
        public ushort Lift { get; private set; }
        public ushort Lag { get; private set; }
        public ushort Occlude { get; private set; }
        public ushort Restore { get; private set; }
        public ushort Zip { get; private set; }


        public CamCommand()
        {
            Name = new ByteString();
            From = new OptionalString();
            To = new OptionalString();
            Owner = new OptionalString();

        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "CamCommand");

            Name.Load(inStream, indent + 1, disasmStream);
            From.Load(inStream, indent + 1, disasmStream);
            To.Load(inStream, indent + 1, disasmStream);
            Owner.Load(inStream, indent + 1, disasmStream);

            Yaw = inStream.ReadUInt16();
            Pitch = inStream.ReadUInt16();
            Fov = inStream.ReadUInt16();
            Far = inStream.ReadUInt16();
            Near = inStream.ReadUInt16();
            Fwd = inStream.ReadUInt16();
            Speed = inStream.ReadUInt16();
            Lift = inStream.ReadUInt16();
            Lag = inStream.ReadUInt16();
            Occlude = inStream.ReadUInt16();
            Restore = inStream.ReadUInt16();
            Zip = inStream.ReadUInt16();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent + 1, $"CamParams(Yaw={Yaw},Pitch={Pitch},Fov={Fov},Far={Far},Near={Near},Fwd={Fwd},Speed={Speed},Lift={Lift},Lag={Lag},Occlude={Occlude},Restore={Restore},Zip={Zip})");
        }
    }

    internal class TalkCommand : IWindowCommand
    {
        public ByteString Animation1 { get; private set; }
        public OptionalString Animation2 { get; private set; }
        public ByteString Name1 { get; private set; }
        public ByteString Name2 { get; private set; }
        public uint Stay1Flag { get; private set; }
        public uint Stay2Flag { get; private set; }

        public TalkCommand()
        {
            Animation1 = new ByteString();
            Animation2 = new OptionalString();
            Name1 = new ByteString();
            Name2 = new ByteString();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "TalkCommand");

            Animation1.Load(inStream, indent + 1, disasmStream);
            Animation2.Load(inStream, indent + 1, disasmStream);
            Name1.Load(inStream, indent + 1, disasmStream);
            Name2.Load(inStream, indent + 1, disasmStream);
            Stay1Flag = inStream.ReadUInt32();
            Stay2Flag = inStream.ReadUInt32();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent + 1, $"TalkParams(Stay1Flag={Stay1Flag},Stay2Flag={Stay2Flag})");
        }
    }

    internal class DimensionsCommand : IWindowCommand
    {
        public OptionalExpression XPos { get; private set; }
        public OptionalExpression YPos { get; private set; }
        public OptionalExpression Width { get; private set; }
        public OptionalExpression Height { get; private set; }

        public DimensionsCommand()
        {
            XPos = new OptionalExpression();
            YPos = new OptionalExpression();
            Width = new OptionalExpression();
            Height = new OptionalExpression();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "DimensionsCommand");

            XPos.Load(inStream, indent + 1, disasmStream);
            YPos.Load(inStream, indent + 1, disasmStream);
            Width.Load(inStream, indent + 1, disasmStream);
            Height.Load(inStream, indent + 1, disasmStream);
        }
    }


    internal class ImageCommand : IWindowCommand
    {
        public OptionalExpression Condition { get; private set; }
        public ByteString FileName { get; private set; }
        public OptionalExpression XPos { get; private set; }
        public OptionalExpression YPos { get; private set; }
        public OptionalExpression Width { get; private set; }
        public OptionalExpression Height { get; private set; }
        public uint Flags { get; private set; }

        public ImageCommand()
        {
            Condition = new OptionalExpression();
            FileName = new ByteString();
            XPos = new OptionalExpression();
            YPos = new OptionalExpression();
            Width = new OptionalExpression();
            Height = new OptionalExpression();
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
                if ((Flags & (1 << 0)) != 0)
                    flagsDesc += ",stretch";
                if ((Flags & (1 << 1)) != 0)
                    flagsDesc += ",tile";
                if ((Flags & (1 << 2)) != 0)
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
    }

    internal class FlagsCommand : IWindowCommand
    {
        public uint Flags { get; private set; }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            uint Flags = inStream.ReadUInt32();

            if (disasmStream != null)
            {
                disasmStream.WriteLineIndented(indent, "FlagsCommand");

                uint scanFlags = Flags;
                string flagsDesc = "";

                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, 0, "persist");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, 2, "noscroll");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, 3, "nograb");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, 4, "norelease");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, 5, "subtitle");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, 29, "passive2d");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, 30, "passive");
                FlagUtil.AddUnknownFlags(scanFlags, ref flagsDesc);

                disasmStream.WriteIndent(indent + 1);
                disasmStream.WriteString($"Flags({flagsDesc})\n");
            }
        }
    }

    internal class SubWindowCommand : IWindowCommand
    {
        public uint Label { get; private set; }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            inStream.ExpectUInt32(0);
            inStream.ExpectUInt32(0);
            Label = inStream.ReadUInt32();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, $"SubWindowCommand({Label})");
        }
    }

    internal class WindowCommandList
    {
        public IReadOnlyList<IWindowCommand> Commands { get; private set; }
        private List<IWindowCommand> _commandsList;

        public WindowCommandList()
        {
            _commandsList = new List<IWindowCommand>();
            Commands = _commandsList;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            while (true)
            {
                byte commandByte = inStream.ReadByte();
                IWindowCommand? cmd = null;

                switch (commandByte)
                {
                    case 49:
                    case 50:
                    case 51:
                        cmd = new WindowSwitchCommand(commandByte);
                        break;
                    case 65:
                    case 70:
                    case 78:
                    case 79:
                    case 87:
                        cmd = new SimpleStringCommand(commandByte);
                        break;
                    case 66:
                        cmd = new BodyCommand();
                        break;
                    case 67:
                        cmd = new ChoiceCommand();
                        break;
                    case 68:
                        cmd = new BackgroundCommand();
                        break;
                    case 69:
                        if (disasmStream != null)
                            disasmStream.WriteLineIndented(indent, "EndCommand");
                        return;
                    case 71:
                        cmd = new DimensionsCommand();
                        break;
                    case 72:
                        cmd = new SubWindowCommand();
                        break;
                    case 73:
                        cmd = new ImageCommand();
                        break;
                    case 76:
                        cmd = new FlagsCommand();
                        break;
                    case 77:
                        cmd = new CamCommand();
                        break;
                    case 80:
                        cmd = new XYPrintFXCommand();
                        break;
                    case 84:
                        cmd = new TitleCommand();
                        break;
                    case 89:
                        cmd = new TalkCommand();
                        break;
                    default:
                        inStream.ReportError($"Unknown command code {commandByte}");
                        break;
                }

                if (cmd == null)
                    throw new Exception("Internal error: Didn't create a command");

                cmd.Load(inStream, indent, disasmStream);
                _commandsList.Add(cmd);
            }
        }
    }

    internal struct CCPrefixedCommand
    {
        public ulong ConditionControl { get; private set; }
        public SwitchCommand Command { get; private set; }

        public CCPrefixedCommand(ulong cc, SwitchCommand command)
        {
            ConditionControl = cc;
            Command = command;
        }
    }

    internal class SwitchCommandList
    {
        public IReadOnlyList<CCPrefixedCommand> Commands { get; private set; }
        private List<CCPrefixedCommand> _commandsList;

        public SwitchCommandList()
        {
            _commandsList = new List<CCPrefixedCommand>();
            Commands = _commandsList;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            while (true)
            {
                ulong cc = inStream.ReadUInt64();
                byte commandByte = inStream.ReadByte();

                if (disasmStream != null)
                {
                    string ccBin = "";
                    ulong ccBits = cc;

                    while (ccBits != 0)
                    {
                        ccBin = $"{(ccBits & 1)}" + ccBin;
                        ccBits >>= 1;
                    }

                    if (ccBin.Length == 0)
                        ccBin = "0";

                    disasmStream.WriteLineIndented(indent, $"CCLabel({ccBin})");
                }

                if (commandByte > 20)
                {
                    if (commandByte == 69)
                    {
                        if (cc != 0)
                            inStream.ReportError("Invalid cc code for end command");

                        if (disasmStream != null)
                            disasmStream.WriteLineIndented(indent, "EndCommand");
                        return;
                    }

                    inStream.ReportError("Invalid switch command code {commandByte}");
                }

                SwitchCommand cmd = new SwitchCommand(commandByte);

                cmd.Load(inStream, indent, disasmStream);

                CCPrefixedCommand prefixedCmd = new CCPrefixedCommand(cc, cmd);

                _commandsList.Add(prefixedCmd);
            }
        }
    }

    internal class Window
    {
        public uint WindowId { get; private set; }
        public WindowCommandList CommandList { get; private set; }

        public Window(uint windowID)
        {
            WindowId = windowID;
            CommandList = new WindowCommandList();
        }

        public void Load(InputStream inStream, OutputStream? disasmStream)
        {
            this.CommandList.Load(inStream, 1, disasmStream);
        }
    }

    internal class Switch
    {
        public uint Label { get; private set; }
        public SwitchCommandList CommandList { get; private set; }

        public Switch(uint label)
        {
            Label = label;
            CommandList = new SwitchCommandList();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, $"Switch({Label})");

            CommandList.Load(inStream, indent + 1, disasmStream);
        }
    }

    internal class Switches
    {
        public IReadOnlyList<Switch> SwitchList { get; private set; }
        private List<Switch> _switchList;

        public Switches()
        {
            _switchList = new List<Switch>();
            SwitchList = _switchList;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "Switches");

            uint label = inStream.ReadUInt32();
            while (label != 0)
            {
                Switch sw = new Switch(label);
                sw.Load(inStream, indent, disasmStream);

                _switchList.Add(sw);

                label = inStream.ReadUInt32();
            }
        }
    }

    internal class RootElementList
    {
        public IReadOnlyList<Window> Windows { get; private set; }
        public Switches Switches { get; private set; }

        private List<Window> _windows;

        public RootElementList()
        {
            _windows = new List<Window>();
            Windows = _windows;
            Switches = new Switches();
        }

        public void Load(InputStream inStream, OutputStream? disasmStream)
        {
            while (true)
            {
                uint windowID = inStream.ReadUInt32();
                if (windowID == 0)
                {
                    uint switchesTagID = inStream.ReadUInt32();

                    if (switchesTagID != 0xfffffffeu)
                        inStream.ReportError("Unexpected switch tag ID");

                    Switches.Load(inStream, 0, disasmStream);

                    if (!inStream.IsAtEOF())
                        inStream.ReportError("Unexpected trailing data");
                    break;
                }
                else
                {
                    Window window = new Window(windowID);

                    if (disasmStream != null)
                    {
                        disasmStream.WriteString($"Window {windowID}\n");
                    }

                    window.Load(inStream, disasmStream);

                    _windows.Add(window);
                }
            }
        }
    }

    internal class APEFile
    {
        public RootElementList RootElementList { get; private set; }

        public APEFile()
        {
            RootElementList = new RootElementList();
        }

        public void Load(InputStream inStream, OutputStream? disasmStream)
        {
            uint header1 = inStream.ReadUInt32();
            uint header2 = inStream.ReadUInt32();

            if (header1 != 317 && header2 != 0xffffffffu)
                inStream.ReportError("Header is invalid");

            RootElementList.Load(inStream, disasmStream);
        }
    }

    internal class Program
    {

        static void PrintUsage()
        {
            Console.Error.WriteLine("Syntax: APEDisasm [-src] <input> <output>");
            Environment.ExitCode = -1;
        }

        static APEFile Disassemble(Stream inStream, Stream outStream, bool sourceMode)
        {
            OutputStream? disasmStream = null;

            if (!sourceMode)
                disasmStream = new OutputStream(outStream);

            APEFile apeFile = new APEFile();

            apeFile.Load(new InputStream(inStream), disasmStream);

            return apeFile;
        }

        static void DisassembleSingleFile(string inputPath, string outputPath, bool sourceMode)
        {
            using (FileStream inFile = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
            {
                using (FileStream outFile = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    try
                    {
                        Disassemble(inFile, outFile, sourceMode);
                    }
                    catch (Exception)
                    {
                        outFile.Flush();
                        throw;
                    }
                }
            }
        }

        static void DisassembleDirectory(string inputPath, string outputPath, bool sourceMode)
        {
            string[] inputPathFiles = Directory.GetFiles(inputPath);

            foreach (string fullPathStr in inputPathFiles)
            {
                string fileName = Path.GetFileName(fullPathStr);
                fileName = Path.ChangeExtension(fileName, ".txt");

                string outPath = Path.Combine(outputPath, fileName);

                Console.WriteLine($"Disassembling {Path.GetFileName(fullPathStr)}");
                DisassembleSingleFile(fullPathStr, outPath, sourceMode);
            }
        }

        static void Main(string[] args)
        {
            bool sourceMode = false;
            bool validateMode = false;
            bool dirMode = false;

            if (args.Length < 2)
            {
                PrintUsage();
                return;
            }

            int endOpts = 0;

            while (endOpts < args.Length)
            {
                string opt = args[endOpts];

                if (opt == "-src")
                    sourceMode = true;
                else if (opt == "-validate")
                    validateMode = true;
                else if (opt == "-dir")
                    dirMode = true;
                else if (opt.StartsWith("-") && opt != "-")
                {
                    PrintUsage();
                    return;
                }
                else
                    break;

                endOpts++;
            }

            if (args.Length - endOpts != 2)
            {
                PrintUsage();
                return;
            }

            string inputPath = args[endOpts];
            string outputPath = args[endOpts + 1];

            if (dirMode)
                DisassembleDirectory(inputPath, outputPath, sourceMode);
            else
                DisassembleSingleFile(inputPath, outputPath, sourceMode);
        }
    }
}
