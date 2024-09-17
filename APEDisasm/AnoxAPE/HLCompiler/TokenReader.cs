using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnoxAPE.HLCompiler
{

    internal enum EOLBehavior
    {
        Stop,   // Stop at EOL
        Ignore, // Stop at first token after EOL
        Fail,   // Fail of EOL or EOF is reached
        Expect, // Expect to hit EOF or at least one EOL
    }

    internal struct TokenReadBehavior
    {
        public bool QuotedStringAllowed { get; set; }
        public bool NewLinesInStringAllowed { get; set; }
        public bool NonStringAllowed { get; set; }
        public bool PunctuationAllowed { get; set; }
    }

    internal class TokenReader
    {
        private byte[] _bytes;
        private CompilerOptions _options;
        private ByteString _lineCommentStartBStr;
        private ByteString _blockCommentStartBStr;
        private ByteString _blockCommentEndBStr;

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

                if (b == '\"')
                {
                    reader.StepAhead(1);
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
    }
}
