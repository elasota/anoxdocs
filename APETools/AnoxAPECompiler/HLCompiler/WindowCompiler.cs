// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
using AnoxAPE;
using AnoxAPE.Elements;

namespace AnoxAPECompiler.HLCompiler
{
    internal class WindowCompiler
    {
        private struct WindowFlagsResolution
        {
            public ByteString Str { get; private set; }
            public FlagsCommand.FlagBit Flag { get; private set; }

            public WindowFlagsResolution(string str, FlagsCommand.FlagBit flag)
            {
                Str = ByteString.FromAsciiString(str);
                Flag = flag;
            }
        }

        private struct ImageFlagsResolution
        {
            public ByteString Str { get; private set; }
            public ImageCommand.EImageFlagBit Flag { get; private set; }

            public ImageFlagsResolution(string str, ImageCommand.EImageFlagBit flag)
            {
                Str = ByteString.FromAsciiString(str);
                Flag = flag;
            }
        }

        private TokenReader2 _reader;
        private IList<Switch> _inlineSwitches;
        private bool _hasBackground;
        private ExprParser _exprParser;
        private ExprConverter _exprConverter;

        private List<ConditionalFormattedStringCommand> _titleCommands;
        private List<ConditionalFormattedStringCommand> _bodyCommands;
        private List<XYPrintFXCommand> _xyPrintFXCommands;
        private CamCommand? _camCommand;
        private TalkCommand? _talkCommand;
        private SimpleStringCommand? _startConsoleCommand;
        private SimpleStringCommand? _finishConsoleCommand;
        private WindowSwitchCommand? _startSwitchCommand;
        private WindowSwitchCommand? _thinkSwitchCommand;
        private WindowSwitchCommand? _finishSwitchCommand;
        private SimpleStringCommand? _styleCommand;
        private SimpleStringCommand? _fontCommand;
        private FlagsCommand _flagsCommand;
        private uint _backgroundColor1;
        private uint _backgroundColor2;
        private uint _backgroundColor3;
        private uint _backgroundColor4;
        private OptionalExpression _xpos;
        private OptionalExpression _ypos;
        private OptionalExpression _width;
        private OptionalExpression _height;
        private List<SubWindowCommand> _subWindowCommands;
        private List<ImageCommand> _imageCommands;
        private List<ChoiceCommand> _choiceCommands;
        private SimpleStringCommand? _nextWindowCommand;

        private List<WindowControlFlowBlock> _controlFlowBlocks;

        private static ByteStringSlice _defineStr = ByteString.FromAsciiString("define").ToSlice();
        private static ByteStringSlice _ifStr = ByteString.FromAsciiString("if").ToSlice();
        private static ByteStringSlice _elseStr = ByteString.FromAsciiString("else").ToSlice();
        private static ByteStringSlice _titleStr = ByteString.FromAsciiString("title").ToSlice();
        private static ByteStringSlice _talkStr = ByteString.FromAsciiString("talk").ToSlice();
        private static ByteStringSlice _talkExStr = ByteString.FromAsciiString("talk_ex").ToSlice();
        private static ByteStringSlice _widthStr = ByteString.FromAsciiString("width").ToSlice();
        private static ByteStringSlice _heightStr = ByteString.FromAsciiString("height").ToSlice();
        private static ByteStringSlice _xposStr = ByteString.FromAsciiString("xpos").ToSlice();
        private static ByteStringSlice _yposStr = ByteString.FromAsciiString("ypos").ToSlice();
        private static ByteStringSlice _imageStr = ByteString.FromAsciiString("image").ToSlice();
        private static ByteStringSlice _flagsStr = ByteString.FromAsciiString("flags").ToSlice();
        private static ByteStringSlice _subWindowStr = ByteString.FromAsciiString("subwindow").ToSlice();
        private static ByteStringSlice _choiceStr = ByteString.FromAsciiString("choice").ToSlice();
        private static ByteStringSlice _startConsoleStr = ByteString.FromAsciiString("startconsole").ToSlice();
        private static ByteStringSlice _finishConsoleStr = ByteString.FromAsciiString("finishconsole").ToSlice();
        private static ByteStringSlice _fontStr = ByteString.FromAsciiString("font").ToSlice();
        private static ByteStringSlice _styleStr = ByteString.FromAsciiString("style").ToSlice();
        private static ByteStringSlice _gotoStr = ByteString.FromAsciiString("goto").ToSlice();
        private static ByteStringSlice _nextWindowStr = ByteString.FromAsciiString("nextwindow").ToSlice();
        private static ByteStringSlice _returnStr = ByteString.FromAsciiString("return").ToSlice();
        private static ByteStringSlice _xyPrintStr = ByteString.FromAsciiString("xyprint").ToSlice();
        private static ByteStringSlice _xyPrintFXStr = ByteString.FromAsciiString("xyprintfx").ToSlice();
        private static ByteStringSlice _startSwitchStr = ByteString.FromAsciiString("startswitch").ToSlice();
        private static ByteStringSlice _finishSwitchStr = ByteString.FromAsciiString("finishswitch").ToSlice();
        private static ByteStringSlice _thinkSwitchStr = ByteString.FromAsciiString("thinkswitch").ToSlice();
        private static ByteStringSlice _bodyStr = ByteString.FromAsciiString("body").ToSlice();
        private static ByteStringSlice _backgroundStr = ByteString.FromAsciiString("background").ToSlice();
        private static ByteStringSlice _camStr = ByteString.FromAsciiString("cam").ToSlice();

        private static ByteStringSlice _fromStr = ByteString.FromAsciiString("from").ToSlice();
        private static ByteStringSlice _toStr = ByteString.FromAsciiString("to").ToSlice();
        private static ByteStringSlice _ownerStr = ByteString.FromAsciiString("owner").ToSlice();

        private static ByteStringSlice _yawStr = ByteString.FromAsciiString("yaw").ToSlice();
        private static ByteStringSlice _pitchStr = ByteString.FromAsciiString("pitch").ToSlice();
        private static ByteStringSlice _fovStr = ByteString.FromAsciiString("fov").ToSlice();
        private static ByteStringSlice _farStr = ByteString.FromAsciiString("far").ToSlice();
        private static ByteStringSlice _nearStr = ByteString.FromAsciiString("near").ToSlice();
        private static ByteStringSlice _fwdStr = ByteString.FromAsciiString("fwd").ToSlice();
        private static ByteStringSlice _speedStr = ByteString.FromAsciiString("speed").ToSlice();
        private static ByteStringSlice _liftStr = ByteString.FromAsciiString("lift").ToSlice();
        private static ByteStringSlice _lagStr = ByteString.FromAsciiString("lag").ToSlice();
        private static ByteStringSlice _occludeStr = ByteString.FromAsciiString("occlude").ToSlice();
        private static ByteStringSlice _restoreStr = ByteString.FromAsciiString("restore").ToSlice();
        private static ByteStringSlice _zipStr = ByteString.FromAsciiString("zip").ToSlice();

        private static ByteStringSlice _playerStr = ByteString.FromAsciiString("player").ToSlice();
        private static ByteStringSlice _npcStr = ByteString.FromAsciiString("npc").ToSlice();
        private static ByteString _clickStr = ByteString.FromAsciiString("_click_");
        private static ByteString _playerChar0Str = ByteString.FromAsciiString("playerchar0");

        private static ByteString _stayStr = ByteString.FromAsciiString("stay");
        private static ByteString _noStayStr = ByteString.FromAsciiString("nostay");
        private static ByteString _noneStr = ByteString.FromAsciiString("none");
        private static ByteString _stretchStr = ByteString.FromAsciiString("stretch");
        private static ByteString _tileStr = ByteString.FromAsciiString("tile");
        private static ByteString _solidStr = ByteString.FromAsciiString("solid");

        private static ByteString _color1Str = ByteString.FromAsciiString("color1");
        private static ByteString _color2Str = ByteString.FromAsciiString("color2");
        private static ByteString _color3Str = ByteString.FromAsciiString("color3");
        private static ByteString _color4Str = ByteString.FromAsciiString("color4");

        private static ByteString _defaultStretchStr = ByteString.FromAsciiString("default_stretch");
        private static ByteString _defaultTileStr = ByteString.FromAsciiString("default_tile");

        private static ByteString _zeroZeroStr = ByteString.FromAsciiString("0:0");

        private CompilerOptions _options;
        public IInlineSwitchIDGenerator _idGenerator;

        private static WindowFlagsResolution[] _windowFlagResolutions =
        {
            new WindowFlagsResolution("persist", FlagsCommand.FlagBit.Persist),
            new WindowFlagsResolution("noscroll", FlagsCommand.FlagBit.NoScroll),
            new WindowFlagsResolution("nograb", FlagsCommand.FlagBit.NoGrab),
            new WindowFlagsResolution("norelease", FlagsCommand.FlagBit.NoRelease),
            new WindowFlagsResolution("subtitle", FlagsCommand.FlagBit.Subtitle),
            new WindowFlagsResolution("passive2d", FlagsCommand.FlagBit.Passive2D),
            new WindowFlagsResolution("passive", FlagsCommand.FlagBit.Passive),
        };

        private static ImageFlagsResolution[] _imageFlagResolutions =
        {
            new ImageFlagsResolution("stretch", ImageCommand.EImageFlagBit.Stretch),
            new ImageFlagsResolution("solid", ImageCommand.EImageFlagBit.Solid),
            new ImageFlagsResolution("tile", ImageCommand.EImageFlagBit.Tile),
        };

        private struct WindowControlFlowBlock
        {
            public bool IsElse { get; set; }
            public bool IsBraced { get; set; }
            public IExprValue Condition { get; set; }
            public ILogger.LocationTag StartLocTag { get; set; }
        }

        public WindowCompiler(TokenReader2 reader, ExprParser exprParser, IList<Switch> inlineSwitches, ExprConverter exprConverter, IInlineSwitchIDGenerator idGenerator, CompilerOptions options)
        {
            _reader = reader;
            _inlineSwitches = inlineSwitches;
            _hasBackground = false;
            _exprParser = exprParser;
            _options = options;

            _titleCommands = new List<ConditionalFormattedStringCommand>();
            _bodyCommands = new List<ConditionalFormattedStringCommand>();
            _xyPrintFXCommands = new List<XYPrintFXCommand>();
            _flagsCommand = new FlagsCommand();
            _subWindowCommands = new List<SubWindowCommand>();
            _imageCommands = new List<ImageCommand>();
            _choiceCommands = new List<ChoiceCommand>();

            _controlFlowBlocks = new List<WindowControlFlowBlock>();

            _xpos = new OptionalExpression();
            _ypos = new OptionalExpression();
            _width = new OptionalExpression();
            _height = new OptionalExpression();
            _exprConverter = exprConverter;

            _idGenerator = idGenerator;
        }

        private void CompileWindowDirective(Token tok)
        {
            // State when entering this function is that the token has already been consumed
            switch (tok.TokenType)
            {
                case TokenType.EndOfLine:
                    return;
                case TokenType.CloseBrace:
                    if (_controlFlowBlocks.Count == 0)
                        throw new CompilerException(tok.Location, "Brace closes non-existent control flow block");

                    _controlFlowBlocks.RemoveAt(_controlFlowBlocks.Count - 1);
                    return;
                case TokenType.Identifier:
                    break;
                default:
                    throw new CompilerException(tok.Location, "Expected window directive but found something else");
            }

            if (tok.Value.Equals(_ifStr))
            {
                WindowControlFlowBlock newCFB = new WindowControlFlowBlock();
                newCFB.StartLocTag = tok.Location;

                _reader.ExpectToken(TokenReadMode.Normal, TokenType.OpenParen);

                IExprValue expr = _exprParser.ParseExpr(_reader);

                _reader.ExpectToken(TokenReadMode.Normal, TokenType.CloseParen);

                if (_controlFlowBlocks.Count > 0)
                {
                    int lastIndex = _controlFlowBlocks.Count - 1;
                    WindowControlFlowBlock lastBlock = _controlFlowBlocks[lastIndex];

                    if (!lastBlock.IsBraced)
                    {
                        _controlFlowBlocks.RemoveAt(lastIndex);
                        expr = new ExpressionExprValue(lastBlock.Condition, expr, ExpressionValue.EOperator.And);

                        if (_options.Optimize)
                            expr = Utils.OptimizeExpression(expr, newCFB.StartLocTag);
                    }
                }

                if (expr.ResultType != ExprResultType.Float)
                    throw new CompilerException(newCFB.StartLocTag, "Expression didn't evaluate to a number");

                newCFB.Condition = expr;
                newCFB.IsElse = false;

                _reader.SkipEndOfLines(TokenReadMode.Normal);
                tok = _reader.PeekToken(TokenReadMode.Normal);

                if (tok.TokenType == TokenType.OpenBrace)
                {
                    _reader.ConsumeToken();
                    newCFB.IsBraced = true;
                }

                _controlFlowBlocks.Add(newCFB);
                return;
            }

            if (tok.TokenType == TokenType.Identifier && tok.Value.Equals(_elseStr))
            {
                WindowControlFlowBlock newCFB = new WindowControlFlowBlock();
                newCFB.StartLocTag = tok.Location;
                newCFB.IsElse = true;

                if (_controlFlowBlocks.Count == 0)
                    throw new CompilerException(newCFB.StartLocTag, "'else' without matching 'if'");

                int lastIndex = _controlFlowBlocks.Count - 1;
                WindowControlFlowBlock existingCFB = _controlFlowBlocks[lastIndex];
                _controlFlowBlocks.RemoveAt(lastIndex);

                if (existingCFB.IsElse)
                    throw new CompilerException(newCFB.StartLocTag, "'else' after 'else'");


                _reader.SkipEndOfLines(TokenReadMode.Normal);
                tok = _reader.PeekToken(TokenReadMode.Normal);

                if (tok.TokenType == TokenType.OpenBrace)
                {
                    _reader.ConsumeToken();
                    newCFB.IsBraced = true;
                }

                newCFB.Condition = Utils.InvertCondition(existingCFB.Condition);
                return;
            }

            if (tok.TokenType != TokenType.Identifier)
                throw new CompilerException(tok.Location, "Expected directive");

            ByteStringSlice directiveToken = tok.Value;

            if (directiveToken.Equals(_titleStr))
                CompileCompiledFormattedStringDirective(ConditionalFormattedStringCommand.ECommandType.TitleCommand, _titleCommands);
            else if (directiveToken.Equals(_talkStr))
                CompileTalkDirective();
            else if (directiveToken.Equals(_talkExStr))
                CompileTalkExDirective();
            else if (directiveToken.Equals(_widthStr))
                CompileDimensionDirective(ref _width);
            else if (directiveToken.Equals(_heightStr))
                CompileDimensionDirective(ref _height);
            else if (directiveToken.Equals(_xposStr))
                CompileDimensionDirective(ref _xpos);
            else if (directiveToken.Equals(_yposStr))
                CompileDimensionDirective(ref _ypos);
            else if (directiveToken.Equals(_imageStr))
                CompileImageDirective();
            else if (directiveToken.Equals(_flagsStr))
                CompileFlagsDirective();
            else if (directiveToken.Equals(_subWindowStr))
                CompileSubWindowDirective();
            else if (directiveToken.Equals(_choiceStr))
                CompileChoiceDirective();
            else if (directiveToken.Equals(_startConsoleStr))
                CompileConsoleDirective(SimpleStringCommand.ECommandType.StartConsoleCommand, ref _startConsoleCommand);
            else if (directiveToken.Equals(_finishConsoleStr))
                CompileConsoleDirective(SimpleStringCommand.ECommandType.FinishConsoleCommand, ref _finishConsoleCommand);
            else if (directiveToken.Equals(_fontStr))
                CompileFontDirective();
            else if (directiveToken.Equals(_styleStr))
                CompileStyleDirective();
            else if (directiveToken.Equals(_gotoStr) || directiveToken.Equals(_nextWindowStr))
                CompileNextWindowDirective();
            else if (directiveToken.Equals(_returnStr))
                CompileReturnDirective();
            else if (directiveToken.Equals(_xyPrintStr))
                CompileXYPrintDirective();
            else if (directiveToken.Equals(_xyPrintFXStr))
                CompileXYPrintFXDirective();
            else if (directiveToken.Equals(_startSwitchStr))
                CompileSwitchDirective(WindowSwitchCommand.ECommandType.StartSwitchCommand, ref _startSwitchCommand);
            else if (directiveToken.Equals(_finishSwitchStr))
                CompileSwitchDirective(WindowSwitchCommand.ECommandType.FinishSwitchCommand,ref _finishSwitchCommand);
            else if (directiveToken.Equals(_thinkSwitchStr))
                CompileSwitchDirective(WindowSwitchCommand.ECommandType.ThinkSwitchCommand, ref _thinkSwitchCommand);
            else if (directiveToken.Equals(_bodyStr))
                CompileCompiledFormattedStringDirective(ConditionalFormattedStringCommand.ECommandType.BodyCommand, _bodyCommands);
            else if (directiveToken.Equals(_backgroundStr))
                CompileBackgroundDirective();
            else if (directiveToken.Equals(_camStr))
                CompileCamDirective();
            else
                throw new CompilerException(tok.Location, "Unknown directive");

            if (_controlFlowBlocks.Count > 0)
            {
                WindowControlFlowBlock lastCFB = _controlFlowBlocks[_controlFlowBlocks.Count - 1];

                if (!lastCFB.IsBraced)
                    _controlFlowBlocks.RemoveAt(_controlFlowBlocks.Count - 1);
            }

            tok = _reader.PeekToken(TokenReadMode.Normal);
            if (tok.TokenType != TokenType.EndOfFile && tok.TokenType != TokenType.EndOfLine)
                throw new CompilerException(tok.Location, "Expected end of line after directive");
        }

        private uint CompileBackgroundColor()
        {
            _reader.ExpectToken(TokenReadMode.Normal, TokenType.AssignmentOperator);

            Token hexStr = _reader.ReadToken(TokenReadMode.UnquotedString);

            if (hexStr.Value.Length != 8)
                throw new CompilerException(hexStr.Location, "Expected 8-digit hex value for background color");

            uint colorEncoded = 0;
            for (int byteIndex = 0; byteIndex < 4; byteIndex++)
            {
                for (int nibbleIndex = 0; nibbleIndex < 2; nibbleIndex++)
                {
                    byte b = hexStr.Value[byteIndex * 2 + nibbleIndex];

                    int bitPos = (4 - (nibbleIndex * 4)) + (byteIndex * 8);

                    int nibble = 0;
                    if (b >= '0' && b <= '9')
                        nibble = (b - '0');
                    else if (b >= 'a' && b <= 'f')
                        nibble = (b - 'a' + 0xa);
                    else if (b >= 'A' && b <= 'F')
                        nibble = (b - 'A' + 0xA);
                    else
                        throw new CompilerException(hexStr.Location, "Expected 8-digit hex value for background color");

                    colorEncoded |= ((uint)nibble) << bitPos;
                }
            }

            return colorEncoded;
        }

        private void CompileBackgroundDirective()
        {
            OptionalExpression condition;
            bool wantImageCmd = ResolveActiveCondition(out condition);

            ByteStringSlice imageNameSlice = new ByteStringSlice();
            bool haveImgName = false;
            bool haveStretchFlag = false;
            bool haveTileFlag = false;
            bool haveSolidFlag = false;
            bool haveNone = false;

            while (true)
            {
                Token tok = _reader.ReadToken(TokenReadMode.Normal);

                if (tok.TokenType == TokenType.StringLiteral)
                {
                    // Intentionally not escaped
                    imageNameSlice = tok.Value.SubSlice(1, tok.Value.Length - 2);
                    haveImgName = true;
                    if (imageNameSlice.Length == 0)
                        throw new CompilerException(tok.Location, "Background image name was empty");
                }
                else if (tok.TokenType == TokenType.Identifier)
                {
                    if (tok.Value.Equals(_color1Str))
                        _backgroundColor1 = CompileBackgroundColor();
                    else if (tok.Value.Equals(_color2Str))
                        _backgroundColor2 = CompileBackgroundColor();
                    else if (tok.Value.Equals(_color3Str))
                        _backgroundColor3 = CompileBackgroundColor();
                    else if (tok.Value.Equals(_color4Str))
                        _backgroundColor4 = CompileBackgroundColor();
                    else if (tok.Value.Equals(_stretchStr))
                        haveStretchFlag = true;
                    else if (tok.Value.Equals(_tileStr))
                        haveTileFlag = true;
                    else if (tok.Value.Equals(_solidStr))
                        haveSolidFlag = true;
                    else if (tok.Value.Equals(_noneStr))
                        haveNone = true;
                    else
                        throw new CompilerException(tok.Location, "Unexpected background command flag");

                }
                else
                    throw new CompilerException(tok.Location, "Background flag expected");

                tok = _reader.PeekToken(TokenReadMode.Normal);
                if (tok.TokenType == TokenType.EndOfLine || tok.TokenType == TokenType.EndOfLine)
                    break;
            }

            int mutexImgFlags = 0;
            if (haveStretchFlag)
                mutexImgFlags += 1;
            if (haveTileFlag)
                mutexImgFlags += 1;
            if (haveNone)
                mutexImgFlags += 1;

            if (mutexImgFlags > 1)
                throw new CompilerException(_reader.Location, "Background contained multiple mutually-exclusive flags (stretch, tile, none)");

            if (haveStretchFlag && !haveImgName)
            {
                haveImgName = true;
                imageNameSlice = _defaultStretchStr.ToSlice();
            }
            else if (haveTileFlag && !haveImgName)
            {
                haveImgName = true;
                imageNameSlice = _defaultTileStr.ToSlice();
            }

            if (wantImageCmd && haveImgName && !haveNone)
            {
                List<ImageCommand.EImageFlagBit> flags = new List<ImageCommand.EImageFlagBit>();
                if (haveStretchFlag)
                    flags.Add(ImageCommand.EImageFlagBit.Stretch);
                if (haveTileFlag)
                    flags.Add(ImageCommand.EImageFlagBit.Tile);
                if (haveSolidFlag)
                    flags.Add(ImageCommand.EImageFlagBit.Solid);

                OptionalExpression zero = _exprConverter.ConvertValueToOptionalExpression(new FloatConstExprValue(0.0f), _reader.Location);

                _imageCommands.Add(new ImageCommand(condition, imageNameSlice.ToByteString(), zero, zero, new OptionalExpression(), new OptionalExpression(), flags));
            }

            _hasBackground = true;
        }

        private bool ResolveActiveCondition(out OptionalExpression expr)
        {
            if (_controlFlowBlocks.Count == 0)
            {
                expr = new OptionalExpression();
                return true;
            }

            IExprValue condition = _controlFlowBlocks[0].Condition;

            for (int i = 1; i < _controlFlowBlocks.Count; i++)
            {
                IExprValue nextCondition = _controlFlowBlocks[i].Condition;
                condition = new ExpressionExprValue(condition, nextCondition, ExpressionValue.EOperator.And);
            }

            return _exprConverter.CheckAndConvertCondition(condition, _controlFlowBlocks[0].StartLocTag, out expr);
        }

        private OptionalString ParseCamInlineString()
        {
            _reader.ExpectToken(TokenReadMode.Normal, TokenType.OpenParen);

            Token tok = _reader.ReadToken(TokenReadMode.UnquotedString,
                TokenReadProperties.Default
                .Add(TokenReadProperties.Flag.IgnoreEscapes)
                .Add(TokenReadProperties.Flag.IgnoreQuotes)
                .Add(TokenReadProperties.Flag.IgnoreWhitespace)
                .Add(TokenReadProperties.Flag.StopAtCloseParen));

            ByteStringSlice slice = tok.Value;
            if (tok.TokenType != TokenType.AbstractString && tok.TokenType != TokenType.Identifier)
            {
                // Only possible via macros
                if (tok.TokenType == TokenType.StringLiteral)
                    slice = Utils.EscapeSlice(slice.SubSlice(1, slice.Length - 2), tok.Location, true, false);
            }

            if (slice.Length == 0)
                throw new CompilerException(tok.Location, "Cam param string was empty");

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.CloseParen);
            return new OptionalString(slice.ToByteString());
        }

        private ushort ParseCamParam()
        {
            _reader.ExpectToken(TokenReadMode.Normal, TokenType.OpenParen);
            Token tok = _reader.ExpectToken(TokenReadMode.Normal, TokenType.NumericLiteral);
            _reader.ExpectToken(TokenReadMode.Normal, TokenType.CloseParen);

            uint result = 0;

            foreach (byte b in tok.Value)
            {
                if (b >= '0' && b <= '9')
                {
                    result = result * 10 + (uint)(b - '0');
                    if (result > ushort.MaxValue)
                        throw new CompilerException(tok.Location, "Camera parameter is too large");
                }
                else
                    throw new CompilerException(tok.Location, "Invalid camera parameter");
            }

            if (result == CamCommand.UnsetValue)
                throw new CompilerException(tok.Location, "Camera parameter is set to a a reserved value");

            return (ushort)result;
        }

        private void CompileCamDirective()
        {
            if (_camCommand != null)
                throw new CompilerException(_reader.Location, "cam command already defined");

            CheckNoCondition();

            Token nameTok;

            if (_options.DParseCamCommandHandling)
            {
                nameTok = _reader.ReadToken(TokenReadMode.UnquotedString);
                if (_options.Logger != null)
                {
                    foreach (byte b in nameTok.Value)
                    {
                        if (b == '(')
                        {
                            _options.Logger.WriteLine(new ILogger.MessageProperties(ILogger.Severity.Warning, nameTok.Location), "First parameter to 'cam' is the camera name, but this is formatted as if it's a parameter");
                            break;
                        }
                    }
                }
            }
            else
                nameTok = _reader.ExpectToken(TokenReadMode.Normal, TokenType.Identifier);

            ByteString name = nameTok.Value.ToByteString();

            OptionalString from = new OptionalString();
            OptionalString to = new OptionalString();
            OptionalString owner = new OptionalString();

            ushort yaw = CamCommand.UnsetValue;
            ushort pitch = CamCommand.UnsetValue;
            ushort fov = CamCommand.UnsetValue;
            ushort far = CamCommand.UnsetValue;
            ushort near = CamCommand.UnsetValue;
            ushort fwd = CamCommand.UnsetValue;
            ushort speed = CamCommand.UnsetValue;
            ushort lift = CamCommand.UnsetValue;
            ushort lag = CamCommand.UnsetValue;
            ushort occlude = CamCommand.UnsetValue;
            ushort restore = CamCommand.UnsetValue;
            ushort zip = CamCommand.UnsetValue;

            while (true)
            {
                Token paramTok = _reader.PeekToken(TokenReadMode.Normal);
                if (paramTok.TokenType == TokenType.EndOfLine || paramTok.TokenType == TokenType.EndOfFile)
                    break;

                paramTok = _reader.ExpectToken(TokenReadMode.Normal, TokenType.Identifier);
                ByteStringSlice paramName = paramTok.Value;

                if (paramName.Equals(_fromStr))
                    from = ParseCamInlineString();
                else if (paramName.Equals(_toStr))
                    to = ParseCamInlineString();
                else if (paramName.Equals(_ownerStr))
                    owner = ParseCamInlineString();
                else if (paramName.Equals(_yawStr))
                    yaw = ParseCamParam();
                else if (paramName.Equals(_pitchStr))
                    pitch = ParseCamParam();
                else if (paramName.Equals(_fovStr))
                    fov = ParseCamParam();
                else if (paramName.Equals(_farStr))
                    far = ParseCamParam();
                else if (paramName.Equals(_nearStr))
                    near = ParseCamParam();
                else if (paramName.Equals(_fwdStr))
                    fwd = ParseCamParam();
                else if (paramName.Equals(_speedStr))
                    speed = ParseCamParam();
                else if (paramName.Equals(_liftStr))
                    lift = ParseCamParam();
                else if (paramName.Equals(_lagStr))
                    lag = ParseCamParam();
                else if (paramName.Equals(_occludeStr))
                {
                    occlude = ParseCamParam();
                    if (occlude != 1 && occlude != 0)
                        throw new CompilerException(paramTok.Location, "Only 1 and 0 are allowed for 'occlude'");
                }
                else if (paramName.Equals(_restoreStr))
                    restore = 1;
                else if (paramName.Equals(_zipStr))
                    zip = 1;
                else
                    throw new CompilerException(paramTok.Location, "Invalid cam parameter");
            }

            _camCommand = new CamCommand(name, from, to, owner, yaw, pitch, fov, far, near, fwd, speed, lift, lag, occlude, restore, zip);
        }

        private void CheckNoCondition()
        {
            if (_controlFlowBlocks.Count != 0)
                throw new CompilerException(_reader.Location, "Unconditional command inside of a condition");
        }

        private void CompileSwitchDirective(WindowSwitchCommand.ECommandType cmdType, ref WindowSwitchCommand? swCommand)
        {
            CheckNoCondition();

            if (swCommand != null)
                throw new CompilerException(_reader.Location, "Switch command type was specified multiple times");

            Token nextTok = _reader.PeekToken(TokenReadMode.Normal);

            if (nextTok.TokenType == TokenType.EndOfLine || nextTok.TokenType == TokenType.OpenBrace)
            {
                _reader.SkipEndOfLines(TokenReadMode.Normal);

                _reader.ExpectToken(TokenReadMode.Normal, TokenType.OpenBrace);

                uint switchID;
                if (!_idGenerator.TryGenerateNextID(out switchID))
                    throw new CompilerException(_reader.Location, "Too many inline switches");

                SwitchCompiler swCompiler = new SwitchCompiler(_reader, _exprParser, _exprConverter, _options);

                bool isImmediatelyAfterTLD;
                Switch sw = swCompiler.Compile(switchID, true, out isImmediatelyAfterTLD);

                if (isImmediatelyAfterTLD)
                    throw new CompilerException(_reader.Location, "Inline switch statement ended at unexpected TLD");

                _reader.ExpectToken(TokenReadMode.Normal, TokenType.CloseBrace);

                _inlineSwitches.Add(sw);

                swCommand = new WindowSwitchCommand(cmdType, switchID);
            }
            else
            {
                uint label = _exprParser.ParseLabel(_reader);
                swCommand = new WindowSwitchCommand(cmdType, label);
            }
        }

        private void CompileXYPrintDirective()
        {
            OptionalExpression condition;
            if (!ResolveActiveCondition(out condition))
                return;

            OptionalExpression xCoord = _exprConverter.ConvertValueToOptionalExpression(_exprParser.ParseExprPreferFloat(_reader), _reader.Location);

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.Comma);
            OptionalExpression yCoord = _exprConverter.ConvertValueToOptionalExpression(_exprParser.ParseExprPreferFloat(_reader), _reader.Location);

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.Comma);
            OptionalExpression alpha = _exprConverter.ConvertValueToOptionalExpression(_exprParser.ParseExprPreferFloat(_reader), _reader.Location);

            OptionalExpression red = new OptionalExpression();
            OptionalExpression green = new OptionalExpression();
            OptionalExpression blue = new OptionalExpression();
            OptionalString font = new OptionalString();

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.Comma);
            Token messageTok = _reader.ReadToken(TokenReadMode.QuotedString, TokenReadProperties.Default.Add(TokenReadProperties.Flag.AllowNewLineInString));

            ByteString message = Utils.EscapeSlice(messageTok.Value.SubSlice(1, messageTok.Value.Length - 2), messageTok.Location, true, true).ToByteString();

            FormattingValue format = _exprParser.ParseOptionalFormattingValueList(_reader);

            _xyPrintFXCommands.Add(new XYPrintFXCommand(xCoord, yCoord, alpha, red, green, blue, font, message, condition, format));
        }

        private Token ParseIdentifierAsString(bool stopAtCommas)
        {
            TokenReadProperties readProps = TokenReadProperties.Default
                .Add(TokenReadProperties.Flag.IgnoreQuotes)
                .Add(TokenReadProperties.Flag.IgnoreEscapes);

            if (stopAtCommas)
                readProps = readProps.Add(TokenReadProperties.Flag.StopAtComma);

            Token tok = _reader.ReadToken(TokenReadMode.UnquotedString, readProps);

            if (_options.Logger != null && tok.TokenType == TokenType.AbstractString && (tok.Value[0] == '\"' || tok.Value[tok.Value.Length - 1] == '\"'))
                _options.Logger.WriteLine(new ILogger.MessageProperties(ILogger.Severity.Warning, tok.Location), "Value is a quoted string, but quotes will be parsed as part of the string in this context");

            if (tok.TokenType == TokenType.StringLiteral)
            {
                // Kind of a hack
                ByteStringSlice quoteStrippedName = tok.Value.SubSlice(1, tok.Value.Length - 2);
                tok = new Token(tok.TokenType, quoteStrippedName, tok.Location);
            }

            if (_options.Logger != null && tok.Value[tok.Value.Length - 1] == '$')
                _options.Logger.WriteLine(new ILogger.MessageProperties(ILogger.Severity.Warning, tok.Location), "Value is parsed as a string, but a variable name was specified");

            return tok;
        }

        private void CompileXYPrintFXDirective()
        {
            OptionalExpression condition;
            if (!ResolveActiveCondition(out condition))
                return;

            OptionalExpression xCoord = _exprConverter.ConvertValueToOptionalExpression(_exprParser.ParseExprPreferFloat(_reader), _reader.Location);

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.Comma);
            OptionalExpression yCoord = _exprConverter.ConvertValueToOptionalExpression(_exprParser.ParseExprPreferFloat(_reader), _reader.Location);

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.Comma);
            OptionalExpression alpha = _exprConverter.ConvertValueToOptionalExpression(_exprParser.ParseExprPreferFloat(_reader), _reader.Location);

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.Comma);
            OptionalExpression red = _exprConverter.ConvertValueToOptionalExpression(_exprParser.ParseExprPreferFloat(_reader), _reader.Location);

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.Comma);
            OptionalExpression green = _exprConverter.ConvertValueToOptionalExpression(_exprParser.ParseExprPreferFloat(_reader), _reader.Location);

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.Comma);
            OptionalExpression blue = _exprConverter.ConvertValueToOptionalExpression(_exprParser.ParseExprPreferFloat(_reader), _reader.Location);

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.Comma);
            Token fontTok = ParseIdentifierAsString(true);

            OptionalString font = new OptionalString(fontTok.Value.ToByteString());

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.Comma);
            Token messageTok = _reader.ReadToken(TokenReadMode.QuotedString, TokenReadProperties.Default.Add(TokenReadProperties.Flag.AllowNewLineInString));

            ByteString message = Utils.EscapeSlice(messageTok.Value.SubSlice(1, messageTok.Value.Length - 2), messageTok.Location, true, true).ToByteString();

            FormattingValue format = _exprParser.ParseOptionalFormattingValueList(_reader);

            _xyPrintFXCommands.Add(new XYPrintFXCommand(xCoord, yCoord, alpha, red, green, blue, font, message, condition, format));
        }

        private void CompileReturnDirective()
        {
            CheckNoCondition();

            if (_nextWindowCommand != null)
                throw new CompilerException(_reader.Location, "nextwindow/goto/return is already defined");

            _nextWindowCommand = new SimpleStringCommand(SimpleStringCommand.ECommandType.NextWindowCommand, _zeroZeroStr);
        }

        private void CompileNextWindowDirective()
        {
            CheckNoCondition();

            if (_nextWindowCommand != null)
                throw new CompilerException(_reader.Location, "nextwindow/goto/return is already defined");

            Token tok = _reader.ReadToken(TokenReadMode.UnquotedString);

            _nextWindowCommand = new SimpleStringCommand(SimpleStringCommand.ECommandType.NextWindowCommand, tok.Value.ToByteString());
        }
         
        private void CompileStyleDirective()
        {
            CheckNoCondition();

            ByteStringSlice nameSlice = _exprParser.ReadOptionallyQuotedName(_reader, _options.Logger);

            if (_styleCommand != null)
                throw new CompilerException(_reader.Location, "Style is already defined");

            _styleCommand = new SimpleStringCommand(SimpleStringCommand.ECommandType.StyleCommand, nameSlice.ToByteString());
        }

        private void CompileFontDirective()
        {
            CheckNoCondition();

            if (_fontCommand != null)
                throw new CompilerException(_reader.Location, "font command is already defined");

            ByteStringSlice fontName = _exprParser.ReadOptionallyQuotedName(_reader, _options.Logger);

            _fontCommand = new SimpleStringCommand(SimpleStringCommand.ECommandType.FontCommand, fontName.ToByteString());
        }

        private void CompileConsoleDirective(SimpleStringCommand.ECommandType cmdType, ref SimpleStringCommand? consoleCmd)
        {
            CheckNoCondition();

            if (consoleCmd != null)
                throw new CompilerException(_reader.Location, "Console directive is already defined");

            if (_options.Logger != null)
                _options.Logger.WriteLine(new ILogger.MessageProperties(ILogger.Severity.Warning, _reader.Location), "Console commands are deprecated");

            Token consoleCmdTok = _reader.ReadToken(TokenReadMode.QuotedString, TokenReadProperties.Default.Add(TokenReadProperties.Flag.AllowNewLineInString));

            ByteStringSlice escapedCmd = Utils.EscapeSlice(consoleCmdTok.Value.SubSlice(1, consoleCmdTok.Value.Length - 2), consoleCmdTok.Location, true, true);

            byte[] paddedCmd = new byte[escapedCmd.Length + 1];
            for (int i = 0; i < escapedCmd.Length; i++)
                paddedCmd[i] = escapedCmd[i];
            paddedCmd[paddedCmd.Length - 1] = 10;

            consoleCmd = new SimpleStringCommand(cmdType, new ByteString(paddedCmd));
        }

        private void CompileChoiceDirective()
        {
            OptionalExpression condition;
            if (!ResolveActiveCondition(out condition))
                return;

            Token textToken = _reader.ReadToken(TokenReadMode.QuotedString);

            ByteStringSlice str = Utils.EscapeSlice(textToken.Value.SubSlice(1, textToken.Value.Length - 2), textToken.Location, true, false);

            List<TypedFormattingValue> tfvs = new List<TypedFormattingValue>();
            uint label = 0;

            Token tok = _reader.PeekToken(TokenReadMode.Normal);
            if (tok.TokenType == TokenType.Comma)
            {
                _reader.ConsumeToken();

                while (true)
                {
                    Token argTok = _reader.PeekToken(TokenReadMode.Normal, TokenReadProperties.Default.Add(TokenReadProperties.Flag.IgnoreEscapes));

                    if (argTok.TokenType == TokenType.NumericLiteral)
                    {
                        // We need to detect if this is the label or an argument
                        _reader.ConsumeToken();

                        Token possiblyColonTok = _reader.PeekToken(TokenReadMode.Normal);

                        if (possiblyColonTok.TokenType == TokenType.Colon)
                        {
                            _reader.ConsumeToken();
                            Token lowPartTok = _reader.ExpectToken(TokenReadMode.Normal, TokenType.NumericLiteral);

                            label = ExprParser.ParseLabelFromTokens(argTok, lowPartTok);
                            break;
                        }

                        float fv = ExprParser.ResolveFloatLiteralToken(argTok);
                        tfvs.Add(new TypedFormattingValue(TypedFormattingValue.EFormattingValueType.Float, new FloatOperand(ExprParser.ResolveFloatLiteralToken(argTok))));
                    }
                    else
                        tfvs.Add(_exprParser.ParseFormattingValue(_reader));

                    _reader.ExpectToken(TokenReadMode.Normal, TokenType.Comma);
                }
            }
            else
                label = _exprParser.ParseLabel(_reader);

            _choiceCommands.Add(new ChoiceCommand(condition, str.ToByteString(), new FormattingValue(tfvs), label));
        }

        private void CompileSubWindowDirective()
        {
            CheckNoCondition();

            uint label = _exprParser.ParseLabel(_reader);

            _subWindowCommands.Add(new SubWindowCommand(label));
        }

        private void CompileFlagsDirective()
        {
            CheckNoCondition();

            while (true)
            {
                Token tok = _reader.PeekToken(TokenReadMode.Normal);
                if (tok.TokenType == TokenType.EndOfLine || tok.TokenType == TokenType.EndOfFile)
                    break;

                tok = _reader.ExpectToken(TokenReadMode.Normal, TokenType.Identifier);

                bool found = false;
                foreach (WindowFlagsResolution flagRes in _windowFlagResolutions)
                {
                    if (flagRes.Str.Equals(tok.Value))
                    {
                        _flagsCommand.SetFlag(flagRes.Flag);
                        found = true;
                        break;
                    }
                }

                if (!found)
                    throw new CompilerException(tok.Location, "Invalid flag");
            }
        }

        private void WarnIfImageFlag(IExprValue exprValue, ILogger.LocationTag tag)
        {
            if (_options.Logger == null)
                return;

            if (exprValue.ExprType == ExprType.FloatVar)
            {
                ByteStringSlice varName = ((FloatVarExprValue)exprValue).VarName;

                foreach (ImageFlagsResolution ifr in _imageFlagResolutions)
                {
                    if (varName.Equals(ifr.Str))
                    {
                        _options.Logger.WriteLine(new ILogger.MessageProperties(ILogger.Severity.Warning, tag), "Image flag name was used as a parameter that accepts a float variable");
                        return;
                    }
                }
            }
        }

        private void CompileImageDirective()
        {
            OptionalExpression condition;
            if (!ResolveActiveCondition(out condition))
                return;

            ByteStringSlice nameSlice = _exprParser.ReadOptionallyQuotedName(_reader, _options.Logger);

            ILogger.LocationTag xposLoc = _reader.Location;
            IExprValue xpos = _exprParser.ParseExprPreferFloat(_reader);

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.Comma);

            ILogger.LocationTag yposLoc = _reader.Location;
            IExprValue ypos = _exprParser.ParseExprPreferFloat(_reader);

            ILogger.LocationTag widthLoc = new ILogger.LocationTag();
            IExprValue? width = null;

            ILogger.LocationTag heightLoc = new ILogger.LocationTag();
            IExprValue? height = null;

            List<ImageCommand.EImageFlagBit> flags = new List<ImageCommand.EImageFlagBit>();

            Token tok = _reader.PeekToken(TokenReadMode.Normal);
            if (tok.TokenType == TokenType.Comma)
            {
                _reader.ConsumeToken();

                widthLoc = _reader.Location;
                width = _exprParser.ParseExprPreferFloat(_reader);

                WarnIfImageFlag(width, widthLoc);

                tok = _reader.PeekToken(TokenReadMode.Normal);

                if (tok.TokenType == TokenType.Comma)
                {
                    _reader.ConsumeToken();

                    heightLoc = _reader.Location;
                    height = _exprParser.ParseExprPreferFloat(_reader);

                    WarnIfImageFlag(height, heightLoc);

                    tok = _reader.PeekToken(TokenReadMode.Normal);

                    if (tok.TokenType == TokenType.Comma)
                    {
                        _reader.ConsumeToken();

                        while (true)
                        {
                            Token flagTok = _reader.ExpectToken(TokenReadMode.Normal, TokenType.Identifier);

                            bool found = false;
                            foreach (ImageFlagsResolution ifr in _imageFlagResolutions)
                            {
                                if (ifr.Str.Equals(flagTok.Value))
                                {
                                    if (flags.Contains(ifr.Flag))
                                        throw new CompilerException(flagTok.Location, "Image flag was specified multiple times");

                                    found = true;
                                    flags.Add(ifr.Flag);
                                    break;
                                }
                            }

                            if (!found)
                                throw new CompilerException(flagTok.Location, "Unknown image flag");

                            tok = _reader.PeekToken(TokenReadMode.Normal);
                            if (tok.TokenType == TokenType.EndOfFile || tok.TokenType == TokenType.EndOfLine)
                                break;
                        }
                    }
                }
            }

            _imageCommands.Add(new ImageCommand(condition, nameSlice.ToByteString(),
                _exprConverter.ConvertValueToOptionalExpression(xpos, xposLoc),
                _exprConverter.ConvertValueToOptionalExpression(ypos, yposLoc),
                _exprConverter.ConvertValueToOptionalExpression(width, widthLoc),
                _exprConverter.ConvertValueToOptionalExpression(height, heightLoc),
                flags));
        }

        private void CompileDimensionDirective(ref OptionalExpression expr)
        {
            CheckNoCondition();

            if (expr.Expression != null)
                throw new CompilerException(_reader.Location, "Dimension value is already set");

            ILogger.LocationTag locTag = _reader.Location;
            IExprValue exprValue = _exprParser.ParseExpr(_reader);

            expr = _exprConverter.ConvertValueToOptionalExpression(exprValue, locTag);
        }

        private void CompileTalkExDirective()
        {
            CheckNoCondition();

            if (_talkCommand != null)
                throw new CompilerException(_reader.Location, "talk command already defined");

            Token name1Tok = ParseIdentifierAsString(false);
            Token name2Tok = ParseIdentifierAsString(false);
            Token anim1Tok = ParseIdentifierAsString(false);
            Token anim2Tok = ParseIdentifierAsString(false);
            bool stay1Flag = true;
            bool stay2Flag = true;

            Token stay1Tok = _reader.PeekToken(TokenReadMode.Normal);
            if (stay1Tok.TokenType != TokenType.EndOfLine && stay1Tok.TokenType != TokenType.EndOfFile)
            {
                stay1Tok = _reader.ExpectToken(TokenReadMode.Normal, TokenType.Identifier);
                Token stay2Tok = _reader.ExpectToken(TokenReadMode.Normal, TokenType.Identifier);

                Token[] stayTokens = { stay1Tok, stay2Tok };
                bool[] stayFlags = { stay1Flag, stay2Flag };

                for (int i = 0; i < 2; i++)
                {
                    Token flagTok = stayTokens[i];
                    if (flagTok.Value.Equals(_stayStr))
                        stayFlags[i] = true;
                    else if (flagTok.Value.Equals(_noStayStr))
                        stayFlags[i] = false;
                    else
                        throw new CompilerException(flagTok.Location, "Talk flag must be 'stay' or 'nostay'");

                    stay1Flag = stayFlags[0];
                    stay2Flag = stayFlags[1];
                }
            }

            _talkCommand = new TalkCommand(anim1Tok.Value.ToByteString(), new OptionalString(anim2Tok.Value.ToByteString()), name1Tok.Value.ToByteString(), name2Tok.Value.ToByteString(), stay1Flag, stay2Flag);
        }

        private void CompileTalkDirective()
        {
            CheckNoCondition();

            if (_talkCommand != null)
                throw new CompilerException(_reader.Location, "talk command already defined");

            Token targetTok = _reader.ExpectToken(TokenReadMode.Normal, TokenType.Identifier);

            ByteString name1;
            ByteString name2;
            if (targetTok.Value.Equals(_npcStr))
            {
                name1 = _clickStr;
                name2 = _playerChar0Str;
            }
            else if (targetTok.Value.Equals(_playerStr))
            {
                name1 = _playerChar0Str;
                name2 = _clickStr;
            }
            else
                throw new CompilerException(targetTok.Location, "Target must be either 'player' or 'npc'");

            Token animTok = ParseIdentifierAsString(false);

            bool stayFlag = true;
            bool isStayFlagSet = false;
            while (true)
            {
                Token flagTok = _reader.PeekToken(TokenReadMode.Normal);

                if (flagTok.TokenType == TokenType.EndOfLine || flagTok.TokenType == TokenType.EndOfFile)
                    break;

                flagTok = _reader.ExpectToken(TokenReadMode.Normal, TokenType.Identifier);

                if (flagTok.Value.Equals(_stayStr))
                {
                    if (isStayFlagSet)
                        throw new CompilerException(flagTok.Location, "'stay' or 'nostay' specified multiple times");

                    isStayFlagSet = true;
                    stayFlag = true;
                }
                else if (flagTok.Value.Equals(_noStayStr))
                {
                    if (isStayFlagSet)
                        throw new CompilerException(flagTok.Location, "'stay' or 'nostay' specified multiple times");

                    isStayFlagSet = true;
                    stayFlag = false;
                }
                else
                    throw new CompilerException(flagTok.Location, "Unexpected flag token");
            }

            _talkCommand = new TalkCommand(animTok.Value.ToByteString(), new OptionalString(), name1, name2, true, stayFlag);
        }

        private void CompileCompiledFormattedStringDirective(ConditionalFormattedStringCommand.ECommandType commandType, IList<ConditionalFormattedStringCommand> outputList)
        {
            OptionalExpression condition;
            if (!ResolveActiveCondition(out condition))
                return;

            Token titleTextTok = _reader.ReadToken(TokenReadMode.QuotedString, TokenReadProperties.Default.Add(TokenReadProperties.Flag.AllowNewLineInString));

            ByteStringSlice text = Utils.EscapeSlice(titleTextTok.Value.SubSlice(1, titleTextTok.Value.Length - 2), titleTextTok.Location, true, true);

            FormattingValue formattingValue = _exprParser.ParseOptionalFormattingValueList(_reader);

            outputList.Add(new ConditionalFormattedStringCommand(commandType, condition, text.ToByteString(), formattingValue));
        }

        private static void AddOptional(IList<IWindowCommand> commands, IWindowCommand? cmd)
        {
            if (cmd != null)
                commands.Add(cmd);
        }

        private IEnumerable<IWindowCommand> CollateCommands()
        {
            List<IWindowCommand> windowCommands = new List<IWindowCommand>();

            windowCommands.AddRange(_titleCommands);
            windowCommands.AddRange(_bodyCommands);
            windowCommands.AddRange(_xyPrintFXCommands);

            AddOptional(windowCommands, _camCommand);
            AddOptional(windowCommands, _talkCommand);
            AddOptional(windowCommands, _startConsoleCommand);
            AddOptional(windowCommands, _finishConsoleCommand);
            AddOptional(windowCommands, _startSwitchCommand);
            AddOptional(windowCommands, _thinkSwitchCommand);
            AddOptional(windowCommands, _finishSwitchCommand);
            AddOptional(windowCommands, _styleCommand);
            AddOptional(windowCommands, _fontCommand);

            if (_flagsCommand.Flags != 0)
                windowCommands.Add(_flagsCommand);

            if (_backgroundColor1 != 0 || _backgroundColor2 != 0 || _backgroundColor3 != 0 || _backgroundColor4 != 0)
                windowCommands.Add(new BackgroundCommand(_backgroundColor1, _backgroundColor2, _backgroundColor3, _backgroundColor4));

            AddOptional(windowCommands, new DimensionsCommand(_xpos, _ypos, _width, _height));

            windowCommands.AddRange(_subWindowCommands);
            windowCommands.AddRange(_imageCommands);
            windowCommands.AddRange(_choiceCommands);

            AddOptional(windowCommands, _nextWindowCommand);

            return windowCommands;
        }

        public Window Compile(out bool isImmediatelyAfterTLD)
        {
            isImmediatelyAfterTLD = false;

            uint label = _exprParser.ParseLabel(_reader);
            _reader.ExpectToken(TokenReadMode.Normal, TokenType.EndOfLine);

            while (true)
            {
                Token tok = _reader.PeekToken(TokenReadMode.Normal);

                if (tok.TokenType == TokenType.TopLevelDirective)
                {
                    _reader.ConsumeToken();

                    tok = _reader.PeekToken(TokenReadMode.Normal);
                    if (tok.Value.Equals(_defineStr))
                    {
                        _reader.ParseInlineMacro();
                        continue;
                    }
                    else
                    {
                        isImmediatelyAfterTLD = true;
                        break;
                    }
                }

                if (tok.TokenType == TokenType.EndOfFile)
                    break;

                _reader.ConsumeToken();
                CompileWindowDirective(tok);
            }

            if (!_hasBackground)
                _flagsCommand.SetFlag(FlagsCommand.FlagBit.NoBackground);

            return new Window(label, new WindowCommandList(CollateCommands()));
        }
    }
}
