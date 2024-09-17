using AnoxAPE.Elements;

namespace AnoxAPE.HLCompiler
{
    internal class ExprParser
    {

        private OperatorPrecedences _precedences;
        private bool _allowExpFloats;

        public ExprParser(OperatorPrecedences precedences, bool allowExpFloats)
        {
            _precedences = precedences;
            _allowExpFloats = allowExpFloats;
        }

        private static bool IsIdentifierChar(byte c)
        {
            if (c >= '0' && c <= '9')
                return true;

            if (c >= 'a' && c <= 'z')
                return true;

            if (c >= 'A' && c <= 'Z')
                return true;

            if (c == '_' || c == '$')
                return true;

            return false;
        }

        private IExprValue ParseParenExpr(PositionTrackingReader reader, TokenReader tokenReader)
        {
            reader.StepAhead(1);

            tokenReader.SkipWhitespace(reader, EOLBehavior.Ignore);

            IExprValue expr = ParseExpr(reader, tokenReader);

            tokenReader.SkipWhitespace(reader, EOLBehavior.Ignore);

            if (reader.IsAtEndOfFile || reader.PeekOne() != ')')
                throw new CompilerException(reader.LocationTag, "Expected ')' to close expression");

            reader.StepAhead(1);

            return expr;
        }

        private IExprValue ParseStringLitExpr(PositionTrackingReader reader)
        {
            reader.StepAhead(1);

            int startPos = reader.FilePosition;
            while (!reader.IsAtEndOfFile)
            {
                byte strb = reader.PeekOne();

                if (strb == '\n')
                    throw new CompilerException(reader.LocationTag, "Newline in string constant");

                if (strb == '\\')
                {
                    reader.StepAhead(1);
                    if (reader.IsAtEndOfFile)
                        throw new CompilerException(reader.LocationTag, "Unterminated string constant");

                    byte escapeChar = reader.PeekOne();
                    byte escapeResult = 0;
                    if (!Utils.TryResolveEscapeChar(escapeChar, out escapeResult))
                        throw new CompilerException(reader.LocationTag, "Unknown escape character");
                }

                if (strb == '\"')
                {
                    int endPos = reader.FilePosition;
                    reader.StepAhead(1);

                    return new StringConstExprValue(reader.GetSlice(startPos, endPos - startPos));
                }

                reader.StepAhead(1);
            }

            throw new CompilerException(reader.LocationTag, "Unterminated string constant");
        }

        private enum FloatParseStep
        {
            Integral,
            Fractional,
            ExpSign,
            ExpFirstDigit,
            Exp
        }

        private IExprValue ParseNumberExpr(PositionTrackingReader reader)
        {
            FloatParseStep step = FloatParseStep.Integral;

            ILogger.LocationTag locTag = reader.LocationTag;

            int litStart = reader.FilePosition;

            while (!reader.IsAtEndOfFile)
            {
                byte b = reader.PeekOne();

                if (b >= '0' && b <= '9')
                {
                    if (step == FloatParseStep.ExpSign || step == FloatParseStep.ExpFirstDigit)
                        step = FloatParseStep.Exp;

                    reader.StepAhead(1);
                    continue;
                }

                if (b == '.')
                {
                    if (step != FloatParseStep.Integral)
                        throw new CompilerException(locTag, $"Float literal had multiple decimals");

                    step = FloatParseStep.Fractional;

                    reader.StepAhead(1);
                    continue;
                }
                
                if ((b == 'e' || b == 'E') && _allowExpFloats)
                {
                    step = FloatParseStep.ExpSign;

                    reader.StepAhead(1);
                    continue;
                }

                if (b == '+' || b == '-')
                {
                    if (step == FloatParseStep.ExpSign)
                    {
                        step = FloatParseStep.ExpFirstDigit;
                        reader.StepAhead(1);
                        continue;
                    }
                    else
                        break;
                }

                if (IsIdentifierChar(b))
                    throw new CompilerException(locTag, $"Unexpected character in float literal");

                break;
            }

            if (step == FloatParseStep.ExpFirstDigit || step == FloatParseStep.ExpSign)
                throw new CompilerException(locTag, "Expected exponent");

            int litEnd = reader.FilePosition;

            ByteStringSlice slice = reader.GetSlice(litStart, litEnd - litStart);
            string str = slice.ToString(System.Text.Encoding.ASCII);

            float result = 0.0f;
            if (!float.TryParse(str, System.Globalization.CultureInfo.InvariantCulture, out result))
                throw new CompilerException(locTag, $"Could not parse {str} as a float");

            return new FloatConstExprValue(result);
        }

        private IExprValue ParseIdentifier(PositionTrackingReader reader)
        {
            ILogger.LocationTag locTag = reader.LocationTag;

            int identStart = reader.FilePosition;

            while (!reader.IsAtEndOfFile)
            {
                byte b = reader.PeekOne();
                if (!IsIdentifierChar(b))
                    break;

                reader.StepAhead(1);
            }

            int identEnd = reader.FilePosition;

            ByteStringSlice slice = reader.GetSlice(identStart, identEnd - identStart);

            if (slice[slice.Length - 1] == '$')
                return new StringVarExprValue(slice);
            else
                return new FloatVarExprValue(slice);
        }

        private IExprValue ParseBottomLevelExpr(PositionTrackingReader reader, TokenReader tokenReader)
        {
            if (reader.IsAtEndOfFile)
                throw new CompilerException(reader.LocationTag, "Expected expression");

            byte firstByte = reader.PeekOne();
            if (firstByte == '(')
                return ParseParenExpr(reader, tokenReader);

            if (firstByte == '\"')
                return ParseStringLitExpr(reader);

            if (firstByte >= '0' && firstByte <= '9')
                return ParseNumberExpr(reader);

            if (IsIdentifierChar(firstByte))
                return ParseIdentifier(reader);

            throw new CompilerException(reader.LocationTag, "Expected expression");
        }

        private IExprValue ParseNegationExpr(PositionTrackingReader reader, TokenReader tokenReader)
        {
            if (reader.IsAtEndOfFile)
                throw new CompilerException(reader.LocationTag, "Expected expression");

            byte firstByte = reader.PeekOne();
            if (firstByte == '-')
            {
                reader.StepAhead(1);
                tokenReader.SkipWhitespace(reader, EOLBehavior.Ignore);

                IExprValue subExpr = ParseNegationExpr(reader, tokenReader);

                if (subExpr.ResultType != ExprResultType.Float)
                    throw new CompilerException(reader.LocationTag, "Expected float expression for negation operator");

                if (subExpr.ExprType == ExprType.FloatConst)
                    return new FloatConstExprValue(-((FloatConstExprValue)subExpr).Value);
                else
                    return new ExpressionExprValue(new FloatConstExprValue(0.0f), subExpr, Elements.ExpressionValue.EOperator.Sub);
            }

            return ParseBottomLevelExpr(reader, tokenReader);
        }

        private IExprValue RecursiveParseExpr(PositionTrackingReader reader, TokenReader tokenReader, int upperTier)
        {
            int thisTier = upperTier - 1;
            if (thisTier < 0)
                return ParseNegationExpr(reader, tokenReader);

            OperatorPrecedenceTier opTier = _precedences.Tiers[thisTier];

            IExprValue expr = RecursiveParseExpr(reader, tokenReader, thisTier);

            while (true)
            {
                PositionTrackingReader.RewindPos rewindPos = reader.GetRewindPos();

                tokenReader.SkipWhitespace(reader, EOLBehavior.Ignore);

                int matchSize = 0;
                ExpressionValue.EOperator expOp = ExpressionValue.EOperator.Invalid;

                if (!reader.IsAtEndOfFile)
                {
                    foreach (OperatorResolution op in opTier.Operators)
                    {
                        if (reader.Matches(op.OperatorStr))
                        {
                            matchSize = op.OperatorStr.Length;
                            expOp = op.Operator;
                            break;
                        }
                    }
                }

                if (matchSize == 0)
                {
                    reader.Rewind(rewindPos);
                    return expr;
                }

                reader.StepAhead(matchSize);

                tokenReader.SkipWhitespace(reader, EOLBehavior.Ignore);

                IExprValue rightSide = RecursiveParseExpr(reader, tokenReader, thisTier);

                expr = new ExpressionExprValue(expr, rightSide, expOp);
            }
        }

        // Parses from the start and an expression to the end of the expression
        public IExprValue ParseExpr(PositionTrackingReader reader, TokenReader tokenReader)
        {
            return RecursiveParseExpr(reader, tokenReader, _precedences.Tiers.Count);
        }
    }
}
