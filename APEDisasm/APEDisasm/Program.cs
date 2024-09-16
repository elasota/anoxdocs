using System.Diagnostics;
using System.Globalization;

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

    internal class ByteString : Object
    {
        public byte[] Bytes { get; private set; }

        public ByteString()
        {
            Bytes = new byte[0];
        }

        public ByteString(byte[] bytes)
        {
            Bytes = bytes;
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

        public static ByteString FromAsciiString(string str)
        {
            return new ByteString(System.Text.Encoding.UTF8.GetBytes(str));
        }

        public override bool Equals(object? other)
        {
            ByteString? otherByteStr = other as ByteString;
            if (otherByteStr == null)
                return false;

            byte[] thisArray = Bytes;
            byte[] otherArray = otherByteStr.Bytes;

            if (thisArray.Length != otherArray.Length)
                return false;

            for (int i = 0; i < thisArray.Length; i++)
            {
                if (thisArray[i] != otherArray[i])
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            return Bytes.GetHashCode();
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

        public ISet<long>? LabelTracker { get; set; }
        public long Position { get { return _stream.Position; } }


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

        public void MarkSwitchLabel()
        {
            if (LabelTracker != null)
                LabelTracker.Add(_stream.Position);
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

        public void WriteLine(string value)
        {
            byte[] encoded = System.Text.Encoding.UTF8.GetBytes(value);
            _stream.Write(encoded, 0, encoded.Length);
            _stream.WriteByte(10);
        }

        public void WriteLineIndented(int indentLevel, string value)
        {
            WriteIndent(indentLevel);
            WriteLine(value);
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

    internal enum WindowCommandType
    {
        Title,
        Talk,
        Dimensions,
        Image,
        Flags,
        SubWindow,
        Choice,
        SimpleStringCommand,
        XYPrintFX,
        Switch,
        Body,
        Background,
        Cam,
    }

    internal interface IWindowCommand
    {
        public void Load(InputStream inStream, int indent, OutputStream? disasmStream);
        public WindowCommandType WindowCommandType { get; }
    }

    internal class SwitchCommand
    {
        public enum ECommandType
        {
            NoOpCommand = 0,
            IfCommand = 1,
            SetFloatCommand = 2,
            SetStringCommand = 3,
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
            SetFocusCommand = 21,
        }

        public OptionalString Str { get; private set; }
        public ECommandType CommandType { get; private set; }
        public FormattingValue FormattingValue { get; private set; }
        public OptionalExpression Expression { get; private set; }

        public SwitchCommand(byte commandByte)
        {
            CommandType = (ECommandType)commandByte;
            Str = new OptionalString();
            FormattingValue = new FormattingValue();
            Expression = new OptionalExpression();
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, CommandType.ToString());

            Str.Load(inStream, indent + 1, disasmStream);
            FormattingValue.Load(inStream, indent + 1, disasmStream);
            Expression.Load(inStream, indent + 1, disasmStream);
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


        public WindowCommandType WindowCommandType { get { return WindowCommandType.Switch; } }

        public WindowSwitchCommand(byte commandByte)
        {
            CommandType = (ECommandType)commandByte;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            inStream.MarkSwitchLabel();

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

        public WindowCommandType WindowCommandType { get { return WindowCommandType.SimpleStringCommand; } }

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

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Body; } }

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
    }

    internal class ChoiceCommand : IWindowCommand
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

        public enum EOperandType
        {
            Expression,
            FloatVar,
            StringVar,
            FloatConst,
            StringConst,
        }

        public EOperator Operator { get; private set; }
        public ulong LeftPrefix { get; private set; }
        public IExpressionOperand Left { get; private set; }
        public ulong RightPrefix { get; private set; }
        public IExpressionOperand Right { get; private set; }

        private uint _exprFlags;

        public EOperandType LeftOperandType
        {
            get
            {
                return OperandTypeFromFlags(_exprFlags & 0x15) ;
            }
        }

        public EOperandType RightOperandType
        {
            get
            {
                return OperandTypeFromFlags((_exprFlags >> 1) & 0x15);
            }
        }

        public ExpressionValue()
        {
            Left = new InvalidOperand();
            Right = Left;
            _exprFlags = 0;
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

        private static EOperandType OperandTypeFromFlags(uint flags)
        {
            if (flags == 0)
                return EOperandType.Expression;
            if (flags == 4)
                return EOperandType.FloatConst;
            if (flags == 5)
                return EOperandType.FloatVar;
            if (flags == 16)
                return EOperandType.StringConst;
            if (flags == 17)
                return EOperandType.StringVar;

            throw new Exception("Internal error: Unhandled expr flags");
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            byte exprOperator = inStream.ReadByte();
            byte exprFlags = inStream.ReadByte();

            if (exprOperator == 0 || exprOperator > 13)
                inStream.ReportError($"Unknown expr operator {exprOperator}");

            Operator = (EOperator)exprOperator;
            _exprFlags = exprFlags;

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
                string leftType = OperandTypeFromFlags(leftSideFlags).ToString();
                string rightType = OperandTypeFromFlags(rightSideFlags).ToString();

                disasmStream.WriteLineIndented(indent + 1, $"ExpressionType({leftType}, {rightType})");
                disasmStream.WriteLineIndented(indent + 1, $"LeftTreePos({LeftPrefix})");
            }

            Left.Load(inStream, indent + 1, disasmStream);

            RightPrefix = inStream.ReadUInt64();

            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent + 1, $"LeftTreePos({RightPrefix})");

            Right.Load(inStream, indent + 1, disasmStream);
        }

        public bool TryResolveFloatConstant(out float v)
        {
            if (Left is FloatOperand && Right is FloatOperand && Operator == EOperator.Add)
            {
                float v1 = ((FloatOperand)Left).Value;
                if (v1 != 0.0f)
                {
                    v = 0.0f;
                    return false;
                }

                v = ((FloatOperand)Right).Value;
                return true;
            }

            v = 0.0f;
            return false;
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

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Title; } }

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


        public WindowCommandType WindowCommandType { get { return WindowCommandType.XYPrintFX; } }

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

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Cam; } }

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

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Talk; } }

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

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Dimensions; } }
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
    }

    internal class FlagsCommand : IWindowCommand
    {
        public enum FlagBit
        {
            Persist = 0,
            NoBackground = 1,
            NoScroll = 2,
            NoGrab = 3,
            NoRelease = 4,
            Subtitle = 5,
            Passive2D = 29,
            Passive = 30,
        }

        public uint Flags { get; private set; }

        public WindowCommandType WindowCommandType { get { return WindowCommandType.Flags; } }

        public bool HasFlag(FlagBit flag)
        {
            uint mask = (uint)1 << (int)flag;
            return (Flags & mask) != 0;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            Flags = inStream.ReadUInt32();

            if (disasmStream != null)
            {
                disasmStream.WriteLineIndented(indent, "FlagsCommand");

                uint scanFlags = Flags;
                string flagsDesc = "";

                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.Persist, "persist");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.NoBackground, "nobackground");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.NoScroll, "noscroll");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.NoGrab, "nograb");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.NoRelease, "norelease");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.Subtitle, "subtitle");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.Passive2D, "passive2d");
                FlagUtil.DetectFlag(ref scanFlags, ref flagsDesc, (int)FlagBit.Passive, "passive");
                FlagUtil.AddUnknownFlags(scanFlags, ref flagsDesc);

                disasmStream.WriteIndent(indent + 1);
                disasmStream.WriteString($"Flags({flagsDesc})\n");
            }
        }
    }

    internal class SubWindowCommand : IWindowCommand
    {
        public uint Label { get; private set; }

        public WindowCommandType WindowCommandType { get { return WindowCommandType.SubWindow; } }

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

                if (commandByte > 21)
                {
                    if (commandByte == 69)
                    {
                        if (cc != 0)
                            inStream.ReportError("Invalid cc code for end command");

                        if (disasmStream != null)
                            disasmStream.WriteLineIndented(indent, "EndCommand");
                        return;
                    }

                    inStream.ReportError($"Invalid switch command code {commandByte}");
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
            {
                disasmStream.WriteLine($"FilePosition({inStream.Position})");
                disasmStream.WriteLineIndented(indent, $"Switch({Label})");
            }

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

            inStream.MarkSwitchLabel();
            uint label = inStream.ReadUInt32();

            while (label != 0)
            {
                Switch sw = new Switch(label);
                sw.Load(inStream, indent, disasmStream);

                _switchList.Add(sw);

                inStream.MarkSwitchLabel();
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
                    if (disasmStream != null)
                        disasmStream.WriteLine($"FilePosition({inStream.Position})");

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

    internal class Decompiler
    {
        private HashSet<uint> _inlinedSwitches;
        private Dictionary<uint, Switch> _idToSwitch;
        private APEFile _apeFile;
        private ByteString _talkPlayerChar0ConstStr;
        private ByteString _talkClickConstStr;

        public Decompiler()
        {
            _inlinedSwitches = new HashSet<uint>();
            _idToSwitch = new Dictionary<uint, Switch>();
            _apeFile = new APEFile();
            _talkPlayerChar0ConstStr = ByteString.FromAsciiString("playerchar0");
            _talkClickConstStr = ByteString.FromAsciiString("_click_");
        }

        public void Load(APEFile apeFile)
        {
            _apeFile = apeFile;

            foreach (Switch sw in _apeFile.RootElementList.Switches.SwitchList)
                _idToSwitch[sw.Label] = sw;
        }

        private static string IdToLabel(uint id)
        {
            return $"{id / 10000}:{id % 10000}";
        }

        private void DumpString(ByteString byteString, OutputStream outStream)
        {
            outStream.WriteBytes(byteString.Bytes);
        }

        private void DumpFloat(float f, OutputStream outStream)
        {
            outStream.WriteString(f.ToString("G", CultureInfo.InvariantCulture));
        }

        private void DumpQuotedString(ByteString byteString, OutputStream outStream)
        {
            byte[] bytes = byteString.Bytes;

            int numRestrictedCharacters = 0;
            foreach (byte b in byteString.Bytes)
            {
                if (b == 10 || b == 92 || b == 34)
                    numRestrictedCharacters++;
            }

            if (numRestrictedCharacters > 0)
            {
                byte[] newBytes = new byte[bytes.Length + numRestrictedCharacters];
                int outOffset = 0;
                foreach (byte b in byteString.Bytes)
                {
                    if (b == 10)
                    {
                        // Escape \n
                        newBytes[outOffset++] = 92;
                        newBytes[outOffset++] = 110;
                    }
                    else if (b == 92)
                    {
                        // Escape \\
                        newBytes[outOffset++] = 92;
                        newBytes[outOffset++] = 92;
                    }
                    else if (b == 34)
                    {
                        // Escape \"
                        newBytes[outOffset++] = 92;
                        newBytes[outOffset++] = 34;
                    }
                    else
                        newBytes[outOffset++] = b;
                }

                bytes = newBytes;
            }

            byte[] quote = new byte[1];
            quote[0] = 34;
            outStream.WriteBytes(quote);
            outStream.WriteBytes(bytes);
            outStream.WriteBytes(quote);
        }

        private void DumpConditionPrefix(OptionalExpression condition, OutputStream outStream)
        {
            if (condition.Expression == null)
                return;

            outStream.WriteString("if (");
            DumpOptimizableExpression(condition.Expression, outStream);
            outStream.WriteLine(")");
            outStream.WriteString("\t");
        }

        private void DumpFormattingValue(FormattingValue value, OutputStream outStream)
        {
            foreach (TypedFormattingValue tfv in value.Values)
            {
                ExpressionValue.EOperandType opType = ExpressionValue.EOperandType.Expression;

                switch (tfv.Type)
                {
                    case TypedFormattingValue.EFormattingValueType.Float:
                        opType = ExpressionValue.EOperandType.FloatConst;
                        break;
                    case TypedFormattingValue.EFormattingValueType.String:
                        opType = ExpressionValue.EOperandType.StringConst;
                        break;
                    case TypedFormattingValue.EFormattingValueType.VariableName:
                        opType = ExpressionValue.EOperandType.FloatVar;
                        break;
                    case TypedFormattingValue.EFormattingValueType.StringVariableName:
                        opType = ExpressionValue.EOperandType.StringVar;
                        break;
                    default:
                        throw new Exception("Invalid formatting value type");
                }

                outStream.WriteString(", ");
                DumpExpressionOperand(opType, tfv.Value, outStream);
            }
        }

        private void DumpTitleCommand(TitleCommand command, OutputStream outStream)
        {
            DumpConditionPrefix(command.Condition, outStream);
            outStream.WriteString("title ");
            DumpQuotedString(command.Text, outStream);
            DumpFormattingValue(command.FormattingValue, outStream);
            outStream.WriteLine("");
        }

        private void DumpTalkCommand(TalkCommand command, OutputStream outStream)
        {
            // Syntaxes:
            // "talk npc" -> Name1 == _click_, Name2 = playerchar0, nostay applies to Stay2
            // "talk player" -> Name1 = playerchar0, Name2 = _click_, nostay applies to Stay2

            if (command.Animation2.Value != null)
            {
                outStream.WriteString("talk_ex ");
                DumpString(command.Name1, outStream);
                outStream.WriteString(" ");
                DumpString(command.Name2, outStream);
                outStream.WriteString(" ");
                DumpString(command.Animation1, outStream);
                outStream.WriteString(" ");
                DumpString(command.Animation2.Value, outStream);

                if (command.Stay1Flag == 0 || command.Stay2Flag == 0)
                {
                    if (command.Stay1Flag != 0)
                        outStream.WriteString(" stay");
                    else
                        outStream.WriteString(" nostay");

                    if (command.Stay2Flag != 0)
                        outStream.WriteString(" stay");
                    else
                        outStream.WriteString(" nostay");
                }
            }
            else
            {
                if (command.Name1.Equals(_talkClickConstStr) && command.Name2.Equals(_talkPlayerChar0ConstStr))
                    outStream.WriteString("talk player ");
                else if (command.Name2.Equals(_talkClickConstStr) && command.Name1.Equals(_talkPlayerChar0ConstStr))
                    outStream.WriteString("talk npc ");
                else
                    throw new Exception("Unhandled talk command configuration");

                DumpString(command.Animation1, outStream);

                if (command.Stay2Flag == 0)
                    outStream.WriteString(" nostay");
            }

            outStream.WriteLine("");
        }

        private void DumpUnoptimizedExpression(ExpressionValue expr, OutputStream outStream)
        {
            DumpExpressionOperand(expr.LeftOperandType, expr.Left, outStream);

            switch (expr.Operator)
            {
                case ExpressionValue.EOperator.Or:
                    outStream.WriteString(" || ");
                    break;
                case ExpressionValue.EOperator.And:
                    outStream.WriteString(" && ");
                    break;
                case ExpressionValue.EOperator.Xor:
                    outStream.WriteString(" ^^ ");
                    break;
                case ExpressionValue.EOperator.Gt:
                    outStream.WriteString(" > ");
                    break;
                case ExpressionValue.EOperator.Lt:
                    outStream.WriteString(" < ");
                    break;
                case ExpressionValue.EOperator.Ge:
                    outStream.WriteString(" >= ");
                    break;
                case ExpressionValue.EOperator.Le:
                    outStream.WriteString(" <= ");
                    break;
                case ExpressionValue.EOperator.Eq:
                    outStream.WriteString(" == ");
                    break;
                case ExpressionValue.EOperator.Neq:
                    outStream.WriteString(" != ");
                    break;
                case ExpressionValue.EOperator.Add:
                    outStream.WriteString(" + ");
                    break;
                case ExpressionValue.EOperator.Sub:
                    outStream.WriteString(" - ");
                    break;
                case ExpressionValue.EOperator.Mul:
                    outStream.WriteString(" * ");
                    break;
                case ExpressionValue.EOperator.Div:
                    outStream.WriteString(" / ");
                    break;

                default:
                    throw new Exception("Unknown operator");
            }

            DumpExpressionOperand(expr.RightOperandType, expr.Right, outStream);
        }

        private void DumpExpressionOperand(ExpressionValue.EOperandType opType, IExpressionOperand operand, OutputStream outStream)
        {
            switch (opType)
            {
                case ExpressionValue.EOperandType.Expression:
                    outStream.WriteString("(");
                    DumpUnoptimizedExpression(((ExpressionValueOperand)operand).Expression, outStream);
                    outStream.WriteString(")");
                    break;
                case ExpressionValue.EOperandType.FloatConst:
                    if (!(operand is FloatOperand))
                        throw new Exception("Internal error");

                    DumpFloat(((FloatOperand)operand).Value, outStream);
                    break;
                case ExpressionValue.EOperandType.FloatVar:
                case ExpressionValue.EOperandType.StringVar:
                    DumpString(((StringOperand)operand).Value, outStream);
                    break;
                case ExpressionValue.EOperandType.StringConst:
                    DumpQuotedString(((QuotedStringOperand)operand).Value, outStream);
                    break;

                default:
                    throw new Exception("Unknown operand type");
            }
        }

        private void DumpOptimizableExpression(ExpressionValue expr, OutputStream outStream)
        {
            if (expr.Operator == ExpressionValue.EOperator.Add && expr.LeftOperandType == ExpressionValue.EOperandType.FloatConst)
            {
                ExpressionValue.EOperandType rightOpType = expr.RightOperandType;

                if (rightOpType == ExpressionValue.EOperandType.FloatConst || rightOpType == ExpressionValue.EOperandType.FloatVar)
                {
                    float fv = ((FloatOperand)expr.Left).Value;

                    if (fv == 0.0f)
                    {
                        DumpExpressionOperand(expr.RightOperandType, expr.Right, outStream);
                        return;
                    }
                }
            }

            DumpUnoptimizedExpression(expr, outStream);
        }

        private void DumpDimensionsCommand(DimensionsCommand command, OutputStream outStream)
        {
            if (command.Width.Expression != null)
            {
                outStream.WriteString("width ");
                DumpOptimizableExpression(command.Width.Expression, outStream);
                outStream.WriteLine("");
            }
            if (command.Height.Expression != null)
            {
                outStream.WriteString("height ");
                DumpOptimizableExpression(command.Height.Expression, outStream);
                outStream.WriteLine("");
            }
            if (command.XPos.Expression != null)
            {
                outStream.WriteString("xpos ");
                DumpOptimizableExpression(command.XPos.Expression, outStream);
                outStream.WriteLine("");
            }
            if (command.YPos.Expression != null)
            {
                outStream.WriteString("ypos ");
                DumpOptimizableExpression(command.YPos.Expression, outStream);
                outStream.WriteLine("");
            }
        }

        private void DumpImageCommand(ImageCommand command, bool canDumpAsBackground, OutputStream outStream)
        {
            DumpConditionPrefix(command.Condition, outStream);

            if (canDumpAsBackground && command.CanEmitAsBackground())
            {
                outStream.WriteString("background ");
                DumpQuotedString(command.FileName, outStream);

                if (command.HasFlag(ImageCommand.EImageFlagBit.Stretch))
                    outStream.WriteString(" stretch");
                if (command.HasFlag(ImageCommand.EImageFlagBit.Tile))
                    outStream.WriteString(" tile");
            }
            else
            {
                if (command.XPos.Expression == null || command.YPos.Expression == null)
                    throw new Exception("Image command with no XPos or YPos");

                outStream.WriteString("image ");
                DumpQuotedString(command.FileName, outStream);
                outStream.WriteString(" ");
                DumpOptimizableExpression(command.XPos.Expression, outStream);
                outStream.WriteString(", ");
                DumpOptimizableExpression(command.YPos.Expression, outStream);

                if (command.Width.Expression != null)
                {
                    outStream.WriteString(", ");
                    DumpOptimizableExpression(command.Width.Expression, outStream);
                }

                if (command.Height.Expression != null)
                {
                    outStream.WriteString(", ");
                    DumpOptimizableExpression(command.Height.Expression, outStream);
                }

                if (command.HasFlag(ImageCommand.EImageFlagBit.Stretch))
                    outStream.WriteString(", stretch");
                if (command.HasFlag(ImageCommand.EImageFlagBit.Tile))
                    outStream.WriteString(", tile");
                if (command.HasFlag(ImageCommand.EImageFlagBit.Solid))
                    outStream.WriteString(", solid");
            }

            outStream.WriteLine("");
        }

        private void DumpFlagsCommand(FlagsCommand command, OutputStream outStream)
        {
            // NoBackground intentionally ignored
            if (command.HasFlag(FlagsCommand.FlagBit.Persist))
                outStream.WriteLine("flags persist");
            if (command.HasFlag(FlagsCommand.FlagBit.NoScroll))
                outStream.WriteLine("flags noscroll");
            if (command.HasFlag(FlagsCommand.FlagBit.NoGrab))
                outStream.WriteLine("flags nograb");
            if (command.HasFlag(FlagsCommand.FlagBit.NoRelease))
                outStream.WriteLine("flags norelease");
            if (command.HasFlag(FlagsCommand.FlagBit.Subtitle))
                outStream.WriteLine("flags subtitle");
            if (command.HasFlag(FlagsCommand.FlagBit.Passive2D))
                outStream.WriteLine("flags passive2d");
            if (command.HasFlag(FlagsCommand.FlagBit.Passive))
                outStream.WriteLine("flags passive");
        }

        private void DumpSubwindowCommand(SubWindowCommand command, OutputStream outStream)
        {
            outStream.WriteString("subwindow ");
            outStream.WriteString(IdToLabel(command.Label));
            outStream.WriteLine("");
        }

        private void DumpChoiceCommand(ChoiceCommand command, OutputStream outStream)
        {
            DumpConditionPrefix(command.Condition, outStream);

            outStream.WriteString("choice ");
            DumpQuotedString(command.Str, outStream);
            outStream.WriteString(" ");

            if (command.FormattingValue.Values.Count > 0)
            {
                DumpFormattingValue(command.FormattingValue, outStream);
                outStream.WriteString(", ");
            }

            outStream.WriteString(IdToLabel(command.Label));
            outStream.WriteLine("");
        }

        private static bool IsStringSimpleLabel(byte[] bytes)
        {
            int length = bytes.Length;

            if (length < 3)
                return false;

            int colonPos = -1;

            for (int i = 0; i < length; i++)
            {
                byte b = bytes[i];

                if (b == 58)
                {
                    if (colonPos == -1)
                        return false;

                    colonPos = i;
                }
                else if (b < 48 || b > 57)
                    return false;
            }

            if (colonPos == -1)
                return false;

            return (colonPos > 0 && colonPos < (length - 1));
        }

        private void DumpSimpleStringCommand(SimpleStringCommand command, OutputStream outStream)
        {
            bool isQuoted = true;

            switch (command.CommandType)
            {
                case SimpleStringCommand.ECommandType.StartConsoleCommand:
                    outStream.WriteString("startconsole ");
                    break;
                case SimpleStringCommand.ECommandType.FinishConsoleCommand:
                    outStream.WriteString("finishconsole ");
                    break;
                case SimpleStringCommand.ECommandType.FontCommand:
                    outStream.WriteString("font ");
                    break;
                case SimpleStringCommand.ECommandType.StyleCommand:
                    outStream.WriteString("style ");
                    break;
                case SimpleStringCommand.ECommandType.NextWindowCommand:
                    if (IsStringSimpleLabel(command.CommandStr.Bytes))
                        outStream.WriteString("goto ");
                    else
                        outStream.WriteString("nextwindow ");

                    isQuoted = false;
                    break;
                default:
                    throw new Exception("Unknown simple string command");
            }

            if (isQuoted)
                DumpQuotedString(command.CommandStr, outStream);
            else
                DumpString(command.CommandStr, outStream);

            outStream.WriteLine("");
        }

        private void DumpXYPrintFXCommand(XYPrintFXCommand command, OutputStream outStream)
        {
            // If this only has XPos, YPos, Alpha, and String, then it's xyprint
            bool isSimpleXYPrint = (command.Red.Expression == null || command.Green.Expression == null || command.Blue.Expression == null || command.Font.Value == null);

            DumpConditionPrefix(command.Condition, outStream);

            if (isSimpleXYPrint)
                outStream.WriteString("xyprint ");
            else
                outStream.WriteString("xyprintfx ");

            if (command.XCoord.Expression == null || command.YCoord.Expression == null || command.Alpha.Expression == null)
                throw new Exception("Invalid XYPrintFX command");

            DumpOptimizableExpression(command.XCoord.Expression, outStream);
            outStream.WriteString(", ");
            DumpOptimizableExpression(command.YCoord.Expression, outStream);
            outStream.WriteString(", ");
            DumpOptimizableExpression(command.Alpha.Expression, outStream);
            outStream.WriteString(", ");

            if (!isSimpleXYPrint)
            {
                if (command.Red.Expression == null || command.Green.Expression == null || command.Blue.Expression == null || command.Font.Value == null)
                    throw new Exception("Internal error");

                DumpOptimizableExpression(command.Red.Expression, outStream);
                outStream.WriteString(", ");
                DumpOptimizableExpression(command.Green.Expression, outStream);
                outStream.WriteString(", ");
                DumpOptimizableExpression(command.Blue.Expression, outStream);
                outStream.WriteString(", ");
                DumpString(command.Font.Value, outStream);
                outStream.WriteString(", ");
            }

            DumpQuotedString(command.Message, outStream);
            DumpFormattingValue(command.FormattingValue, outStream);
            outStream.WriteLine("");
        }

        private void DumpWindowSwitchCommand(WindowSwitchCommand command, OutputStream outStream)
        {
            switch (command.CommandType)
            {
                case WindowSwitchCommand.ECommandType.StartSwitchCommand:
                    outStream.WriteString("startswitch ");
                    break;
                case WindowSwitchCommand.ECommandType.ThinkSwitchCommand:
                    outStream.WriteString("thinkswitch ");
                    break;
                case WindowSwitchCommand.ECommandType.FinishSwitchCommand:
                    outStream.WriteString("finishswitch ");
                    break;

                default:
                    throw new Exception("Unknown window switch command type");
            }

            if (command.Label >= 1000000000)
            {
                outStream.WriteLine("{");
                Switch sw = _idToSwitch[command.Label];

                DumpSwitchStatements(1, sw.CommandList, outStream);

                _inlinedSwitches.Add(command.Label);
                outStream.WriteLine("}");
            }
            else
                outStream.WriteLine(IdToLabel(command.Label));
        }

        private void DumpCamCommand(CamCommand command, OutputStream outStream)
        {
            uint notSetValue = 32769;

            outStream.WriteString("cam ");
            DumpString(command.Name, outStream);

            if (command.From.Value != null)
            {
                outStream.WriteString(" from(");
                DumpString(command.From.Value, outStream);
                outStream.WriteString(")");
            }

            if (command.To.Value != null)
            {
                outStream.WriteString(" to(");
                DumpString(command.To.Value, outStream);
                outStream.WriteString(")");
            }

            if (command.Owner.Value != null)
            {
                outStream.WriteString(" owner(");
                DumpString(command.Owner.Value, outStream);
                outStream.WriteString(")");
            }

            if (command.Yaw != notSetValue)
                outStream.WriteString($" yaw({command.Yaw})");

            if (command.Pitch != notSetValue)
                outStream.WriteString($" pitch({command.Pitch})");

            if (command.Fov != notSetValue)
                outStream.WriteString($" fov({command.Fov})");

            if (command.Far != notSetValue)
                outStream.WriteString($" far({command.Far})");

            if (command.Near != notSetValue)
                outStream.WriteString($" near({command.Near})");

            if (command.Fwd != notSetValue)
                outStream.WriteString($" fwd({command.Fwd})");

            if (command.Speed != notSetValue)
                outStream.WriteString($" speed({command.Speed})");

            if (command.Lift != notSetValue)
                outStream.WriteString($" lift({command.Lift})");

            if (command.Lag != notSetValue)
                outStream.WriteString($" lag({command.Lag})");

            if (command.Occlude != notSetValue)
            {
                if (command.Occlude == 0)
                    outStream.WriteString(" occlude(no)");
                else
                    outStream.WriteString(" occlude(yes)");
            }

            if (command.Restore != notSetValue)
                outStream.WriteString(" restore");

            if (command.Zip != notSetValue)
                outStream.WriteString(" zip");

            outStream.WriteLine("");
        }

        private void DumpBackgroundCommand(BackgroundCommand command, OutputStream outStream)
        {
            outStream.WriteString("background");

            if (command.Color1 != 0)
                outStream.WriteString(" color1=" + BackgroundCommand.ColorToHex(command.Color1));
            if (command.Color2 != 0)
                outStream.WriteString(" color2=" + BackgroundCommand.ColorToHex(command.Color2));
            if (command.Color3 != 0)
                outStream.WriteString(" color3=" + BackgroundCommand.ColorToHex(command.Color3));
            if (command.Color4 != 0)
                outStream.WriteString(" color4=" + BackgroundCommand.ColorToHex(command.Color4));

            outStream.WriteLine("");
        }

        private void DumpBodyCommand(BodyCommand command, OutputStream outStream)
        {
            DumpConditionPrefix(command.Condition, outStream);
            outStream.WriteString("body ");
            DumpQuotedString(command.BodyStr, outStream);
            DumpFormattingValue(command.FormattingValue, outStream);
            outStream.WriteLine("");
        }

        public class ChainedStatement
        {
            public ChainedStatement? NextUnconditional { get; set; }
            public ChainedStatement? TrueCase { get; set; }
            public ChainedStatement? FalseCase { get; set; }

            public SwitchCommand Command { get; private set; }

            public ulong CCLabel { get; private set; }

            public ChainedStatement(SwitchCommand cmd, ulong label)
            {
                Command = cmd;
                CCLabel = label;
            }
        };

        private void DumpSimpleSwitchCommand(string name, SwitchCommand cmd, bool quoteArg, OutputStream outStream)
        {
            outStream.WriteString(name);

            if (cmd.Str.Value != null)
            {
                outStream.WriteString(" ");

                if (quoteArg)
                    DumpQuotedString(cmd.Str.Value, outStream);
                else
                    DumpString(cmd.Str.Value, outStream);

                DumpFormattingValue(cmd.FormattingValue, outStream);
            }
        }

        private void DumpSetFloatCommand(SwitchCommand cmd, OutputStream outStream)
        {
            if (cmd.Str.Value == null)
                throw new Exception("Set command string was null");

            if (cmd.Expression.Expression == null)
            {
                outStream.WriteString("unset ");
                DumpString(cmd.Str.Value, outStream);
            }
            else
            {
                outStream.WriteString("set ");
                DumpString(cmd.Str.Value, outStream);
                outStream.WriteString(" = ");
                DumpOptimizableExpression(cmd.Expression.Expression, outStream);
            }

            DumpFormattingValue(cmd.FormattingValue, outStream);
        }

        private static byte[] SliceByteArray(byte[] bytes, int start, int end)
        {
            byte[] newBytes = new byte[end - start];

            for (int i = start; i < end; i++)
                newBytes[i - start] = bytes[i];

            return newBytes;
        }

        private void DumpSetStringCommand(SwitchCommand cmd, OutputStream outStream)
        {
            if (cmd.Str.Value == null)
                throw new Exception("Set command string was null");

            byte[] cmdBytes = cmd.Str.Value.Bytes;

            int eqPos = -1;
            for (int i = 0; i < cmdBytes.Length; i++)
            {
                if (cmdBytes[i] == 61)
                {
                    eqPos = i;
                    break;
                }
            }

            if (eqPos == -1)
            {
                outStream.WriteString("unset ");
                outStream.WriteBytes(cmdBytes);
            }
            else
            {
                byte[] varName = SliceByteArray(cmdBytes, 0, eqPos);
                byte[] valueToAssign = SliceByteArray(cmdBytes, eqPos + 1, cmdBytes.Length);

                bool needToEscape = false;
                if (valueToAssign.Length > 0)
                {
                    if (valueToAssign[0] == 34)
                    {
                        if (valueToAssign.Length < 2 || valueToAssign[valueToAssign.Length - 1] != 34)
                            throw new Exception("Set string command had unusual quote handling");

                        needToEscape = true;
                        valueToAssign = SliceByteArray(valueToAssign, 1, valueToAssign.Length - 1);
                    }
                }

                outStream.WriteString("set ");
                outStream.WriteBytes(varName);
                outStream.WriteString(" = ");

                if (needToEscape)
                    DumpQuotedString(new ByteString(valueToAssign), outStream);
                else
                    outStream.WriteBytes(valueToAssign);

                DumpFormattingValue(cmd.FormattingValue, outStream);
            }
        }

        private void DumpGotoCommand(SwitchCommand cmd, OutputStream outStream)
        {
            if (cmd.Str.Value == null)
                throw new Exception("Goto command missing argument");

            byte[] target = cmd.Str.Value.Bytes;

            if (target.Length == 3 && target[0] == 48 && target[1] == 58 && target[2] == 48)
            {
                outStream.WriteString("return");
                return;
            }

            DumpSimpleSwitchCommand("goto", cmd, false, outStream);
        }

        private void DumpSwitchCommand(SwitchCommand cmd, OutputStream outStream)
        {
            switch (cmd.CommandType)
            {
                case SwitchCommand.ECommandType.NoOpCommand:
                    // Only emitted in empty
                    break;

                case SwitchCommand.ECommandType.SetFloatCommand:
                    DumpSetFloatCommand(cmd, outStream);
                    break;

                case SwitchCommand.ECommandType.SetStringCommand:
                    DumpSetStringCommand(cmd, outStream);
                    break;

                case SwitchCommand.ECommandType.GotoCommand:
                    DumpGotoCommand(cmd, outStream);
                    break;

                case SwitchCommand.ECommandType.GoSubCommand:
                    DumpSimpleSwitchCommand("gosub", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.ConsoleCommand:
                    DumpSimpleSwitchCommand("console", cmd, true, outStream);
                    break;

                case SwitchCommand.ECommandType.EchoCommand:
                    DumpSimpleSwitchCommand("echo", cmd, true, outStream);
                    break;

                case SwitchCommand.ECommandType.TargetCommand:
                    DumpSimpleSwitchCommand("target", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.PathTargetCommand:
                    DumpSimpleSwitchCommand("pathtarget", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.ExternCommand:
                    DumpSimpleSwitchCommand("extern", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.PlayAmbientCommand:
                    DumpSimpleSwitchCommand("playambient", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.LoopAmbientCommand:
                    DumpSimpleSwitchCommand("loopambient", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.StopAmbientCommand:
                    DumpSimpleSwitchCommand("stopambient", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.PlaySceneCommand:
                    DumpSimpleSwitchCommand("playscene", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.LoopSceneCommand:
                    DumpSimpleSwitchCommand("loopscene", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.StopSceneCommand:
                    DumpSimpleSwitchCommand("stopscene", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.ChainScriptsCommand:
                    DumpSimpleSwitchCommand("chainscripts", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.CloseWindowCommand:
                    DumpSimpleSwitchCommand("closewindow", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.LoadAPECommand:
                    DumpSimpleSwitchCommand("loadape", cmd, true, outStream);
                    break;

                case SwitchCommand.ECommandType.SetFocusCommand:
                    DumpSimpleSwitchCommand("setfocus", cmd, true, outStream);
                    break;

                default:
                    throw new Exception("Invalid switch command");
            }
        }

        private static bool IsCommandTypeControlFlow(SwitchCommand.ECommandType ctype)
        {
            return (ctype == SwitchCommand.ECommandType.IfCommand || ctype == SwitchCommand.ECommandType.WhileCommand);
        }

        private void DumpChainedStatementBlock(int indent, bool skipInitialIndent, ChainedStatement? stmt, OutputStream outStream)
        {
            int numIndentChars = indent * 2;

            byte[] indentBytes = new byte[numIndentChars];

            for (int i = 0; i < numIndentChars; i++)
                indentBytes[i] = 32;

            while (stmt != null)
            {
                SwitchCommand cmd = stmt.Command;

                if (IsCommandTypeControlFlow(cmd.CommandType))
                {
                    ChainedStatement? trueCaseStmt = stmt.TrueCase;
                    if (trueCaseStmt == null)
                        throw new Exception("Control flow block was empty");

                    ChainedStatement? falseCaseStmt = stmt.FalseCase;
                    if (falseCaseStmt != null && cmd.CommandType != SwitchCommand.ECommandType.IfCommand)
                        throw new Exception("False case existed for non-if statement");

                    if (cmd.Expression.Expression == null)
                        throw new Exception("Control flow missing condition");

                    if (!skipInitialIndent)
                        outStream.WriteBytes(indentBytes);

                    if (cmd.CommandType == SwitchCommand.ECommandType.IfCommand)
                        outStream.WriteString("if (");
                    if (cmd.CommandType == SwitchCommand.ECommandType.WhileCommand)
                        outStream.WriteString("while (");

                    DumpOptimizableExpression(cmd.Expression.Expression, outStream);

                    outStream.WriteString(")");

                    bool isTrueCaseSingleCommand = false;
                    if (!IsCommandTypeControlFlow(trueCaseStmt.Command.CommandType) && trueCaseStmt.NextUnconditional == null)
                        isTrueCaseSingleCommand = true;

                    if (!isTrueCaseSingleCommand)
                        outStream.WriteString(" {");
                    outStream.WriteLine("");

                    DumpChainedStatementBlock(indent + 1, false, trueCaseStmt, outStream);

                    bool isTrailingBrace = false;
                    if (!isTrueCaseSingleCommand)
                    {
                        outStream.WriteBytes(indentBytes);
                        outStream.WriteString("}");
                        isTrailingBrace = true;
                    }

                    if (falseCaseStmt != null)
                    {
                        if (isTrailingBrace)
                            outStream.WriteString(" ");
                        else
                            outStream.WriteBytes(indentBytes);

                        bool isFalseCaseSingleCommand = false;
                        bool isElseIf = false;
                        if (falseCaseStmt.Command.CommandType != SwitchCommand.ECommandType.WhileCommand && falseCaseStmt.NextUnconditional == null)
                        {
                            isFalseCaseSingleCommand = true;
                            if (falseCaseStmt.Command.CommandType == SwitchCommand.ECommandType.IfCommand)
                                isElseIf = true;
                        }


                        isTrailingBrace = false;
                        if (isElseIf)
                        {
                            outStream.WriteString("else ");
                            DumpChainedStatementBlock(indent, true, falseCaseStmt, outStream);
                        }
                        else if (isFalseCaseSingleCommand)
                        {
                            outStream.WriteLine("else");
                            DumpChainedStatementBlock(indent + 1, false, falseCaseStmt, outStream);
                        }
                        else
                        {
                            outStream.WriteLine("else {");
                            outStream.WriteBytes(indentBytes);
                            DumpChainedStatementBlock(indent + 1, false, falseCaseStmt, outStream);
                            outStream.WriteString("}");
                            isTrailingBrace = true;
                        }
                    }

                    if (isTrailingBrace)
                        outStream.WriteLine("");
                }
                else
                {
                    outStream.WriteBytes(indentBytes);
                    DumpSwitchCommand(cmd, outStream);
                    outStream.WriteLine("");
                }

                stmt = stmt.NextUnconditional;
                skipInitialIndent = false;
            }
        }

        private void DumpSwitchStatements(int startIndent, SwitchCommandList commandList, OutputStream outStream)
        {
            Dictionary<ulong, ChainedStatement> ccToStatement = new Dictionary<ulong, ChainedStatement>();

            foreach (CCPrefixedCommand prefixedCommand in commandList.Commands)
            {
                ChainedStatement stmt = new ChainedStatement(prefixedCommand.Command, prefixedCommand.ConditionControl);
                ccToStatement[prefixedCommand.ConditionControl] = stmt;
            }

            foreach (ChainedStatement stmt in ccToStatement.Values)
            {
                if (stmt.CCLabel == 1)
                    continue;

                ulong precedingInstrCC = (stmt.CCLabel >> 2);
                ChainedStatement precedingInstr = ccToStatement[precedingInstrCC];

                switch ((int)(stmt.CCLabel & 3))
                {
                    default:
                    case 0:
                        throw new Exception("Invalid CC context");
                    case 1:
                        if (precedingInstr == null || precedingInstr.TrueCase != null)
                            throw new Exception("Invalid CC context");
                        precedingInstr.TrueCase = stmt;
                        break;
                    case 2:
                        if (precedingInstr == null || precedingInstr.FalseCase != null)
                            throw new Exception("Invalid CC context");
                        precedingInstr.FalseCase = stmt;
                        break;
                    case 3:
                        if (precedingInstr == null || precedingInstr.NextUnconditional != null)
                            throw new Exception("Invalid CC context");
                        precedingInstr.NextUnconditional = stmt;
                        break;
                }
            }

            ChainedStatement? firstStatement = null;
            if (ccToStatement.TryGetValue(1, out firstStatement))
                DumpChainedStatementBlock(startIndent, false, firstStatement, outStream);
        }

        public void Dump(OutputStream outStream)
        {
            foreach (Window window in _apeFile.RootElementList.Windows)
            {
                outStream.WriteLine($"#window {IdToLabel(window.WindowId)}");

                bool hasNoBackgroundFlag = false;
                bool hasBackgroundCommand = false;
                bool hasBackgroundEmittableImage = false;
                foreach (IWindowCommand cmd in window.CommandList.Commands)
                {
                    WindowCommandType cmdType = cmd.WindowCommandType;

                    if (cmdType == WindowCommandType.Flags)
                        hasNoBackgroundFlag = ((FlagsCommand)cmd).HasFlag(FlagsCommand.FlagBit.NoBackground);
                    else if (cmdType == WindowCommandType.Background)
                        hasBackgroundCommand = true;
                    else if (cmdType == WindowCommandType.Image)
                    {
                        if (((ImageCommand)cmd).CanEmitAsBackground())
                            hasBackgroundEmittableImage = true;
                    }
                }

                bool shouldEmitImageAsBackground = (hasBackgroundEmittableImage && !hasBackgroundCommand && !hasNoBackgroundFlag);

                foreach (IWindowCommand cmd in window.CommandList.Commands)
                {
                    switch (cmd.WindowCommandType)
                    {
                        case WindowCommandType.Title:
                            DumpTitleCommand((TitleCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Talk:
                            DumpTalkCommand((TalkCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Dimensions:
                            DumpDimensionsCommand((DimensionsCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Image:
                            DumpImageCommand((ImageCommand)cmd, shouldEmitImageAsBackground, outStream);
                            break;
                        case WindowCommandType.Flags:
                            DumpFlagsCommand((FlagsCommand)cmd, outStream);
                            break;
                        case WindowCommandType.SubWindow:
                            DumpSubwindowCommand((SubWindowCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Choice:
                            DumpChoiceCommand((ChoiceCommand)cmd, outStream);
                            break;
                        case WindowCommandType.SimpleStringCommand:
                            DumpSimpleStringCommand((SimpleStringCommand)cmd, outStream);
                            break;
                        case WindowCommandType.XYPrintFX:
                            DumpXYPrintFXCommand((XYPrintFXCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Switch:
                            DumpWindowSwitchCommand((WindowSwitchCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Body:
                            DumpBodyCommand((BodyCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Background:
                            DumpBackgroundCommand((BackgroundCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Cam:
                            DumpCamCommand((CamCommand)cmd, outStream);
                            break;
                        default:
                            throw new Exception("Unknown window command type");
                    }
                }

                if (!hasNoBackgroundFlag && !shouldEmitImageAsBackground && !hasBackgroundCommand)
                {
                    outStream.WriteLine("background color1=00000000");
                }

                outStream.WriteLine("");
            }

            foreach (Switch sw in _apeFile.RootElementList.Switches.SwitchList)
            {
                if (_inlinedSwitches.Contains(sw.Label))
                    continue;

                outStream.WriteLine($"#switch {IdToLabel(sw.Label)}");
                DumpSwitchStatements(0, sw.CommandList, outStream);
            }
        }
    }

    internal class Program
    {

        static void PrintUsage()
        {
            Console.Error.WriteLine("Syntax: APEDisasm [options] <input> <output>");
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("    -src                            Decompile to source code");
            Console.Error.WriteLine("    -validate <path to dparse.exe>  Validate decompiled results");
            Environment.ExitCode = -1;
        }

        static void Decompile(Stream inStream, Stream outStream, bool enableInlineTracking, out IReadOnlySet<long>? labelLocations)
        {
            labelLocations = null;

            APEFile apeFile = new APEFile();
            InputStream wrappedInStream = new InputStream(inStream);
            OutputStream decompileStream = new OutputStream(outStream);

            if (enableInlineTracking)
            {
                HashSet<long> labelLocsHashSet = new HashSet<long>();
                wrappedInStream.LabelTracker = labelLocsHashSet;
                labelLocations = labelLocsHashSet;
            }

            apeFile.Load(wrappedInStream, null);

            Decompiler decompiler = new Decompiler();
            decompiler.Load(apeFile);

            decompiler.Dump(decompileStream);
        }

        static void Disassemble(Stream inStream, Stream outStream)
        {
            OutputStream disasmStream = new OutputStream(outStream);

            APEFile apeFile = new APEFile();

            apeFile.Load(new InputStream(inStream), disasmStream);
        }

        static bool CompareBytes(Stream streamA, Stream streamB, long size)
        {
            if (size == 0)
                return true;

            int bufferSize = 1024;
            if (size < 1024)
                bufferSize = (int)size;

            byte[] bytesA = new byte[bufferSize];
            byte[] bytesB = new byte[bufferSize];

            for (long i = 0; i < size; i += bufferSize)
            {
                int chunkSize = (int)Math.Min(bufferSize, size - i);

                streamA.Read(bytesA, 0, chunkSize);
                streamB.Read(bytesB, 0, chunkSize);

                for (int j = 0; j < chunkSize; j++)
                {
                    if (bytesA[j] != bytesB[j])
                        return false;
                }
            }

            return true;
        }

        static bool CompareLabels(Stream streamA, Stream streamB)
        {
            byte[] bytes = new byte[4];
            streamA.Read(bytes, 0, 4);

            uint streamALabel = 0;
            for (int i = 0; i < 4; i++)
                streamALabel |= (uint)(bytes[i] << (i * 8));

            streamB.Read(bytes, 0, 4);

            uint streamBLabel = 0;
            for (int i = 0; i < 4; i++)
                streamBLabel |= (uint)(bytes[i] << (i * 8));

            // Non-inline IDs must match exactly
            if (streamALabel < 1000000000 || streamBLabel < 1000000000)
                return streamALabel == streamBLabel;

            uint streamAGenIndex = streamALabel % 10000u;
            uint streamBGenIndex = streamBLabel % 10000u;

            uint streamAHigh = streamALabel / 1000000000u;
            uint streamBHigh = streamBLabel / 1000000000u;

            if (streamAGenIndex != streamBGenIndex)
                return false;

            if (streamAHigh != streamBHigh)
                return false;

            return true;
        }

        static void ValidateFile(string apePath, string sourcePath, string dparsePath, IReadOnlySet<long>? labelLocations)
        {
            if (labelLocations == null)
                throw new Exception("Internal error: No label location tracker");

            List<long> sortedLabelLocations = new List<long>();
            foreach (long labelLocation in labelLocations)
                sortedLabelLocations.Add(labelLocation);

            sortedLabelLocations.Sort();

            Console.WriteLine("Validating {0}", sourcePath);

            string recompiledPath = Path.ChangeExtension(sourcePath, "ape");
            if (File.Exists(recompiledPath))
            {
                Console.WriteLine("FAILED: Compiled output already exists");
                return;
            }

            try
            {
                List<string> args = new List<string>();

                args.Add(Path.GetFileName(sourcePath));

                string? sourceDir = Path.GetDirectoryName(sourcePath);
                if (sourceDir == null)
                    throw new Exception("Source path wasn't in a directory");

                ProcessStartInfo psi = new ProcessStartInfo(dparsePath, args);
                psi.WorkingDirectory = sourceDir;


                System.Diagnostics.Process? process = System.Diagnostics.Process.Start(psi);

                if (process == null)
                {
                    Console.WriteLine("FAILED: dparse didn't start");
                    return;
                }


                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine("FAILED: dparse crashed");
                    return;
                }

                using (FileStream apeFile = new FileStream(apePath, FileMode.Open, FileAccess.Read))
                {
                    using (FileStream recompiledFile = new FileStream(recompiledPath, FileMode.Open, FileAccess.Read))
                    {
                        if (apeFile.Length != recompiledFile.Length)
                        {
                            if (recompiledFile.Length == 0)
                                Console.WriteLine("FAILED: dparse compile failed");
                            else
                                Console.WriteLine("FAILED: File lengths are different");
                            return;
                        }

                        long lastReadStart = 0;
                        foreach (long labelLocation in sortedLabelLocations)
                        {
                            long chunkSize = labelLocation - lastReadStart;

                            if (chunkSize > 0)
                            {
                                if (!CompareBytes(apeFile, recompiledFile, chunkSize))
                                {
                                    Console.WriteLine($"FAILED: File contents were different (strict check in range {lastReadStart}..{labelLocation} failed)");
                                    return;
                                }
                            }

                            if (!CompareLabels(apeFile, recompiledFile))
                            {
                                Console.WriteLine($"FAILED: File contents were different (label compare check at {labelLocation} failed)");
                                return;
                            }

                            lastReadStart = labelLocation + 4;
                        }

                        long remainderSize = apeFile.Length - lastReadStart;
                        if (!CompareBytes(apeFile, recompiledFile, remainderSize))
                        {
                            Console.WriteLine($"FAILED: File contents were different (trailing strict check at {lastReadStart} failed)");
                            return;
                        }
                    }
                }

                Console.WriteLine("PASSED");
            }
            finally
            {
                if (File.Exists(recompiledPath))
                {
                    try
                    {
                        File.Delete(recompiledPath);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        static void DisassembleSingleFile(string inputPath, string outputPath, bool sourceMode, bool validateMode, string dparsePath)
        {
            IReadOnlySet<long>? labelLocations = null;

            using (FileStream inFile = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
            {
                using (FileStream outFile = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    try
                    {
                        if (sourceMode)
                            Decompile(inFile, outFile, validateMode, out labelLocations);
                        else
                            Disassemble(inFile, outFile);
                    }
                    catch (Exception)
                    {
                        outFile.Flush();
                        throw;
                    }
                }
            }

            if (validateMode && sourceMode)
            {
                ValidateFile(inputPath, outputPath, dparsePath, labelLocations);
            }
        }

        static void DisassembleDirectory(string inputPath, string outputPath, bool sourceMode, bool validateMode, string dparsePath)
        {
            string[] inputPathFiles = Directory.GetFiles(inputPath);

            foreach (string fullPathStr in inputPathFiles)
            {
                string fileName = Path.GetFileName(fullPathStr);
                fileName = Path.ChangeExtension(fileName, ".txt");

                string outPath = Path.Combine(outputPath, fileName);

                Console.WriteLine($"Disassembling {Path.GetFileName(fullPathStr)}");
                DisassembleSingleFile(fullPathStr, outPath, sourceMode, validateMode, dparsePath);
            }
        }

        static void Main(string[] args)
        {
            bool sourceMode = false;
            bool validateMode = false;
            bool dirMode = false;
            string validatePath = "";

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
                {
                    validateMode = true;
                    endOpts++;
                    if (endOpts == args.Length)
                    {
                        PrintUsage();
                        return;
                    }

                    validatePath = args[endOpts];
                }
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

            if (validateMode && !sourceMode)
            {
                PrintUsage();
                return;
            }

            string inputPath = args[endOpts];
            string outputPath = args[endOpts + 1];

            if (dirMode)
                DisassembleDirectory(inputPath, outputPath, sourceMode, validateMode, validatePath);
            else
                DisassembleSingleFile(inputPath, outputPath, sourceMode, validateMode, validatePath);
        }
    }
}
