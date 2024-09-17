using AnoxAPE.Elements;
using System.Reflection.PortableExecutable;

namespace AnoxAPE.HLCompiler
{
    internal class WindowCompiler
    {
        private PositionTrackingReader _reader;
        private TokenReader _tokenReader;
        private IList<Switch> _inlineSwitches;
        private bool _hasBackground;
        private ExprParser _exprParser;

        private List<TitleCommand> _titleCommands;
        private List<BodyCommand> _bodyCommands;
        private List<XYPrintFXCommand> _xyPrintFXCommands;
        private TalkCommand? _talkCommand;
        private SimpleStringCommand? _startConsoleCommand;
        private SimpleStringCommand? _finishConsoleCommand;
        private WindowSwitchCommand? _startSwitchCommand;
        private WindowSwitchCommand? _thinkSwitchCommand;
        private WindowSwitchCommand? _finishSwitchCommand;
        private SimpleStringCommand? _styleCommand;
        private SimpleStringCommand? _fontCommand;
        private FlagsCommand _flagsCommand;
        private BackgroundCommand _backgroundCommand;
        private OptionalExpression _xpos;
        private OptionalExpression _ypos;
        private OptionalExpression _width;
        private OptionalExpression _height;
        private List<SubWindowCommand> _subWindowCommands;
        private List<ImageCommand> _imageCommands;
        private List<ChoiceCommand> _choiceCommands;
        private SimpleStringCommand? _nextWindowCommand;

        private List<WindowControlFlowBlock> _controlFlowBlocks;

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

        private struct WindowControlFlowBlock
        {
            public bool IsElse { get; set; }
            public bool IsBraced { get; set; }
            public IExprValue Condition { get; set; }
            public ILogger.LocationTag StartLocTag { get; set; }
        }

        public WindowCompiler(PositionTrackingReader reader, TokenReader tokenReader, ExprParser exprParser, IList<Switch> inlineSwitches)
        {
            _reader = reader;
            _tokenReader = tokenReader;
            _inlineSwitches = inlineSwitches;
            _hasBackground = false;
            _exprParser = exprParser;

            _titleCommands = new List<TitleCommand>();
            _bodyCommands = new List<BodyCommand>();
            _xyPrintFXCommands = new List<XYPrintFXCommand>();
            _flagsCommand = new FlagsCommand();
            _backgroundCommand = new BackgroundCommand();
            _subWindowCommands = new List<SubWindowCommand>();
            _imageCommands = new List<ImageCommand>();
            _choiceCommands = new List<ChoiceCommand>();

            _controlFlowBlocks = new List<WindowControlFlowBlock>();

            _xpos = new OptionalExpression();
            _ypos = new OptionalExpression();
            _width = new OptionalExpression();
            _height = new OptionalExpression();
        }

        private void ErrorAtCurrentLocation(string msg)
        {
            throw new CompilerException(_reader.LocationTag, msg);
        }

        private void CompileWindowDirective()
        {
            if (_reader.PeekOne() == '}')
            {
                if (_controlFlowBlocks.Count == 0)
                    ErrorAtCurrentLocation("Brace closes non-existent control flow block");

                _controlFlowBlocks.RemoveAt(_controlFlowBlocks.Count - 1);
                return;
            }

            TokenReadBehavior directiveTRB = new TokenReadBehavior();
            directiveTRB.NonStringAllowed = true;

            ByteStringSlice directiveToken = _tokenReader.ReadToken(_reader, directiveTRB);

            if (directiveToken.Equals(_ifStr))
            {
                WindowControlFlowBlock newCFB = new WindowControlFlowBlock();
                newCFB.StartLocTag = _reader.LocationTag;

                _tokenReader.SkipWhitespace(_reader, EOLBehavior.Fail);

                if (_reader.IsAtEndOfFile || _reader.PeekOne() != '(')
                    ErrorAtCurrentLocation("Expected ( after 'if'");

                IExprValue expr = _exprParser.ParseExpr(_reader, _tokenReader);
                if (_controlFlowBlocks.Count > 0)
                {
                    int lastIndex = _controlFlowBlocks.Count - 1;
                    WindowControlFlowBlock lastBlock = _controlFlowBlocks[lastIndex];

                    if (!lastBlock.IsBraced)
                    {
                        _controlFlowBlocks.RemoveAt(lastIndex);
                        expr = new ExpressionExprValue(lastBlock.Condition, expr, ExpressionValue.EOperator.And);
                    }
                }

                if (expr.ResultType != ExprResultType.Float)
                    throw new CompilerException(newCFB.StartLocTag, "Expression didn't evaluate to a number");

                newCFB.Condition = expr;
                newCFB.IsElse = false;

                _tokenReader.SkipWhitespace(_reader, EOLBehavior.Ignore);

                if (!_reader.IsAtEndOfFile && _reader.PeekOne() == '{')
                {
                    _reader.StepAhead(1);
                    newCFB.IsBraced = true;
                }

                _controlFlowBlocks.Add(newCFB);
                return;
            }

            if (directiveToken.Equals(_elseStr))
            {
                WindowControlFlowBlock newCFB = new WindowControlFlowBlock();
                newCFB.StartLocTag = _reader.LocationTag;
                newCFB.IsElse = true;

                if (_controlFlowBlocks.Count == 0)
                    throw new CompilerException(newCFB.StartLocTag, "'else' without matching 'if'");

                int lastIndex = _controlFlowBlocks.Count - 1;
                WindowControlFlowBlock existingCFB = _controlFlowBlocks[lastIndex];
                _controlFlowBlocks.RemoveAt(lastIndex);

                if (existingCFB.IsElse)
                    throw new CompilerException(newCFB.StartLocTag, "'else' after 'else'");

                _tokenReader.SkipWhitespace(_reader, EOLBehavior.Ignore);
                if (!_reader.IsAtEndOfFile && _reader.PeekOne() == '{')
                {
                    _reader.StepAhead(1);
                    newCFB.IsBraced = true;
                }

                newCFB.Condition = Utils.InvertCondition(existingCFB.Condition);
                return;
            }

            bool isInCondition = (_controlFlowBlocks.Count > 0);
            if (directiveToken.Equals(_titleStr))
                CompileTitleDirective(isInCondition);
            else if (directiveToken.Equals(_talkStr))
                CompileTalkDirective(isInCondition);
            else if (directiveToken.Equals(_talkExStr))
                CompileTalkExDirective(isInCondition);
            else if (directiveToken.Equals(_widthStr))
                CompileDimensionDirective(isInCondition, ref _width);
            else if (directiveToken.Equals(_heightStr))
                CompileDimensionDirective(isInCondition, ref _height);
            else if (directiveToken.Equals(_xposStr))
                CompileDimensionDirective(isInCondition, ref _xpos);
            else if (directiveToken.Equals(_yposStr))
                CompileDimensionDirective(isInCondition, ref _ypos);
            else if (directiveToken.Equals(_imageStr))
                CompileImageDirective(isInCondition);
            else if (directiveToken.Equals(_flagsStr))
                CompileFlagsDirective(isInCondition);
            else if (directiveToken.Equals(_subWindowStr))
                CompileSubWindowDirective(isInCondition);
            else if (directiveToken.Equals(_choiceStr))
                CompileChoiceDirective(isInCondition);
            else if (directiveToken.Equals(_startConsoleStr))
                CompileConsoleDirective(isInCondition, ref _startConsoleCommand);
            else if (directiveToken.Equals(_finishConsoleStr))
                CompileConsoleDirective(isInCondition, ref _finishConsoleCommand);
            else if (directiveToken.Equals(_fontStr))
                CompileFontDirective(isInCondition);
            else if (directiveToken.Equals(_styleStr))
                CompileStyleDirective(isInCondition);
            else if (directiveToken.Equals(_gotoStr) || directiveToken.Equals(_nextWindowStr))
                CompileNextWindowDirective(isInCondition);
            else if (directiveToken.Equals(_returnStr))
                CompileReturnDirective(isInCondition);
            else if (directiveToken.Equals(_xyPrintStr))
                CompileXYPrintDirective(isInCondition);
            else if (directiveToken.Equals(_xyPrintFXStr))
                CompileXYPrintFXDirective(isInCondition);
            else if (directiveToken.Equals(_startSwitchStr))
                CompileSwitchDirective(isInCondition, ref _startSwitchCommand);
            else if (directiveToken.Equals(_finishSwitchStr))
                CompileSwitchDirective(isInCondition, ref _finishSwitchCommand);
            else if (directiveToken.Equals(_thinkSwitchStr))
                CompileSwitchDirective(isInCondition, ref _thinkSwitchCommand);
            else if (directiveToken.Equals(_bodyStr))
                CompileBodyDirective(isInCondition);
            else if (directiveToken.Equals(_backgroundStr))
                CompileBodyDirective(isInCondition);
            else if (directiveToken.Equals(_camStr))
                CompileCamDirective(isInCondition);
            else
                ErrorAtCurrentLocation("Unknown directive");

            _tokenReader.SkipWhitespace(_reader, EOLBehavior.Expect);

            if (_controlFlowBlocks.Count > 0)
            {
                WindowControlFlowBlock lastCFB = _controlFlowBlocks[_controlFlowBlocks.Count - 1];

                if (!lastCFB.IsBraced)
                    _controlFlowBlocks.RemoveAt(_controlFlowBlocks.Count - 1);
            }
        }

        private void CompileCamDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileBodyDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileSwitchDirective(bool isInCondition, ref WindowSwitchCommand? startSwitchCommand)
        {
            throw new NotImplementedException();
        }

        private void CompileXYPrintFXDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileXYPrintDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileReturnDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileNextWindowDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileStyleDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileFontDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileConsoleDirective(bool isInCondition, ref SimpleStringCommand? startConsoleCommand)
        {
            throw new NotImplementedException();
        }

        private void CompileChoiceDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileSubWindowDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileFlagsDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileImageDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileDimensionDirective(bool isInCondition, ref OptionalExpression width)
        {
            throw new NotImplementedException();
        }

        private void CompileTalkExDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileTalkDirective(bool isInCondition)
        {
            throw new NotImplementedException();
        }

        private void CompileTitleDirective(bool isInCondition)
        {
            throw new NotImplementedException();
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

            if (_backgroundCommand.Color1 != 0 || _backgroundCommand.Color2 != 0 || _backgroundCommand.Color3 != 0 || _backgroundCommand.Color4 != 0)
                windowCommands.Add(_backgroundCommand);

            AddOptional(windowCommands, new DimensionsCommand(_xpos, _ypos, _width, _height));

            windowCommands.AddRange(_subWindowCommands);
            windowCommands.AddRange(_imageCommands);
            windowCommands.AddRange(_choiceCommands);

            AddOptional(windowCommands, _nextWindowCommand);

            return windowCommands;
        }

        public Window Compile()
        {
            _tokenReader.SkipWhitespace(_reader, EOLBehavior.Fail);

            TokenReadBehavior punctuationAllowedTRB = new TokenReadBehavior();
            punctuationAllowedTRB.NewLinesInStringAllowed = false;
            punctuationAllowedTRB.PunctuationAllowed = true;
            punctuationAllowedTRB.QuotedStringAllowed = false;
            punctuationAllowedTRB.NonStringAllowed = true;

            ILogger.LocationTag labelLoc = _reader.LocationTag;
            ByteStringSlice labelToken = _tokenReader.ReadToken(_reader, punctuationAllowedTRB);

            uint label = Utils.ParseLabel(labelToken, labelLoc);


            _tokenReader.SkipWhitespace(_reader, EOLBehavior.Expect);

            while (true)
            {
                if (_reader.IsAtEndOfFile)
                    break;

                if (_reader.PeekOne() == '#')
                {
                    if (_controlFlowBlocks.Count != 0)
                        throw new CompilerException(_controlFlowBlocks[_controlFlowBlocks.Count - 1].StartLocTag, "Control flow statement wasn't closed");
                    break;
                }

                CompileWindowDirective();
            }

            if (!_hasBackground)
                _flagsCommand.SetFlag(FlagsCommand.FlagBit.NoBackground);

            return new Window(label, new WindowCommandList(CollateCommands()));
        }
    }
}
