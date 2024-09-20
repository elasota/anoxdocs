// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
using AnoxAPE;
using AnoxAPE.Elements;
using AnoxAPECompiler.HLCompiler;

namespace AnoxAPECompiler
{
    public class Compiler
    {
        private struct MacroDef
        {
            public ByteString Name { get; private set; }
            public ByteString Value { get; private set; }

            public MacroDef(ByteString name, ByteString value)
            {
                Name = name;
                Value = value;
            }
        }

        private CompilerOptions _options;
        private List<Window> _windows;
        private List<Switch> _explicitSwitches;
        private List<Switch> _inlineSwitches;
        private bool _isCompiled;
        private byte[] _inputFileBytes;
        private Stream _loaderStream;
        private ByteString _defineBStr;
        private ByteString _windowBStr;
        private ByteString _switchBStr;
        private ExprParser _exprParser;
        private ExprConverter _exprConverter;
        private IInlineSwitchIDGenerator _inlineSwitchIDGenerator;

        public Compiler(CompilerOptions options, Stream inStream)
        {
            _options = options;
            _windows = new List<Window>();
            _explicitSwitches = new List<Switch>();
            _inlineSwitches = new List<Switch>();
            _loaderStream = inStream;
            _isCompiled = false;
            _inputFileBytes = new byte[0];
            _defineBStr = ByteString.FromAsciiString("define");
            _windowBStr = ByteString.FromAsciiString("window");
            _switchBStr = ByteString.FromAsciiString("switch");

            OperatorPrecedences precedences = (options.DParseOperatorPrecedences) ? OperatorPrecedences.DParseCompatible : OperatorPrecedences.Cpp;

            _exprParser = new ExprParser(precedences, options.Logger, options.AllowExpFloatSyntax, options.AllowMalformedExprs, options.Optimize);
            _exprConverter = new ExprConverter(options.AllowMalformedExprs, options.Optimize, options.Logger);

            uint inlineSwitchHash;
            if (options.UseExplicitInlineSwitchHash)
                inlineSwitchHash = options.ExplicitInlineSwitchHash;
            else
                inlineSwitchHash = ComputeInlineSwitchHash(options.InputFileName);

            _inlineSwitchIDGenerator = new InlineSwitchIDGenerator(inlineSwitchHash);
        }

        private static uint ComputeInlineSwitchHash(string inputFileName)
        {
            string noExtFileName = Path.GetFileNameWithoutExtension(inputFileName);

            uint hash = 0;
            foreach (char c in noExtFileName)
            {
                if (c < 0 || c >= 128)
                    throw new ArgumentException("File name for computing inline switch hash contained non-ASCII characters");

                int ci = c;
                if (ci >= 'A' && ci <= 'Z')
                    ci += ('a' - 'A');

                hash = (hash & 0x7ffffffu) * 31 + (uint)ci;
            }

            return hash % 100000u;
        }

        private APEFile InternalCompile()
        {
            _isCompiled = true;

            LoadInput();

            ConvertNewlines();

            if (_options.DParseCommentHandling)
                DParseCompatibleStripComments();

            if (_options.DParseMacroHandling)
                DParseCompatibleApplyMacros();

            PositionTrackingReader ptr = new PositionTrackingReader(_inputFileBytes, _options.InputFileName);
            TokenReader2 tokenReader = new TokenReader2(ptr, _options.AllowExpFloatSyntax, new MacroHandler());

            CompileInput(tokenReader);

            return ConstructAPEFile();
        }

        private void LoadInput()
        {
            List<byte> bytes = new List<byte>();
            byte[] buffer = new byte[1024];

            while (true)
            {
                int amountRead = _loaderStream.Read(buffer, 0, buffer.Length);

                if (amountRead > 0)
                    bytes.AddRange(new ReadOnlySpan<byte>(buffer, 0, amountRead));

                if (amountRead < buffer.Length)
                    break;
            }

            _inputFileBytes = bytes.ToArray();
        }

        private void ConvertNewlines()
        {
            List<byte> newBytes = new List<byte>();

            byte[] inBytes = _inputFileBytes;
            for (int i = 0; i < inBytes.Length; i++)
            {
                byte b = inBytes[i];
                if (b == '\r')
                {
                    newBytes.Add((byte)'\n');
                    if (i + 1 < inBytes.Length && inBytes[i + 1] == '\n')
                        i++;
                }
                else
                    newBytes.Add(b);
            }

            _inputFileBytes = newBytes.ToArray();
        }

        private static bool SimpleSkipWhitespace(ref ILogger.MutableLocationTag locTag, byte[] fBytes, ref int index, bool canSkipNewlines)
        {
            int i = index;
            int line = locTag.FileLine;
            int col = locTag.FileCol;

            while (i < fBytes.Length)
            {
                byte b = fBytes[i];
                if (b == '\n')
                {
                    if (!canSkipNewlines)
                        return false;

                    line++;
                    col = 0;
                }

                if (b > ' ')
                    break;

                col++;
            }

            locTag.FileLine = line;
            locTag.FileCol = col;
            index = i;

            return true;
        }

        private void DParseCompatibleApplyMacros()
        {
            List<MacroDef> macroDefs = new List<MacroDef>();

            ILogger.MutableLocationTag locTag = new ILogger.MutableLocationTag(_options.InputFileName, 0, 0);

            byte[] fBytes = _inputFileBytes;

            for (int i = 0; i < fBytes.Length; i++)
            {
                int availableBytes = fBytes.Length - i;

                if (availableBytes > _defineBStr.Length)
                {
                    if (_defineBStr.Equals(new ByteStringSlice(fBytes, 0, _defineBStr.Length)))
                    {
                        ILogger.LocationTag defineStartLocTag = new ILogger.LocationTag(locTag);
                        int defineStartPos = i;

                        i += _defineBStr.Length;
                        locTag.FileCol += _defineBStr.Length;

                        if (!SimpleSkipWhitespace(ref locTag, fBytes, ref i, false))
                            throw new CompilerException(defineStartLocTag, "EOL encountered instead of #define macro name");

                        if (i == fBytes.Length)
                            throw new CompilerException(defineStartLocTag, "EOF encountered instead of #define macro name");

                        int macroNameStartPos = i;
                        while (i < fBytes.Length)
                        {
                            byte b = fBytes[i];
                            if (b == '\n')
                                throw new CompilerException(defineStartLocTag, "EOL encountered instead of #define macro name");

                            if (Utils.IsWhitespace(b))
                                break;
                        }

                        int macroNameEndPos = i;
                        if (!SimpleSkipWhitespace(ref locTag, fBytes, ref i, false))
                            throw new CompilerException(defineStartLocTag, "EOL encountered instead of #define macro name");

                        if (i == fBytes.Length)
                            throw new CompilerException(defineStartLocTag, "EOF encountered instead of #define macro contents");

                        if (fBytes[i] != '\"')
                            throw new CompilerException(defineStartLocTag, "Expected macro value to be quoted");

                        locTag.FileCol++;
                        i++;

                        int macroContentsStartPos = i;
                        while (i < fBytes.Length)
                        {
                            byte b = fBytes[i];
                            if (b == '\"')
                                break;

                            if (b == '\n')
                                throw new CompilerException(defineStartLocTag, "EOL encountered in #define macro contents");

                            locTag.FileCol++;
                            i++;
                        }

                        int macroContentsEndPos = i;

                        ByteString macroName = (new ByteStringSlice(fBytes, macroNameStartPos, macroNameEndPos - macroNameStartPos)).ToByteString();
                        ByteString macroValue = (new ByteStringSlice(fBytes, macroContentsStartPos, macroContentsEndPos - macroContentsStartPos)).ToByteString();

                        macroDefs.Add(new MacroDef(macroName, macroValue));


                        // NOTE: This is intentionally off by one and fails to skip the end quote
                        // dparse accepts:
                        // title #define MACRO "Value"hello"
                        // ... and just requires that the rest of it is capable of cleaning up orphaned quotation marks
                        // on the define line (if it doesn't crash)

                        for (int j = defineStartPos; j < i; j++)
                            fBytes[j] = (byte)' ';
                    }
                }

                if (fBytes[i] == '\n')
                {
                    locTag.FileCol = 0;
                    locTag.FileLine++;
                }
                else
                    locTag.FileCol++;
            }

            // Apply macros
            List<byte> newBytes = new List<byte>();

            for (int i = 0; i < fBytes.Length;)
            {
                bool appliedMacro = false;
                int bytesAvailable = fBytes.Length - i;

                if (fBytes[i] > ' ')
                {
                    foreach (MacroDef macro in macroDefs)
                    {
                        if (bytesAvailable >= macro.Name.Length)
                        {
                            if ((new ByteStringSlice(fBytes, i, macro.Name.Length)).Equals(macro.Name))
                            {
                                newBytes.AddRange(macro.Value);
                                appliedMacro = true;
                                i += macro.Name.Length;
                                break;
                            }
                        }
                    }
                }

                if (!appliedMacro)
                {
                    newBytes.Add(fBytes[i]);
                    i++;
                }
            }

            _inputFileBytes = newBytes.ToArray();
        }

        private void DParseCompatibleStripComments()
        {
            // DParse-compatible comment stripping will strip comments even inside of strings
            ILogger.MutableLocationTag locTag = new ILogger.MutableLocationTag(_options.InputFileName, 0, 0);

            bool isInLineComment = false;
            bool isInBlockComment = false;

            ILogger.MutableLocationTag blockCommentStartLocTag = locTag;

            byte[] inBytes = _inputFileBytes;
            for (int i = 0; i < inBytes.Length; )
            {
                byte b = inBytes[i];
                if (inBytes.Length - i >= 2)
                {
                    byte b2 = inBytes[i + 1];
                    if (isInBlockComment)
                    {
                        if (b == '*' && b2 == '/')
                        {
                            inBytes[i] = (byte)' ';
                            inBytes[i + 1] = (byte)' ';
                            i += 2;
                            locTag.FileCol += 2;
                            isInBlockComment = false;
                            continue;
                        }    
                    }
                    else
                    {
                        if (!isInLineComment)
                        {
                            if (b == '/')
                            {
                                if (b2 == '*')
                                {
                                    isInBlockComment = true;
                                    blockCommentStartLocTag = locTag;
                                }
                                else if (b2 == '/')
                                    isInLineComment = true;
                            }
                        }
                    }
                }

                if (b == '\n')
                {
                    locTag.FileCol = 0;
                    locTag.FileLine++;
                    isInLineComment = false;
                }
                else
                {
                    if (isInLineComment || isInBlockComment)
                        inBytes[i] = (byte)' ';

                    locTag.FileCol++;
                }

                i++;
            }

            if (isInBlockComment)
                throw new CompilerException(blockCommentStartLocTag, "Unterminated block comment");
        }

        private void CompileWindowDirective(TokenReader2 tokenReader, out bool isImmediatelyAfterTLD)
        {
            WindowCompiler windowCompiler = new WindowCompiler(tokenReader, _exprParser, _inlineSwitches, _exprConverter, _inlineSwitchIDGenerator, _options);

            Window window = windowCompiler.Compile(out isImmediatelyAfterTLD);
            _windows.Add(window);
        }

        private void CompileSwitchDirective(TokenReader2 tokenReader, out bool isImmediatelyAfterTLD)
        {
            uint label = _exprParser.ParseLabel(tokenReader);

            SwitchCompiler swCompiler = new SwitchCompiler(tokenReader, _exprParser, _exprConverter, _options);

            _explicitSwitches.Add(swCompiler.Compile(label, false, out isImmediatelyAfterTLD));
        }

        // This function must be called AFTER the top-level directive
        private void CompileDirective(TokenReader2 reader, Token tok, out bool isImmediatelyAfterTLD)
        {
            if (tok.Value.Equals(_windowBStr))
                CompileWindowDirective(reader, out isImmediatelyAfterTLD);
            else if (tok.Value.Equals(_switchBStr))
                CompileSwitchDirective(reader, out isImmediatelyAfterTLD);
            else if (tok.Value.Equals(_defineBStr))
            {
                reader.ParseInlineMacro();
                isImmediatelyAfterTLD = false;
            }
            else
                throw new CompilerException(tok.Location, "Unknown compile directive");
        }

        private void CompileInput(TokenReader2 reader)
        {
            if (_options.DParseTopLevelDirectiveHandling)
            {
                // Ignore garbage before first TLD
                while (true)
                {
                    Token tok = reader.PeekToken(TokenReadMode.Normal, (new TokenReadProperties()).Add(TokenReadProperties.Flag.TerminateQuotesOnNewLine));

                    if (tok.TokenType == TokenType.TopLevelDirective || tok.TokenType == TokenType.EndOfFile)
                        break;

                    reader.ConsumeToken();
                }
            }

            bool isImmediatelyAfterTLD = false;
            while (true)
            {
                if (!isImmediatelyAfterTLD)
                {
                    Token tok = reader.ReadToken(TokenReadMode.Normal);

                    if (tok.TokenType == TokenType.EndOfLine)
                        continue;

                    if (tok.TokenType == TokenType.EndOfFile)
                        break;

                    if (tok.TokenType != TokenType.TopLevelDirective)
                        throw new CompilerException(tok.Location, "Expected a top-level directive");
                }

                Token tldTypeTok = reader.ReadToken(TokenReadMode.Normal);

                if (tldTypeTok.TokenType != TokenType.Identifier)
                    throw new CompilerException(tldTypeTok.Location, "Expected identifier for # directive");

                CompileDirective(reader, tldTypeTok, out isImmediatelyAfterTLD);
            }
        }

        private APEFile ConstructAPEFile()
        {
            List<Switch> allSwitches = new List<Switch>();
            allSwitches.AddRange(_explicitSwitches);
            allSwitches.AddRange(_inlineSwitches);

            Switches switches = new Switches(allSwitches);

            RootElementList rootElementList = new RootElementList(_windows, switches);

            return new APEFile(rootElementList);
        }

        public APEFile? Compile()
        {
            if (_isCompiled)
                throw new ArgumentException("Already compiled");

            try
            {
                return InternalCompile();
            }
            catch (CompilerException ex)
            {
                if (_options.Logger != null)
                {
                    ILogger.MessageProperties props = new ILogger.MessageProperties(ILogger.Severity.Error, ex.LocationTag);
                    _options.Logger.WriteLine(props, ex.CompilerExceptionMessage);
                }

                return null;
            }
            catch (Exception ex)
            {
                if (_options.Logger != null)
                {
                    Type exType = ex.GetType();
                    ILogger.MessageProperties props = new ILogger.MessageProperties(ILogger.Severity.Error, new ILogger.LocationTag());
                    _options.Logger.WriteLine(props, $"Compile threw an exception of type {exType.Namespace}.{exType.Name}");
                }

                return null;
            }
        }
    }
}
