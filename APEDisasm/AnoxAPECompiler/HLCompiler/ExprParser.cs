using AnoxAPE;
using AnoxAPE.Elements;

namespace AnoxAPECompiler.HLCompiler
{
    internal class ExprParser
    {

        private OperatorPrecedences _precedences;
        private bool _allowExpFloats;
        private bool _allowEscapesInStrings;
        private bool _allowInvalidExprs;
        private ILogger? _logger;

        public ExprParser(OperatorPrecedences precedences, ILogger? logger, bool allowExpFloats, bool allowEscapesInStrings, bool allowInvalidExprs)
        {
            _precedences = precedences;
            _allowExpFloats = allowExpFloats;
            _allowEscapesInStrings = allowEscapesInStrings;
            _allowInvalidExprs = allowInvalidExprs;
            _logger = logger;
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

        private IExprValue ParseParenExpr(TokenReader2 reader)
        {
            reader.ConsumeToken();

            IExprValue expr = ParseExpr(reader);

            Token closeParen = reader.ReadToken(TokenReadMode.Normal);

            if (closeParen.TokenType != TokenType.CloseParen)
                throw new CompilerException(closeParen.Location, "Expected ')' to close expression");

            return expr;
        }

        private IExprValue ParseStringLitExpr(TokenReader2 reader, Token tok)
        {
            reader.ConsumeToken();

            ByteStringSlice str = Utils.EscapeSlice(tok.Value.SubSlice(1, tok.Value.Length - 2), tok.Location, true, false);

            return new StringConstExprValue(str);
        }

        private IExprValue ParseNumberExpr(TokenReader2 reader, Token tok)
        {
            reader.ConsumeToken();

            return new FloatConstExprValue(ResolveFloatLiteralToken(tok));
        }

        internal static float ResolveFloatLiteralToken(Token tok)
        {
            if (tok.TokenType != TokenType.NumericLiteral)
                throw new ArgumentException("Input token wasn't a numeric literal");

            string str = tok.Value.ToString(System.Text.Encoding.ASCII);

            float result = 0.0f;
            if (!float.TryParse(str, System.Globalization.CultureInfo.InvariantCulture, out result))
                throw new CompilerException(tok.Location, $"Could not parse {str} as a float");

            return result;
        }


        private IExprValue ParseIdentifier(TokenReader2 reader, Token tok)
        {
            reader.ConsumeToken();

            ByteStringSlice slice = tok.Value;

            if (slice[slice.Length - 1] == '$')
                return new StringVarExprValue(slice);
            else
                return new FloatVarExprValue(slice);
        }

        private IExprValue ParseBottomLevelExpr(TokenReader2 reader)
        {
            Token tok = reader.PeekToken(TokenReadMode.Normal);

            if (tok.TokenType == TokenType.OpenParen)
                return ParseParenExpr(reader);

            if (tok.TokenType == TokenType.StringLiteral)
                return ParseStringLitExpr(reader, tok);

            if (tok.TokenType == TokenType.NumericLiteral)
                return ParseNumberExpr(reader, tok);

            if (tok.TokenType == TokenType.Identifier)
                return ParseIdentifier(reader, tok);

            throw new CompilerException(tok.Location, "Expected expression");
        }

        private IExprValue ParseNegationExpr(TokenReader2 reader)
        {
            Token tok = reader.PeekToken(TokenReadMode.Normal);

            if (tok.TokenType == TokenType.ExprOperator && tok.Value[0] == '-')
            {
                reader.ConsumeToken();

                IExprValue subExpr = ParseNegationExpr(reader);

                if (subExpr.ResultType != ExprResultType.Float)
                    throw new CompilerException(tok.Location, "Expected float expression for negation operator");

                if (subExpr.ExprType == ExprType.FloatConst)
                    return new FloatConstExprValue(-((FloatConstExprValue)subExpr).Value);
                else
                    return new ExpressionExprValue(new FloatConstExprValue(0.0f), subExpr, ExpressionValue.EOperator.Sub);
            }

            return ParseBottomLevelExpr(reader);
        }

        private IExprValue RecursiveParseExpr(TokenReader2 reader, int upperTier)
        {
            int thisTier = upperTier - 1;
            if (thisTier < 0)
                return ParseNegationExpr(reader);

            OperatorPrecedenceTier opTier = _precedences.Tiers[thisTier];

            IExprValue expr = RecursiveParseExpr(reader, thisTier);

            while (true)
            {
                Token nextToken = reader.PeekToken(TokenReadMode.Normal);

                if (nextToken.TokenType != TokenType.ExprOperator)
                    return expr;

                bool haveMatch = false;
                ExpressionValue.EOperator expOp = ExpressionValue.EOperator.Invalid;

                foreach (OperatorResolution op in opTier.Operators)
                {
                    if (nextToken.Value.Equals(op.OperatorStr))
                    {
                        haveMatch = true;
                        expOp = op.Operator;
                        break;
                    }
                }

                if (!haveMatch)
                    return expr;

                reader.ConsumeToken();

                IExprValue rightSide = RecursiveParseExpr(reader, thisTier);

                expr = new ExpressionExprValue(expr, rightSide, expOp);
            }
        }

        // Parses from the start and an expression to the end of the expression
        public IExprValue ParseExpr(TokenReader2 reader)
        {
            return RecursiveParseExpr(reader, _precedences.Tiers.Count);
        }

        public IExprValue ParseExprPreferFloat(TokenReader2 reader)
        {
            ILogger.LocationTag locTag = reader.Location;
            IExprValue expr = RecursiveParseExpr(reader, _precedences.Tiers.Count);

            if (expr.ResultType != ExprResultType.Float)
            {
                if (!_allowInvalidExprs)
                    throw new CompilerException(locTag, "Compiler expression was invalid");
                else
                {
                    if (_logger != null)
                        _logger.WriteLine(new ILogger.MessageProperties(ILogger.Severity.Warning, locTag), "Expression does not evaluate to a number");
                }
            }

            return expr;
        }

        internal static uint ParseLabelPart(Token tok, uint limit)
        {
            uint result = 0;

            ByteStringSlice slice = tok.Value;
            for (int i = 0; i < slice.Length; i++)
            {
                byte b = slice[i];
                if (b >= '0' && b <= '9')
                    result = result * 10 + (uint)(b - '0');
                else
                    throw new CompilerException(tok.Location, "Label part was not an integral");

                if (result >= limit)
                    throw new CompilerException(tok.Location, "Label part was too large");
            }

            return result;
        }

        internal static uint ParseLabelFromTokens(Token highTok, Token lowTok)
        {
            uint labelHigh = ParseLabelPart(highTok, 100000);
            uint labelLow = ParseLabelPart(lowTok, 10000);

            return labelHigh * 10000 + labelLow;
        }

        internal uint ParseLabel(TokenReader2 reader)
        {
            Token labelHighTok = reader.ExpectToken(TokenReadMode.Normal, TokenType.NumericLiteral);
            reader.ExpectToken(TokenReadMode.Normal, TokenType.Colon);
            Token labelLowTok = reader.ExpectToken(TokenReadMode.Normal, TokenType.NumericLiteral);

            uint labelHigh = ParseLabelPart(labelHighTok, 100000);
            uint labelLow = ParseLabelPart(labelLowTok, 10000);

            return labelHigh * 10000 + labelLow;
        }

        internal TypedFormattingValue ParseFormattingValue(TokenReader2 reader)
        {
            Token tok = reader.ReadToken(TokenReadMode.Normal, TokenReadProperties.Default.Add(TokenReadProperties.Flag.IgnoreEscapes));

            if (tok.TokenType == TokenType.NumericLiteral)
                return new TypedFormattingValue(TypedFormattingValue.EFormattingValueType.Float, new FloatOperand(ResolveFloatLiteralToken(tok)));

            if (tok.TokenType == TokenType.Identifier)
            {
                ByteStringSlice slice = tok.Value;
                TypedFormattingValue.EFormattingValueType type = TypedFormattingValue.EFormattingValueType.VariableName;
                if (slice[slice.Length - 1] == '$')
                    type = TypedFormattingValue.EFormattingValueType.StringVariableName;

                return new TypedFormattingValue(type, new StringOperand(slice.ToByteString()));
            }

            if (tok.TokenType == TokenType.StringLiteral)
            {
                ByteStringSlice slice = tok.Value;
                slice = slice.SubSlice(1, slice.Length - 2);
                return new TypedFormattingValue(TypedFormattingValue.EFormattingValueType.String, new QuotedStringOperand(slice.ToByteString()));
            }

            throw new CompilerException(tok.Location, "Unexpected token type where a formatting value was expected");
        }

        internal FormattingValue ParseOptionalFormattingValueList(TokenReader2 reader)
        {
            IList<TypedFormattingValue> tfvs = new List<TypedFormattingValue>();
            Token tok = reader.PeekToken(TokenReadMode.Normal);
            while (tok.TokenType == TokenType.Comma)
            {
                reader.ConsumeToken();

                tfvs.Add(ParseFormattingValue(reader));

                tok = reader.PeekToken(TokenReadMode.Normal);
            }

            return new FormattingValue(tfvs);
        }
    }
}
