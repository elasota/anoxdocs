using AnoxAPE;

namespace AnoxAPECompiler.HLCompiler
{

    internal enum EOLBehavior
    {
        Stop,   // Stop at EOL
        Ignore, // Stop at first token after EOL
        Fail,   // Fail of EOL or EOF is reached
        Expect, // Expect to hit EOF or at least one EOL
    }

    internal struct TokenReadProperties
    {
        public enum Flag
        {
            NoMacroSubstitution,
            TerminateQuotesOnNewLine,
            IgnoreEscapes,
            AllowNewLineInString,
            StopAtCloseParen,
            IgnoreWhitespace,
            IgnoreQuotes,
        }

        public static TokenReadProperties Default
        {
            get { return new TokenReadProperties(); }
        }

        private ulong _flags;

        public TokenReadProperties()
        {
            _flags = 0;
        }

        public bool HasFlag(Flag flag)
        {
            ulong mask = ((ulong)1 << (int)flag);
            return (_flags & mask) != 0;
        }

        public TokenReadProperties Add(Flag flag)
        {
            ulong mask = ((ulong)1 << (int)flag);

            TokenReadProperties adjusted = this;
            adjusted._flags |= mask;
            return adjusted;
        }

        public TokenReadProperties Remove(Flag flag)
        {
            ulong mask = ((ulong)1 << (int)flag);

            TokenReadProperties adjusted = this;
            adjusted._flags &= ~mask;
            return adjusted;
        }
    }

    internal enum TokenReadMode
    {
        Normal,                     // Normal parse mode
        QuotedString,               // Must be a quoted string, by default escapes are processed and newlines are not allowed
        UnquotedString,             // All characters to next whitespace
    }

    internal enum TokenType
    {
        EndOfLine,
        EndOfFile,
        Identifier,
        NumericLiteral,
        Colon,
        StringLiteral,
        Comma,
        ExprOperator,
        OpenParen,
        CloseParen,
        OpenBrace,
        CloseBrace,
        WhiteSpace,
        TopLevelDirective,
        Assign,
        AbstractString,
    }

    internal struct Token
    {
        public TokenType TokenType { get; private set; }
        public ByteStringSlice Value { get; private set; }
        public ILogger.LocationTag Location { get; private set; }

        public Token(TokenType type, ByteStringSlice value, ILogger.LocationTag locationTag)
        {
            TokenType = type;
            Value = value;
            Location = locationTag;
        }
    }

    internal class TokenReader2
    {
        private enum NumberParseStep
        {
            Integral,
            FractionalFirstDigit,
            Fractional,
            ExpSign,
            ExpFirstDigit,
            Exp,
        }

        public ILogger.LocationTag Location { get { return _reader.LocationTag; } }

        private PositionTrackingReader _reader;
        private Queue<Token> _queue;
        private bool _hasReadEOF;
        private bool _allowExpFloats;

        private static ByteString _lineCommentStartBStr = ByteString.FromAsciiString("//");
        private static ByteString _blockCommentStartBStr = ByteString.FromAsciiString("/*");
        private static ByteString _blockCommentEndBStr = ByteString.FromAsciiString("*/");

        public TokenReader2(PositionTrackingReader reader, bool allowExpFloats)
        {
            _reader = reader;
            _queue = new Queue<Token>();
            _allowExpFloats = allowExpFloats;
        }

        public Token ReadToken(TokenReadMode readMode, TokenReadProperties readProps)
        {
            if (_queue.Count > 0)
                return _queue.Dequeue();

            return SkipWhitespaceAndReadTokenFromReader(readMode, readProps);
        }

        public Token ReadToken(TokenReadMode readMode)
        {
            return ReadToken(readMode, new TokenReadProperties());
        }

        private Token SkipWhitespaceAndReadTokenFromReader(TokenReadMode readMode, TokenReadProperties readProps)
        {
            if (_hasReadEOF)
                throw new CompilerException(_reader.LocationTag, "Unexpected end of file");

            // Skip whitespace
            bool isInBlockComment = false;
            bool isInLineComment = false;
            while (true)
            {
                if (_reader.IsAtEndOfFile)
                {
                    _hasReadEOF = true;
                    return new Token(TokenType.EndOfFile, new ByteStringSlice(new byte[0], 0, 0), _reader.LocationTag);
                }

                byte b = _reader.PeekOne();
                if (b == '\n')
                {
                    _reader.StepAhead(1);

                    if (isInBlockComment)
                        continue;

                    return new Token(TokenType.EndOfLine, new ByteStringSlice(new byte[0], 0, 0), _reader.LocationTag);
                }

                if (isInLineComment || Utils.IsWhitespace(b))
                {
                    _reader.StepAhead(1);
                    continue;
                }

                if (isInBlockComment && !_reader.Matches(_blockCommentEndBStr))
                {
                    _reader.StepAhead(1);
                    continue;
                }

                if (_reader.Matches(_lineCommentStartBStr))
                {
                    _reader.StepAhead(_lineCommentStartBStr.Length);
                    isInLineComment = true;
                    continue;
                }

                if (_reader.Matches(_blockCommentStartBStr))
                {
                    _reader.StepAhead(_blockCommentStartBStr.Length);
                    isInLineComment = true;
                    continue;
                }

                break;
            }

            // Reader is at the start of a token
            switch (readMode)
            {
                case TokenReadMode.UnquotedString:
                    return ReadUnquotedString(readProps);
                case TokenReadMode.QuotedString:
                    return ReadQuotedString(readProps);
                case TokenReadMode.Normal:
                    return ReadTokenFromReader(readProps);
                default:
                    throw new Exception("Internal error: Unhandled token read mode");
            }
        }

        private Token ReadUnquotedString(TokenReadProperties readProps)
        {
            ILogger.LocationTag locTag = _reader.LocationTag;
            int strStart = _reader.FilePosition;

            _reader.StepAhead(1);

            while (!_reader.IsAtEndOfFile)
            {
                byte b = _reader.PeekOne();

                if (b == '\n' && readProps.HasFlag(TokenReadProperties.Flag.TerminateQuotesOnNewLine))
                    break;

                if (b == ')' && readProps.HasFlag(TokenReadProperties.Flag.StopAtCloseParen))
                    break;

                if (b == '\\' && !readProps.HasFlag(TokenReadProperties.Flag.IgnoreEscapes))
                {
                    _reader.StepAhead(1);
                    if (_reader.IsAtEndOfFile)
                        throw new CompilerException(locTag, "Unterminated string escape");

                    _reader.StepAhead(1);
                    continue;
                }

                if (b == '\"' && !readProps.HasFlag(TokenReadProperties.Flag.StopAtCloseParen) && !readProps.HasFlag(TokenReadProperties.Flag.IgnoreQuotes))
                {
                    _reader.StepAhead(1);
                    break;
                }

                if (Utils.IsWhitespace(b) && !readProps.HasFlag(TokenReadProperties.Flag.IgnoreWhitespace))
                    break;

                _reader.StepAhead(1);
            }

            int strEnd = _reader.FilePosition;

            ByteStringSlice slice = _reader.GetSlice(strStart, strEnd - strStart);

            return new Token(TokenType.AbstractString, slice, locTag);
        }

        private Token ReadTokenFromReader(TokenReadProperties readProps)
        {
            ILogger.LocationTag startLoc = _reader.LocationTag;
            byte b = _reader.PeekOne();

            if (IsNumeral(b))
                return ReadNumberToken(readProps);

            if (IsIdentifierChar(b))
                return ReadIdentifier(readProps);

            if (b == '\"')
                return ReadQuotedString(readProps);

            if (b == ',')
                return ReadSimpleToken(1, TokenType.Comma);

            if (b == '/' || b == '*' || b == '-' || b == '+')
                return ReadSimpleToken(1, TokenType.ExprOperator);

            if (b == ':')
                return ReadSimpleToken(1, TokenType.Colon);

            if (b == '#')
                return ReadSimpleToken(1, TokenType.TopLevelDirective);

            if (b == '{')
                return ReadSimpleToken(1, TokenType.OpenBrace);

            if (b == '}')
                return ReadSimpleToken(1, TokenType.CloseBrace);

            if (b == '(')
                return ReadSimpleToken(1, TokenType.OpenParen);

            if (b == ')')
                return ReadSimpleToken(1, TokenType.CloseParen);

            if (b == '<' || b == '>')
            {
                int tokenStart = _reader.FilePosition;
                _reader.StepAhead(1);

                int tokenEnd = tokenStart + 1;
                if (!_reader.IsAtEndOfFile)
                {
                    byte secondByte = _reader.PeekOne();
                    if (secondByte == '=')
                    {
                        _reader.StepAhead(1);
                        tokenEnd++;
                    }
                }

                return new Token(TokenType.ExprOperator, _reader.GetSlice(tokenStart, tokenEnd - tokenStart), startLoc);
            }

            if (b == '=')
            {
                TokenType tokenType = TokenType.Assign;

                int tokenStart = _reader.FilePosition;
                _reader.StepAhead(1);

                int tokenEnd = tokenStart + 1;
                if (!_reader.IsAtEndOfFile)
                {
                    byte secondByte = _reader.PeekOne();
                    if (secondByte == '=')
                    {
                        tokenType = TokenType.ExprOperator;
                        _reader.StepAhead(1);
                        tokenEnd++;
                    }
                }

                return new Token(tokenType, _reader.GetSlice(tokenStart, tokenEnd - tokenStart), startLoc);
            }

            if (b == '!' || b == '&' || b == '^' || b == '|')
            {
                int tokenStart = _reader.FilePosition;
                _reader.StepAhead(1);

                if (_reader.IsAtEndOfFile)
                    throw new CompilerException(startLoc, "Unexpected end of file in token");

                byte secondByte = _reader.PeekOne();
                _reader.StepAhead(1);

                bool isAllowedToken = false;
                if (b == '!' && secondByte == '=')
                    isAllowedToken = true;
                else if ((b == '&' || b == '^' || b == '|') && (b == secondByte))
                    isAllowedToken = true;

                if (isAllowedToken)
                    return new Token(TokenType.ExprOperator, _reader.GetSlice(tokenStart, 2), startLoc);
            }

            throw new CompilerException(startLoc, "Unrecognized token");
        }

        private Token ReadIdentifier(TokenReadProperties readProps)
        {
            ILogger.LocationTag startLoc = _reader.LocationTag;
            int startPos = _reader.FilePosition;

            while (!_reader.IsAtEndOfFile)
            {
                byte b = _reader.PeekOne();

                if (!IsIdentifierChar(b))
                    break;

                _reader.StepAhead(1);
            }    

            int endPos = _reader.FilePosition;
            return new Token(TokenType.Identifier, _reader.GetSlice(startPos, endPos - startPos), startLoc);
        }

        private Token ReadNumberToken(TokenReadProperties readProps)
        {
            NumberParseStep step = NumberParseStep.Integral;
            ILogger.LocationTag locationTag = _reader.LocationTag;
            int startLoc = _reader.FilePosition;

            while (!_reader.IsAtEndOfFile)
            {
                byte b = _reader.PeekOne();

                if (IsNumeral(b))
                {
                    _reader.StepAhead(1);

                    if (step == NumberParseStep.FractionalFirstDigit)
                        step = NumberParseStep.Fractional;
                    else if (step == NumberParseStep.ExpFirstDigit || step == NumberParseStep.ExpSign)
                        step = NumberParseStep.Exp;

                    continue;
                }

                if (b == 'e' || b == 'E')
                {
                    if (_allowExpFloats && (step == NumberParseStep.Integral || step == NumberParseStep.Fractional))
                    {
                        step = NumberParseStep.ExpSign;
                        _reader.StepAhead(1);
                        continue;
                    }
                }

                if (IsIdentifierChar(b))
                    throw new CompilerException(locationTag, "Invalid character in float literal");

                if (b == '.')
                {
                    if (step == NumberParseStep.Integral)
                    {
                        step = NumberParseStep.FractionalFirstDigit;
                        _reader.StepAhead(1);
                        continue;
                    }
                    else
                        throw new CompilerException(locationTag, "Multiple decimal points in float literal");
                }

                if (b == '+' || b == '-')
                {
                    if (step == NumberParseStep.ExpSign)
                    {
                        step = NumberParseStep.ExpFirstDigit;
                        _reader.StepAhead(1);
                        continue;
                    }
                    else
                        break;
                }

                break;
            }

            if (step == NumberParseStep.FractionalFirstDigit || step == NumberParseStep.ExpFirstDigit || step == NumberParseStep.ExpSign)
                throw new CompilerException(locationTag, "Unexpected character in float literal");

            return new Token(TokenType.NumericLiteral, _reader.GetSlice(startLoc, _reader.FilePosition - startLoc), locationTag);
        }

        private static bool IsNumeral(byte b)
        {
            return b >= '0' && b <= '9';
        }

        private bool IsIdentifierChar(byte b)
        {
            return IsNumeral(b) || b == '_' || b == '$' || b == '@' || (b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z');
        }

        private Token ReadSimpleToken(int length, TokenType tokenType)
        {
            ILogger.LocationTag locTag = _reader.LocationTag;
            int tokenStart = _reader.FilePosition;
            _reader.StepAhead(length);

            return new Token(tokenType, _reader.GetSlice(tokenStart, length), locTag);
        }

        private Token ReadQuotedString(TokenReadProperties readProps)
        {
            ILogger.LocationTag locTag = _reader.LocationTag;
            int readStart = _reader.FilePosition;

            if (_reader.IsAtEndOfFile || _reader.PeekOne() != '\"')
                throw new CompilerException(_reader.LocationTag, "Expected quoted string");

            _reader.StepAhead(1);

            while (true)
            {
                if (_reader.IsAtEndOfFile)
                    throw new CompilerException(_reader.LocationTag, "Unexpected end of file in string constant");

                byte b = _reader.PeekOne();
                if (b == '\n')
                {
                    if (readProps.HasFlag(TokenReadProperties.Flag.TerminateQuotesOnNewLine))
                        break;

                    if (!readProps.HasFlag(TokenReadProperties.Flag.AllowNewLineInString))
                        throw new CompilerException(_reader.LocationTag, "Unexpected newline in string constant");
                }

                if (b == '\"')
                {
                    _reader.StepAhead(1);
                    break;
                }

                if (b == '\\' && readProps.HasFlag(TokenReadProperties.Flag.IgnoreEscapes))
                {
                    _reader.StepAhead(1);
                    if (_reader.IsAtEndOfFile)
                        throw new CompilerException(_reader.LocationTag, "Unexpected end of file in string constant");
                }

                _reader.StepAhead(1);
            }

            ByteStringSlice slice = _reader.GetSlice(readStart, _reader.FilePosition - readStart);

            return new Token(TokenType.StringLiteral, slice, locTag);
        }

        public Token PeekToken(TokenReadMode readMode, TokenReadProperties readProps)
        {
            if (_queue.Count > 0)
                return _queue.Peek();

            Token parsedToken = SkipWhitespaceAndReadTokenFromReader(readMode, readProps);

            _queue.Enqueue(parsedToken);

            return parsedToken;
        }

        public Token PeekToken(TokenReadMode readMode)
        {
            return PeekToken(readMode, new TokenReadProperties());
        }

        public void ConsumeToken()
        {
            if (_queue.Count == 0)
                throw new ArgumentException("Can't consume a token when none are queued");

            _queue.Dequeue();
        }

        internal void ParseInlineMacro()
        {
            throw new NotImplementedException();
        }

        internal void SkipEndOfLines(TokenReadMode readMode)
        {
            Token tok = PeekToken(readMode);
            while (tok.TokenType == TokenType.EndOfLine)
            {
                ConsumeToken();
                tok = PeekToken(readMode);
            }
        }

        internal Token ExpectToken(TokenReadMode readMode, TokenType expectedType)
        {
            Token tok = ReadToken(readMode);
            if (tok.TokenType != expectedType)
                throw new CompilerException(tok.Location, $"Expected token of type {expectedType} but found {tok.TokenType}");

            return tok;
        }
    }

    // DELETE ME
    internal struct TokenReadBehavior
    {
        public bool EscapesInStringAllowed { get; set; }
        public bool QuotedStringAllowed { get; set; }
        public bool NewLinesInStringAllowed { get; set; }
        public bool NonStringAllowed { get; set; }
        public bool PunctuationAllowed { get; set; }
        public bool ExpressionsAllowed { get; set; }
        public bool NumericLiteralsAllowed { get; set; }
        public bool LabelsAllowed { get; set; }
        public bool FloatsPermitted { get; set; }
    }

    // DELETE ME
    internal class TokenReader
    {
        private byte[] _bytes;
        private CompilerOptions _options;
        private ByteString _lineCommentStartBStr;
        private ByteString _blockCommentStartBStr;
        private ByteString _blockCommentEndBStr;

        private enum NumberParseStep
        {
            Integral,
            Fractional,
            ExpSign,
            ExpFirstDigit,
            Exp,
        }

        public TokenReader(byte[] bytes, CompilerOptions options)
        {
            _bytes = bytes;
            _options = options;
            _lineCommentStartBStr = ByteString.FromAsciiString("//");
            _blockCommentStartBStr = ByteString.FromAsciiString("/*");
            _blockCommentEndBStr = ByteString.FromAsciiString("*/");
        }

        public ByteStringSlice ReadQuotedStringToken(PositionTrackingReader reader, TokenReadBehavior behavior)
        {
            ILogger.LocationTag locTag = reader.LocationTag;

            if (!behavior.QuotedStringAllowed)
                throw new CompilerException(locTag, "Unexpected quoted string");

            int quotedStringStart = reader.FilePosition;
            reader.StepAhead(1);

            while (!reader.IsAtEndOfFile)
            {
                byte b = reader.PeekOne();
                reader.StepAhead(1);

                if (b == '\"')
                {
                    int quotedStringEnd = reader.FilePosition;
                    return ApplyMacros(new ByteStringSlice(_bytes, quotedStringStart, quotedStringEnd - quotedStringStart));
                }
            }

            throw new CompilerException(locTag, "Unexpected EOF in quoted string");
        }

        public ByteStringSlice ReadToken(PositionTrackingReader reader, TokenReadBehavior behavior)
        {
            if (reader.IsAtEndOfFile)
                return new ByteStringSlice(_bytes, reader.FilePosition, 0);

            byte firstByte = reader.PeekOne();
            if (!behavior.PunctuationAllowed && firstByte == '\"')
                return ReadQuotedStringToken(reader, behavior);

            if (!behavior.NonStringAllowed)
                throw new CompilerException(reader.LocationTag, "Expected quoted string");

            int startFilePosition = reader.FilePosition;
            int endFilePosition = startFilePosition;

            while (!reader.IsAtEndOfFile)
            {
                endFilePosition = reader.FilePosition;

                byte nextByte = reader.PeekOne();
                if (Utils.IsWhitespace(nextByte))
                    break;

                if (nextByte == '/')
                {
                    if (reader.Matches(_lineCommentStartBStr) || reader.Matches(_blockCommentEndBStr))
                        break;
                }

                reader.StepAhead(1);
            }

            return ApplyMacros(new ByteStringSlice(_bytes, startFilePosition, endFilePosition - startFilePosition));
        }

        public void SkipWhitespace(PositionTrackingReader reader, EOLBehavior eolBehavior)
        {
            ILogger.LocationTag startLoc = reader.LocationTag;

            bool skipComments = (!_options.DParseCommentHandling);

            bool hitAnyEOL = false;
            while (!reader.IsAtEndOfFile)
            {
                if (skipComments)
                {
                    if (reader.Matches(_lineCommentStartBStr))
                    {
                        reader.StepAhead(_lineCommentStartBStr.Length);

                        while (!reader.IsAtEndOfFile)
                        {
                            byte b = reader.PeekOne();
                            if (b == '\n')
                                break;
                        }
                        continue;
                    }

                    if (reader.Matches(_blockCommentStartBStr))
                    {
                        ILogger.LocationTag blockCommentStartLoc = reader.LocationTag;
                        reader.StepAhead(_blockCommentStartBStr.Length);

                        bool blockCommentClosed = false;
                        while (!reader.IsAtEndOfFile)
                        {
                            if (reader.Matches(_blockCommentEndBStr))
                            {
                                reader.StepAhead(_blockCommentEndBStr.Length);
                                blockCommentClosed = true;
                                break;
                            }
                            reader.StepAhead(1);
                        }

                        if (!blockCommentClosed)
                            throw new CompilerException(blockCommentStartLoc, "Block comment wasn't terminated");

                        continue;
                    }
                }

                byte nextByte = reader.PeekOne();
                if (nextByte == '\n')
                {
                    if (eolBehavior == EOLBehavior.Stop)
                        return;

                    hitAnyEOL = true;

                    if (eolBehavior == EOLBehavior.Fail)
                        throw new CompilerException(startLoc, "Unexpected end of line");

                    reader.StepAhead(1);
                }
                else if (!Utils.IsWhitespace(nextByte))
                {
                    if (eolBehavior == EOLBehavior.Expect && !hitAnyEOL)
                        throw new CompilerException(startLoc, "Expected end of line");

                    return;
                }
                else
                    reader.StepAhead(1);
            }

            if (eolBehavior == EOLBehavior.Fail)
                throw new CompilerException(startLoc, "Unexpected end of file");
        }
        private ByteStringSlice ApplyMacros(ByteStringSlice slice)
        {
            if (_options.DParseMacroHandling)
                return slice;

            throw new NotImplementedException();
        }

        // Parses a simple quoted string
        public ByteStringSlice SimpleParseQuotedString(PositionTrackingReader reader, bool escapeChars, bool permitNewlines)
        {
            ILogger.LocationTag startLoc = reader.LocationTag;

            if (reader.IsAtEndOfFile || reader.PeekOne() != '\"')
                throw new CompilerException(startLoc, "Expected quoted string");

            reader.StepAhead(1);

            int stringStartPos = reader.FilePosition;

            bool needsNewLineEscape = false;
            bool needsNormalEscape = false;
            while (true)
            {
                if (reader.IsAtEndOfFile)
                    throw new CompilerException(startLoc, "Unexpected EOF in string constant");

                byte b = reader.PeekOne();
                if (b == '\n')
                {
                    if (permitNewlines)
                        needsNewLineEscape = true;
                    else
                        throw new CompilerException(startLoc, "Unexpected end of line in string constant");
                }
                else if (b == '\\')
                {
                    needsNormalEscape = true;
                    reader.StepAhead(1);

                    if (reader.IsAtEndOfFile)
                        throw new CompilerException(startLoc, "Unexpected EOF in string constant");
                }
                else if (b == '\"')
                    break;

                reader.StepAhead(1);
            }

            int stringEndPos = reader.FilePosition;
            reader.StepAhead(1);

            ByteStringSlice slice = reader.GetSlice(stringStartPos, stringEndPos - stringStartPos);
            if (needsNewLineEscape || needsNormalEscape)
                return EscapeSlice(slice, needsNormalEscape, needsNewLineEscape, startLoc);

            return slice;
        }

        public static ByteStringSlice EscapeSlice(ByteStringSlice slice, bool needsNormalEscape, bool needsNewLineEscape, ILogger.LocationTag locTag)
        {
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
                else if (needsNewLineEscape && b == '\n')
                    b = (byte)' ';

                bytes.Add(b);
            }

            return new ByteString(bytes.ToArray()).ToSlice();
        }
    }
}
