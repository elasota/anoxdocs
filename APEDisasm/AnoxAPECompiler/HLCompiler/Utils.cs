using AnoxAPE;
using AnoxAPE.Elements;

namespace AnoxAPECompiler.HLCompiler
{
    internal class Utils
    {
        public static bool IsWhitespace(byte b)
        {
            return b <= ' ';
        }

        internal static ExprResultType ResolveResultType(IExprValue left, IExprValue right, ExpressionValue.EOperator op)
        {
            switch (op)
            {
                case ExpressionValue.EOperator.Eq:
                case ExpressionValue.EOperator.Neq:
                    if (left.ResultType != right.ResultType)
                        return ExprResultType.Invalid;

                    return ExprResultType.Float;

                case ExpressionValue.EOperator.Or:
                case ExpressionValue.EOperator.And:
                case ExpressionValue.EOperator.Xor:
                case ExpressionValue.EOperator.Gt:
                case ExpressionValue.EOperator.Lt:
                case ExpressionValue.EOperator.Ge:
                case ExpressionValue.EOperator.Le:
                case ExpressionValue.EOperator.Add:
                case ExpressionValue.EOperator.Sub:
                case ExpressionValue.EOperator.Mul:
                case ExpressionValue.EOperator.Div:
                    if (left.ResultType != ExprResultType.Float || right.ResultType != ExprResultType.Float)
                        return ExprResultType.Invalid;

                    return ExprResultType.Float;

                default:
                    return ExprResultType.Invalid;
            }
        }

        private static IExprValue InvertExpression(ExpressionExprValue expr)
        {
            IExprValue left = expr.Left;
            IExprValue right = expr.Right;

            switch (expr.Operator)
            {
                case ExpressionValue.EOperator.Or:
                    {
                        IExprValue invLeft = InvertCondition(left);
                        IExprValue invRight = InvertCondition(right);

                        if (invLeft.ResultType == ExprResultType.Invalid || right.ResultType == ExprResultType.Invalid)
                            return new InvalidExprValue();

                        return new ExpressionExprValue(invLeft, invRight, ExpressionValue.EOperator.And);
                    }

                case ExpressionValue.EOperator.And:
                    {
                        IExprValue invLeft = InvertCondition(left);
                        IExprValue invRight = InvertCondition(right);

                        if (invLeft.ResultType == ExprResultType.Invalid || right.ResultType == ExprResultType.Invalid)
                            return new InvalidExprValue();

                        return new ExpressionExprValue(invLeft, invRight, ExpressionValue.EOperator.Or);
                    }

                case ExpressionValue.EOperator.Gt:
                    return new ExpressionExprValue(left, right, ExpressionValue.EOperator.Le);
                case ExpressionValue.EOperator.Lt:
                    return new ExpressionExprValue(left, right, ExpressionValue.EOperator.Ge);
                case ExpressionValue.EOperator.Ge:
                    return new ExpressionExprValue(left, right, ExpressionValue.EOperator.Lt);
                case ExpressionValue.EOperator.Le:
                    return new ExpressionExprValue(left, right, ExpressionValue.EOperator.Gt);
                case ExpressionValue.EOperator.Eq:
                    return new ExpressionExprValue(left, right, ExpressionValue.EOperator.Neq);
                case ExpressionValue.EOperator.Xor:
                case ExpressionValue.EOperator.Add:
                case ExpressionValue.EOperator.Sub:
                case ExpressionValue.EOperator.Mul:
                case ExpressionValue.EOperator.Div:
                case ExpressionValue.EOperator.Neq:
                    return new ExpressionExprValue(expr, new FloatConstExprValue(0), ExpressionValue.EOperator.Eq);

                default:
                    return new InvalidExprValue();
            }
        }

        internal static IExprValue InvertCondition(IExprValue expr)
        {
            switch (expr.ExprType)
            {
                case ExprType.FloatConst:
                    if (((FloatConstExprValue)expr).Value == 0)
                        return new FloatConstExprValue(1);
                    else
                        return new FloatConstExprValue(0);
                case ExprType.FloatVar:
                    return new ExpressionExprValue(expr, new FloatConstExprValue(0), ExpressionValue.EOperator.Neq);
                case ExprType.Expr:
                    return InvertExpression((ExpressionExprValue)expr);
                case ExprType.StringVar:
                case ExprType.StringConst:
                case ExprType.Invalid:
                default:
                    return new InvalidExprValue();
            }
        }

        internal static bool TryResolveEscapeChar(byte escapeChar, out byte result)
        {
            if (escapeChar == 't')
            {
                result = (byte)'\t';
                return true;
            }

            if (escapeChar == 'n')
            {
                result = (byte)'\n';
                return true;
            }

            if (escapeChar == '\"')
            {
                result = (byte)'\"';
                return true;
            }

            if (escapeChar == '\\')
            {
                result = (byte)'\\';
                return true;
            }

            result = 0;
            return false;
        }

        internal static ByteStringSlice EscapeSlice(ByteStringSlice slice, ILogger.LocationTag locTag, bool applyNormalEscapes, bool convertEndOfLineToSpace)
        {
            bool needsNormalEscape = false;
            bool needsEOLEscape = false;
            for (int i = 0; i < slice.Length; i++)
            {
                byte b = slice[i];
                if (b == '\\' && applyNormalEscapes)
                    needsNormalEscape = true;
                if (b == '\n' && convertEndOfLineToSpace)
                    needsEOLEscape = true;
            }

            if (!needsNormalEscape && !needsEOLEscape)
                return slice;

            List<byte> bytes = new List<byte>();

            for (int i = 0; i < slice.Length; i++)
            {
                byte b = slice[i];
                if (needsNormalEscape && b == '\\')
                {
                    i++;
                    b = slice[i];
                    if (!Utils.TryResolveEscapeChar(b, out b))
                        throw new CompilerException(locTag, "Unknown escape character");
                }
                else if (needsEOLEscape && b == '\n')
                    b = (byte)' ';

                bytes.Add(b);
            }

            return new ByteString(bytes.ToArray()).ToSlice();
        }
    }
}
