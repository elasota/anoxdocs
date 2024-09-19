namespace AnoxAPE.Elements
{
    public class ExpressionValue
    {
        public enum EOperator
        {
            Invalid = 0,
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
                return OperandTypeFromFlags(_exprFlags & 0x15);
            }
            set
            {
                _exprFlags = _exprFlags - (_exprFlags & 0x15) + FlagsFromOperandType(value);
            }
        }

        public EOperandType RightOperandType
        {
            get
            {
                return OperandTypeFromFlags((_exprFlags >> 1) & 0x15);
            }
            set
            {
                _exprFlags = _exprFlags - (_exprFlags & 0x2a) + (FlagsFromOperandType(value) << 1);
            }
        }

        public ExpressionValue()
        {
            Left = new InvalidOperand();
            Right = Left;
            _exprFlags = 0;
        }

        public ExpressionValue(EOperator op, ulong leftPrefix, EOperandType leftOpType, IExpressionOperand left, ulong rightPrefix, EOperandType rightOpType, IExpressionOperand right)
        {
            _exprFlags = 0;
            Operator = op;
            LeftPrefix = leftPrefix;
            Left = left;
            RightPrefix = rightPrefix;
            Right = right;
            LeftOperandType = leftOpType;
            RightOperandType = rightOpType;
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

        private static uint FlagsFromOperandType(EOperandType opType)
        {
            switch (opType)
            {
                case EOperandType.Expression:
                    return 0;
                case EOperandType.FloatConst:
                    return 4;
                case EOperandType.FloatVar:
                    return 5;
                case EOperandType.StringConst:
                    return 16;
                case EOperandType.StringVar:
                    return 17;
                default:
                    throw new Exception("Internal error: Unhandled expr flags");
            }
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
                disasmStream.WriteLineIndented(indent + 1, $"RightTreePos({RightPrefix})");

            Right.Load(inStream, indent + 1, disasmStream);
        }


        public void Write(OutputStream outStream)
        {
            outStream.WriteByte((byte)Operator);
            outStream.WriteByte((byte)_exprFlags);
            outStream.WriteUInt64(LeftPrefix);
            Left.Write(outStream);
            outStream.WriteUInt64(RightPrefix);
            Right.Write(outStream);
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
}
