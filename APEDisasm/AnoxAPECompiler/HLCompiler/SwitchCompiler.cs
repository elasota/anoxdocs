using AnoxAPE;
using AnoxAPE.Elements;

namespace AnoxAPECompiler.HLCompiler
{
    internal class SwitchCompiler
    {
        private TokenReader2 _reader;
        private ExprParser _exprParser;
        private ExprConverter _exprConverter;
        private SwitchStmtTree? _firstTopLevelStmt;
        private SwitchStmtTree? _lastTopLevelStmt;
        private CompilerOptions _options;

        private static ByteStringSlice _defineStr = ByteString.FromAsciiString("define").ToSlice();

        private static ByteStringSlice _ifStr = ByteString.FromAsciiString("if").ToSlice();
        private static ByteStringSlice _elseStr = ByteString.FromAsciiString("else").ToSlice();
        private static ByteStringSlice _whileStr = ByteString.FromAsciiString("while").ToSlice();
        private static ByteStringSlice _setStr = ByteString.FromAsciiString("set").ToSlice();
        private static ByteStringSlice _unsetStr = ByteString.FromAsciiString("unset").ToSlice();
        private static ByteStringSlice _gotoStr = ByteString.FromAsciiString("goto").ToSlice();
        private static ByteStringSlice _returnStr = ByteString.FromAsciiString("return").ToSlice();
        private static ByteStringSlice _gosubStr = ByteString.FromAsciiString("gosub").ToSlice();
        private static ByteStringSlice _consoleStr = ByteString.FromAsciiString("console").ToSlice();
        private static ByteStringSlice _echoStr = ByteString.FromAsciiString("echo").ToSlice();
        private static ByteStringSlice _targetStr = ByteString.FromAsciiString("target").ToSlice();
        private static ByteStringSlice _pathTargetStr = ByteString.FromAsciiString("pathtarget").ToSlice();
        private static ByteStringSlice _externStr = ByteString.FromAsciiString("extern").ToSlice();
        private static ByteStringSlice _playAmbientStr = ByteString.FromAsciiString("playambient").ToSlice();
        private static ByteStringSlice _loopAmbientStr = ByteString.FromAsciiString("loopambient").ToSlice();
        private static ByteStringSlice _stopAmbientStr = ByteString.FromAsciiString("stopambient").ToSlice();
        private static ByteStringSlice _playSceneStr = ByteString.FromAsciiString("playscene").ToSlice();
        private static ByteStringSlice _loopSceneStr = ByteString.FromAsciiString("loopscene").ToSlice();
        private static ByteStringSlice _stopSceneStr = ByteString.FromAsciiString("stopscene").ToSlice();
        private static ByteStringSlice _chainScriptsStr = ByteString.FromAsciiString("chainscripts").ToSlice();
        private static ByteStringSlice _closeWindowStr = ByteString.FromAsciiString("closewindow").ToSlice();
        private static ByteStringSlice _loadApeStr = ByteString.FromAsciiString("loadape").ToSlice();
        private static ByteStringSlice _setFocusStr = ByteString.FromAsciiString("setfocus").ToSlice();

        private static ByteString _zeroZeroStr = ByteString.FromAsciiString("0:0");

        public SwitchCompiler(TokenReader2 reader, ExprParser exprParser, ExprConverter exprConverter, CompilerOptions options)
        {
            _reader = reader;
            _exprParser = exprParser;
            _exprConverter = exprConverter;
            _options = options;
        }

        private ulong NextCC(ulong cc, byte append, ILogger.LocationTag locTag)
        {
            if ((cc >> 62) != 0)
                throw new CompilerException(locTag, "Too many statements in switch");

            cc <<= 2;
            cc += append;

            return cc;
        }

        private void FlushExprTreeToCommands(ulong initialCC, IList<CCPrefixedCommand> cmdList, SwitchStmtTree? stmtTree, ILogger.LocationTag locTag)
        {
            while (stmtTree != null)
            {
                SwitchCommand.ECommandType cmdType = stmtTree.CmdType;
                SwitchStmtTree? trueTree = stmtTree.TrueTree;
                SwitchStmtTree? falseTree = stmtTree.FalseTree;
                IExprValue? expr = stmtTree.Expr;

                OptionalExpression convertedExpr = _exprConverter.ConvertValueToOptionalExpression(expr, locTag);

                cmdList.Add(new CCPrefixedCommand(initialCC, new SwitchCommand(cmdType, stmtTree.Str, stmtTree.FormattingValue, convertedExpr)));

                if (stmtTree.TrueTree != null)
                    FlushExprTreeToCommands(NextCC(initialCC, 1, locTag), cmdList, stmtTree.TrueTree, locTag);

                if (stmtTree.FalseTree != null)
                    FlushExprTreeToCommands(NextCC(initialCC, 2, locTag), cmdList, stmtTree.TrueTree, locTag);

                stmtTree = stmtTree.NextStmt;

                // Check this since NextCC may throw
                if (stmtTree != null)
                    initialCC = NextCC(initialCC, 3, locTag);
            }
        }

        private void AppendStatement(ref SwitchStmtTree? headStmt, ref SwitchStmtTree? tailStmt, SwitchStmtTree? stmt)
        {
            if (stmt == null)
                return;

            if (headStmt == null)
                headStmt = stmt;
            if (tailStmt != null)
                tailStmt.NextStmt = stmt;

            tailStmt = stmt;
        }

        private void AppendAllStatements(ref SwitchStmtTree? headStmt, ref SwitchStmtTree? tailStmt, SwitchStmtTree? stmt)
        {
            while (stmt != null)
            {
                AppendStatement(ref headStmt, ref tailStmt, stmt);
                stmt = stmt.NextStmt;
            }
        }

        private SwitchStmtTree? OptimizeExprTree(SwitchStmtTree? inStmt, ILogger.LocationTag locTag)
        {
            SwitchStmtTree? headStmt = null;
            SwitchStmtTree? tailStmt = null;

            while (inStmt != null)
            {
                SwitchCommand.ECommandType cmdType = inStmt.CmdType;

                if (cmdType == SwitchCommand.ECommandType.IfCommand)
                {
                    if (inStmt.Expr == null)
                        throw new Exception("Internal error: 'if' condition was null");

                    SwitchStmtTree? trueTree = null;
                    SwitchStmtTree? falseTree = null;

                    if (inStmt.TrueTree != null)
                        trueTree = OptimizeExprTree(inStmt.TrueTree, locTag);
                    if (inStmt.FalseTree != null)
                        falseTree = OptimizeExprTree(inStmt.FalseTree, locTag);

                    if (_options.Optimize)
                    {

                        if (inStmt.Expr.OperandType == ExpressionValue.EOperandType.FloatConst)
                        {
                            bool isTrue = (((FloatConstExprValue)inStmt.Expr).Value != 0.0f);

                            if (isTrue)
                                AppendAllStatements(ref headStmt, ref tailStmt, trueTree);
                            else
                                AppendAllStatements(ref headStmt, ref tailStmt, falseTree);

                            inStmt = inStmt.NextStmt;
                            continue;
                        }
                    }

                    // Non-constant condition
                    if (trueTree == null)
                    {
                        // No true statements
                        if (falseTree == null)
                        {
                            // No false statements
                            // Drop the condition
                        }
                        else
                        {
                            // Has false statements
                            IExprValue invertedCondition = Utils.InvertCondition(inStmt.Expr);
                            if (invertedCondition.ResultType != ExprResultType.Float)
                                throw new CompilerException(locTag, "Conditional 'if' block with no true statements was not invertible");

                            SwitchStmtTree ifStmt = new SwitchStmtTree(cmdType, inStmt.Str, inStmt.FormattingValue, invertedCondition);
                            ifStmt.TrueTree = falseTree;
                            AppendStatement(ref headStmt, ref tailStmt, ifStmt);
                        }
                    }
                    else
                    {
                        // Has true statements
                        SwitchStmtTree ifStmt = new SwitchStmtTree(cmdType, inStmt.Str, inStmt.FormattingValue, inStmt.Expr);
                        ifStmt.TrueTree = trueTree;
                        ifStmt.FalseTree = falseTree;

                        AppendStatement(ref headStmt, ref tailStmt, ifStmt);
                    }
                }
                else if (cmdType == SwitchCommand.ECommandType.WhileCommand)
                {
                    SwitchStmtTree? whileStmt = new SwitchStmtTree(cmdType, inStmt.Str, inStmt.FormattingValue, inStmt.Expr);
                    whileStmt.TrueTree = OptimizeExprTree(inStmt.TrueTree, locTag);

                    if (whileStmt.TrueTree == null)
                        throw new CompilerException(locTag, "'while' loop interior has no runnable statements");

                    AppendStatement(ref headStmt, ref tailStmt, whileStmt);
                }
                else
                {
                    SwitchStmtTree? copiedStmt = new SwitchStmtTree(cmdType, inStmt.Str, inStmt.FormattingValue, inStmt.Expr);
                    AppendStatement(ref headStmt, ref tailStmt, copiedStmt);
                }

                inStmt = inStmt.NextStmt;
            }

            return headStmt;
        }

        private IEnumerable<CCPrefixedCommand> FlushExprTree(ILogger.LocationTag locTag)
        {
            SwitchStmtTree? tree = _firstTopLevelStmt;

            tree = OptimizeExprTree(tree, locTag);

            List<CCPrefixedCommand> cmds = new List<CCPrefixedCommand>();

            if (tree == null)
                cmds.Add(new CCPrefixedCommand(1, new SwitchCommand(SwitchCommand.ECommandType.NoOpCommand, new OptionalString(), new FormattingValue(), new OptionalExpression())));
            else
                FlushExprTreeToCommands(1, cmds, tree, locTag);

            return cmds;
        }

        private SwitchStmtTree CompileExternDirective()
        {
            Token tok = _reader.ReadToken(TokenReadMode.UnquotedString,
                TokenReadProperties.Default
                .Add(TokenReadProperties.Flag.TerminateQuotesOnNewLine)
                .Add(TokenReadProperties.Flag.IgnoreWhitespace)
                .Add(TokenReadProperties.Flag.IgnoreEscapes)
                .Add(TokenReadProperties.Flag.IgnoreQuotes));

            return new SwitchStmtTree(SwitchCommand.ECommandType.ExternCommand, new OptionalString(tok.Value.ToByteString()), new FormattingValue(), null);
        }

        private SwitchStmtTree CompileGotoDirective()
        {
            if (_options.DParseGotoHandling)
            {
                if (!_options.DParseCommentHandling)
                    throw new ArgumentException("DParseGotoHandling option was set without DParseCommentHandling");

                Token tok = _reader.ReadToken(TokenReadMode.UnquotedString,
                    TokenReadProperties.Default
                    .Add(TokenReadProperties.Flag.TerminateQuotesOnNewLine)
                    .Add(TokenReadProperties.Flag.IgnoreWhitespace)
                    .Add(TokenReadProperties.Flag.IgnoreEscapes)
                    .Add(TokenReadProperties.Flag.IgnoreQuotes));

                return new SwitchStmtTree(SwitchCommand.ECommandType.GotoCommand, new OptionalString(tok.Value.ToByteString()), new FormattingValue(), null);
            }
            else
                return CompileSimpleLabeledDirective(SwitchCommand.ECommandType.GotoCommand);
        }

        private SwitchStmtTree CompileSimpleLabeledDirective(SwitchCommand.ECommandType cmdType)
        {
            uint label = _exprParser.ParseLabel(_reader);

            uint labelLow = label % 10000;
            uint labelHigh = label / 10000;

            byte[] asciiLabelString = System.Text.Encoding.ASCII.GetBytes($"{labelHigh}:{labelLow}");
            return new SwitchStmtTree(SwitchCommand.ECommandType.GotoCommand, new OptionalString(new ByteString(asciiLabelString)), new FormattingValue(), null);
        }

        private SwitchStmtTree CompileSimpleFormattableStringDirective(SwitchCommand.ECommandType cmdType)
        {
            Token str = _reader.ReadToken(TokenReadMode.QuotedString);

            ByteStringSlice escaped = Utils.EscapeSlice(str.Value.SubSlice(1, str.Value.Length - 1), str.Location, true, false);

            FormattingValue fmt = _exprParser.ParseOptionalFormattingValueList(_reader);

            return new SwitchStmtTree(cmdType, new OptionalString(str.Value.ToByteString()), fmt, null);
        }

        private SwitchStmtTree CompileSetDirective(Token destTok)
        {
            _reader.ExpectToken(TokenReadMode.Normal, TokenType.Assign);

            if (destTok.Value[destTok.Value.Length - 1] == '$')
                return CompileSetStringDirective(destTok);
            else
                return CompileSetFloatDirective(destTok);
        }

        private SwitchStmtTree? CompileBracedStatementBlock()
        {
            SwitchStmtTree? firstStmt = null;
            SwitchStmtTree? lastStmt = null;

            while (true)
            {
                Token tok = _reader.PeekToken(TokenReadMode.Normal);

                if (tok.TokenType == TokenType.CloseBrace)
                {
                    _reader.ConsumeToken();
                    break;
                }

                if (tok.TokenType == TokenType.EndOfLine)
                {
                    _reader.SkipEndOfLines(TokenReadMode.Normal);
                    continue;
                }

                tok = _reader.ExpectToken(TokenReadMode.Normal, TokenType.Identifier);

                SwitchStmtTree stmt = CompileSwitchDirective(tok);
                if (firstStmt == null)
                    firstStmt = stmt;

                if (lastStmt != null)
                    lastStmt.NextStmt = stmt;

                lastStmt = stmt;
            }

            return firstStmt;
        }

        private SwitchStmtTree CompileIfDirective(out bool needsNewLineCheck)
        {
            needsNewLineCheck = true;

            ILogger.LocationTag locTag = _reader.Location;
            _reader.ExpectToken(TokenReadMode.Normal, TokenType.OpenParen);
            IExprValue condition = _exprParser.ParseExprPreferFloat(_reader);

            if (condition.ResultType != ExprResultType.Float && !_options.AllowMalformedExprs)
                throw new CompilerException(locTag, "Expression didn't evaluate to a number");

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.CloseParen);

            _reader.SkipEndOfLines(TokenReadMode.Normal);

            SwitchStmtTree? trueBlock = null;
            SwitchStmtTree? falseBlock = null;

            Token directiveTok = _reader.ReadToken(TokenReadMode.Normal);
            if (directiveTok.TokenType == TokenType.OpenBrace)
            {
                _reader.SkipEndOfLines(TokenReadMode.Normal);

                trueBlock = CompileBracedStatementBlock();
            }
            else
                trueBlock = CompileSwitchDirective(directiveTok);

            bool hasEOLAfterTruePart = (_reader.PeekToken(TokenReadMode.Normal).TokenType == TokenType.EndOfLine);

            _reader.SkipEndOfLines(TokenReadMode.Normal);
            Token possibleElseTok = _reader.PeekToken(TokenReadMode.Normal);

            if (possibleElseTok.TokenType == TokenType.Identifier && possibleElseTok.Value.Equals(_elseStr))
            {
                _reader.ConsumeToken();
                _reader.SkipEndOfLines(TokenReadMode.Normal);

                directiveTok = _reader.ReadToken(TokenReadMode.Normal);
                if (directiveTok.TokenType == TokenType.OpenBrace)
                {
                    _reader.SkipEndOfLines(TokenReadMode.Normal);

                    falseBlock = CompileBracedStatementBlock();
                }
                else
                    falseBlock = CompileSwitchDirective(directiveTok);
            }
            else
                needsNewLineCheck = !hasEOLAfterTruePart;

            SwitchStmtTree stmt = new SwitchStmtTree(SwitchCommand.ECommandType.IfCommand, new OptionalString(), new FormattingValue(), condition);
            stmt.TrueTree = trueBlock;
            stmt.FalseTree = falseBlock;

            return stmt;
        }

        private SwitchStmtTree CompileSwitchDirective(Token tok)
        {
            bool needsNewLineCheck;
            SwitchStmtTree stmt = CompileSwitchDirectiveWithoutNewLine(tok, out needsNewLineCheck);

            if (needsNewLineCheck)
            {
                Token eolEofTok = _reader.PeekToken(TokenReadMode.Normal);
                if (eolEofTok.TokenType != TokenType.EndOfLine || eolEofTok.TokenType != TokenType.EndOfLine)
                    throw new CompilerException(eolEofTok.Location, "Expected end of line after switch directive");
            }

            return stmt;
        }

        private SwitchStmtTree CompileSwitchDirectiveWithoutNewLine(Token tok, out bool needsNewLineCheck)
        {
            needsNewLineCheck = true;

            if (tok.TokenType != TokenType.Identifier)
                throw new ArgumentException("CompileSwitchDirective token was not an identifier");

            // This function is called after the first token is already consumed
            ByteStringSlice directiveName = tok.Value;

            if (directiveName.Equals(_ifStr))
                return CompileIfDirective(out needsNewLineCheck);
            else if (directiveName.Equals(_whileStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_setStr))
            {
                Token dest = _reader.ExpectToken(TokenReadMode.Normal, TokenType.Identifier);
                return CompileSetDirective(dest);
            }
            else if (directiveName.Equals(_unsetStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_gotoStr))
                return CompileGotoDirective();
            else if (directiveName.Equals(_returnStr))
                return new SwitchStmtTree(SwitchCommand.ECommandType.GotoCommand, new OptionalString(_zeroZeroStr), new FormattingValue(), null);
            else if (directiveName.Equals(_gosubStr))
                return CompileSimpleLabeledDirective(SwitchCommand.ECommandType.GoSubCommand);
            else if (directiveName.Equals(_consoleStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_echoStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_targetStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_pathTargetStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_externStr))
                return CompileExternDirective();
            else if (directiveName.Equals(_playAmbientStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_loopAmbientStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_stopAmbientStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_playSceneStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_loopSceneStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_stopSceneStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_chainScriptsStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_closeWindowStr))
                return CompileSimpleLabeledDirective(SwitchCommand.ECommandType.CloseWindowCommand);
            else if (directiveName.Equals(_loadApeStr))
                throw new NotImplementedException();
            else if (directiveName.Equals(_setFocusStr))
                throw new NotImplementedException();
            else
                return CompileSetDirective(tok);
        }

        private SwitchStmtTree CompileSetFloatDirective(Token destTok)
        {
            ILogger.LocationTag locTag = _reader.Location;

            IExprValue expr = _exprParser.ParseExpr(_reader);

            return new SwitchStmtTree(SwitchCommand.ECommandType.SetFloatCommand, new OptionalString(destTok.Value.ToByteString()), new FormattingValue(), expr);
        }

        private SwitchStmtTree CompileSetStringDirective(Token destTok)
        {
            throw new NotImplementedException();
        }

        public Switch Compile(uint label, bool terminateOnCloseBrace, out bool isImmediatelyAfterTLD)
        {
            ILogger.LocationTag locTag = _reader.Location;

            isImmediatelyAfterTLD = false;
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
                {
                    if (terminateOnCloseBrace)
                        throw new CompilerException(locTag, "Expected close brace to close switch command block");

                    break;
                }

                if (tok.TokenType == TokenType.CloseBrace && terminateOnCloseBrace)
                    break;

                if (tok.TokenType == TokenType.EndOfLine)
                {
                    _reader.ConsumeToken();
                    continue;
                }

                tok = _reader.ExpectToken(TokenReadMode.Normal, TokenType.Identifier);
                SwitchStmtTree stmt = CompileSwitchDirective(tok);

                if (_firstTopLevelStmt == null)
                    _firstTopLevelStmt = stmt;

                if (_lastTopLevelStmt != null)
                    _lastTopLevelStmt.NextStmt = stmt;

                _lastTopLevelStmt = stmt;
            }

            return new Switch(label, new SwitchCommandList(FlushExprTree(locTag)));
        }
    }
}
