using AnoxAPE;
using AnoxAPE.Elements;
using System.Collections.Generic;
using System.Runtime.Intrinsics.X86;

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
                    FlushExprTreeToCommands(NextCC(initialCC, 2, locTag), cmdList, stmtTree.FalseTree, locTag);

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

        private SwitchStmtTree CompileSimpleLabeledDirective(SwitchCommand.ECommandType cmdType)
        {
            if (_options.DParseLabeledCommandHandling)
            {
                if (!_options.DParseCommentHandling)
                    throw new ArgumentException("DParseLabeledCommandHandling option was set without DParseCommentHandling");

                Token tok = _reader.ReadToken(TokenReadMode.UnquotedString,
                    TokenReadProperties.Default
                    .Add(TokenReadProperties.Flag.TerminateQuotesOnNewLine)
                    .Add(TokenReadProperties.Flag.IgnoreWhitespace)
                    .Add(TokenReadProperties.Flag.IgnoreEscapes)
                    .Add(TokenReadProperties.Flag.IgnoreQuotes));

                return new SwitchStmtTree(cmdType, new OptionalString(tok.Value.ToByteString()), new FormattingValue(), null);
            }
            else
            {
                uint label = _exprParser.ParseLabel(_reader);

                uint labelLow = label % 10000;
                uint labelHigh = label / 10000;

                byte[] asciiLabelString = System.Text.Encoding.ASCII.GetBytes($"{labelHigh}:{labelLow}");
                return new SwitchStmtTree(cmdType, new OptionalString(new ByteString(asciiLabelString)), new FormattingValue(), null);
            }
        }

        private SwitchStmtTree CompileSimpleOptionallyQuotedNameDirective(SwitchCommand.ECommandType cmdType)
        {
            ByteStringSlice slice = _exprParser.ReadOptionallyQuotedName(_reader, _options.Logger);

            return new SwitchStmtTree(cmdType, new OptionalString(slice.ToByteString()), new FormattingValue(), null);
        }

        private SwitchStmtTree CompileSimpleFormattableStringDirective(SwitchCommand.ECommandType cmdType)
        {
            Token str = _reader.ReadToken(TokenReadMode.QuotedString);

            ByteStringSlice escaped = Utils.EscapeSlice(str.Value.SubSlice(1, str.Value.Length - 2), str.Location, true, false);

            FormattingValue fmt = _exprParser.ParseOptionalFormattingValueList(_reader);

            return new SwitchStmtTree(cmdType, new OptionalString(escaped.ToByteString()), fmt, null);
        }

        private SwitchStmtTree CompileSetDirective(Token destTok)
        {
            ByteStringSlice destName = destTok.Value;
            if (_options.DParseSetVariableNameHandling)
            {
                Token nextTok = _reader.PeekToken(TokenReadMode.Normal);

                if (nextTok.TokenType == TokenType.NumericLiteral)
                {
                    _reader.ConsumeToken();

                    if (_options.Logger != null)
                        _options.Logger.WriteLine(new ILogger.MessageProperties(ILogger.Severity.Warning, nextTok.Location), "Numeric literal after a set variable name, this is allowed by compatibility options but is most likely a bug");

                    List<byte> rebuiltVarName = new List<byte>();
                    rebuiltVarName.AddRange(destName);
                    rebuiltVarName.Add((byte)' ');
                    rebuiltVarName.AddRange(nextTok.Value);

                    destName = (new ByteString(rebuiltVarName.ToArray())).ToSlice();
                }
            }

            if (destName[destName.Length - 1] == '$')
                return CompileSetStringDirective(destName);
            else
                return CompileSetFloatDirective(destName);
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

        private SwitchStmtTree CompileWhileDirective(out bool needsNewLineCheck)
        {
            ILogger.LocationTag locTag = _reader.Location;
            _reader.ExpectToken(TokenReadMode.Normal, TokenType.OpenParen);
            IExprValue condition = _exprParser.ParseExprPreferFloat(_reader);

            if (condition.ResultType != ExprResultType.Float && !_options.AllowMalformedExprs)
                throw new CompilerException(locTag, "Expression didn't evaluate to a number");

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.CloseParen);

            _reader.SkipEndOfLines(TokenReadMode.Normal);

            SwitchStmtTree? internalBlock = null;

            Token directiveTok = _reader.ReadToken(TokenReadMode.Normal);
            if (directiveTok.TokenType == TokenType.OpenBrace)
            {
                _reader.SkipEndOfLines(TokenReadMode.Normal);

                internalBlock = CompileBracedStatementBlock();

                needsNewLineCheck = true;
            }
            else
                internalBlock = CompileSwitchDirectiveWithoutNewLine(directiveTok, out needsNewLineCheck);

            SwitchStmtTree stmt = new SwitchStmtTree(SwitchCommand.ECommandType.WhileCommand, new OptionalString(), new FormattingValue(), condition);
            stmt.TrueTree = internalBlock;

            return stmt;
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
                    falseBlock = CompileSwitchDirectiveWithoutNewLine(directiveTok, out needsNewLineCheck);
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
                if (eolEofTok.TokenType != TokenType.EndOfLine && eolEofTok.TokenType != TokenType.EndOfFile)
                    throw new CompilerException(eolEofTok.Location, "Expected end of line after switch directive");
            }

            return stmt;
        }

        private SwitchStmtTree CompileUnsetDirective()
        {
            Token varName = _reader.ExpectToken(TokenReadMode.Normal, TokenType.Identifier);

            if (varName.Value[varName.Value.Length - 1] == '$')
                return new SwitchStmtTree(SwitchCommand.ECommandType.SetStringCommand, new OptionalString(varName.Value.ToByteString()), new FormattingValue(), null);
            else
                return new SwitchStmtTree(SwitchCommand.ECommandType.SetFloatCommand, new OptionalString(varName.Value.ToByteString()), new FormattingValue(), null);
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
                return CompileWhileDirective(out needsNewLineCheck);
            else if (directiveName.Equals(_setStr))
            {
                Token dest = _reader.ExpectToken(TokenReadMode.Normal, TokenType.Identifier);
                return CompileSetDirective(dest);
            }
            else if (directiveName.Equals(_unsetStr))
                return CompileUnsetDirective();
            else if (directiveName.Equals(_gotoStr))
                return CompileSimpleLabeledDirective(SwitchCommand.ECommandType.GotoCommand);
            else if (directiveName.Equals(_returnStr))
                return new SwitchStmtTree(SwitchCommand.ECommandType.GotoCommand, new OptionalString(_zeroZeroStr), new FormattingValue(), null);
            else if (directiveName.Equals(_gosubStr))
                return CompileSimpleLabeledDirective(SwitchCommand.ECommandType.GoSubCommand);
            else if (directiveName.Equals(_consoleStr))
                return CompileSimpleFormattableStringDirective(SwitchCommand.ECommandType.ConsoleCommand);
            else if (directiveName.Equals(_echoStr))
                return CompileSimpleFormattableStringDirective(SwitchCommand.ECommandType.EchoCommand);
            else if (directiveName.Equals(_targetStr))
                return CompileSimpleOptionallyQuotedNameDirective(SwitchCommand.ECommandType.TargetCommand);
            else if (directiveName.Equals(_pathTargetStr))
                return CompileSimpleOptionallyQuotedNameDirective(SwitchCommand.ECommandType.PathTargetCommand);
            else if (directiveName.Equals(_externStr))
                return CompileExternDirective();
            else if (directiveName.Equals(_playAmbientStr))
                return CompileSimpleOptionallyQuotedNameDirective(SwitchCommand.ECommandType.PlayAmbientCommand);
            else if (directiveName.Equals(_loopAmbientStr))
                return CompileSimpleOptionallyQuotedNameDirective(SwitchCommand.ECommandType.LoopAmbientCommand);
            else if (directiveName.Equals(_stopAmbientStr))
                return CompileSimpleOptionallyQuotedNameDirective(SwitchCommand.ECommandType.StopAmbientCommand);
            else if (directiveName.Equals(_playSceneStr))
                return CompileSimpleOptionallyQuotedNameDirective(SwitchCommand.ECommandType.PlaySceneCommand);
            else if (directiveName.Equals(_loopSceneStr))
                return CompileSimpleOptionallyQuotedNameDirective(SwitchCommand.ECommandType.LoopSceneCommand);
            else if (directiveName.Equals(_stopSceneStr))
                return CompileSimpleOptionallyQuotedNameDirective(SwitchCommand.ECommandType.StopSceneCommand);
            else if (directiveName.Equals(_chainScriptsStr))
                return CompileSimpleOptionallyQuotedNameDirective(SwitchCommand.ECommandType.ChainScriptsCommand);
            else if (directiveName.Equals(_closeWindowStr))
                return CompileSimpleLabeledDirective(SwitchCommand.ECommandType.CloseWindowCommand);
            else if (directiveName.Equals(_loadApeStr))
                return CompileSimpleFormattableStringDirective(SwitchCommand.ECommandType.LoadAPECommand);
            else if (directiveName.Equals(_setFocusStr))
                throw new CompilerException(tok.Location, "setfocus command is not supported");
            else
                return CompileSetDirective(tok);
        }

        private SwitchStmtTree CompileSetFloatDirective(ByteStringSlice destName)
        {
            ILogger.LocationTag locTag = _reader.Location;

            Token paramTok = _reader.PeekToken(TokenReadMode.Normal);
            if (paramTok.TokenType == TokenType.OpenSquareBracket)
            {
                ByteStringSlice paramStr = _exprParser.ParseFunctionCallParameters(_reader);

                List<byte> combinedName = new List<byte>();
                combinedName.AddRange(destName);
                combinedName.AddRange(paramStr);

                destName = (new ByteString(combinedName.ToArray()).ToSlice());
            }

            _reader.ExpectToken(TokenReadMode.Normal, TokenType.AssignmentOperator);

            IExprValue expr = _exprParser.ParseExpr(_reader);

            return new SwitchStmtTree(SwitchCommand.ECommandType.SetFloatCommand, new OptionalString(destName.ToByteString()), new FormattingValue(), expr);
        }

        private SwitchStmtTree CompileSetStringDirective(ByteStringSlice destName)
        {
            _reader.ExpectToken(TokenReadMode.Normal, TokenType.AssignmentOperator);

            Token srcTok = _reader.ReadToken(TokenReadMode.Normal);

            if (srcTok.TokenType == TokenType.StringLiteral)
            {
                ByteStringSlice unescapedSlice = srcTok.Value.SubSlice(1, srcTok.Value.Length - 2);
                ByteStringSlice escapedSlice = Utils.EscapeSlice(unescapedSlice, srcTok.Location, true, false);

                List<byte> combined = new List<byte>();
                combined.AddRange(destName);
                combined.Add((byte)'=');
                combined.Add((byte)'\"');
                combined.AddRange(escapedSlice);
                combined.Add((byte)'\"');

                FormattingValue fmt = _exprParser.ParseOptionalFormattingValueList(_reader);

                return new SwitchStmtTree(SwitchCommand.ECommandType.SetStringCommand, new OptionalString(new ByteString(combined.ToArray())), fmt, null);
            }
            else if (srcTok.TokenType == TokenType.Identifier)
            {
                if (srcTok.Value[srcTok.Value.Length - 1] != '$')
                    throw new CompilerException(srcTok.Location, "String set source must be a string variable");

                List<byte> combined = new List<byte>();
                combined.AddRange(destName);
                combined.Add((byte)'=');
                combined.AddRange(srcTok.Value);

                return new SwitchStmtTree(SwitchCommand.ECommandType.SetStringCommand, new OptionalString(new ByteString(combined.ToArray())), new FormattingValue(), null);
            }
            else
                throw new CompilerException(srcTok.Location, "String set source wasn't a string variable or string literal");
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
