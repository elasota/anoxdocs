using AnoxAPE.Elements;
using System.Globalization;

namespace AnoxAPE
{
    public class Decompiler
    {
        private HashSet<uint> _inlinedSwitches;
        private Dictionary<uint, Switch> _idToSwitch;
        private APEFile _apeFile;
        private ByteString _talkPlayerChar0ConstStr;
        private ByteString _talkClickConstStr;
        private Switch? _lastExplicitSwitch;
        private Switch? _lastSwitchInFile;

        private class WindowSwitchCommandComparer : IComparer<WindowSwitchCommand>
        {
            public int Compare(WindowSwitchCommand? x, WindowSwitchCommand? y)
            {
                if (x == null)
                {
                    if (y == null)
                        return 0;
                    else
                        return -1;
                }

                if (y == null)
                    return 1;

                uint xLabel = x.Label;
                uint yLabel = y.Label;

                return xLabel.CompareTo(yLabel);
            }
        }

        public Decompiler()
        {
            _inlinedSwitches = new HashSet<uint>();
            _idToSwitch = new Dictionary<uint, Switch>();
            _apeFile = new APEFile();
            _talkPlayerChar0ConstStr = ByteString.FromAsciiString("playerchar0");
            _talkClickConstStr = ByteString.FromAsciiString("_click_");
        }

        public void Load(APEFile apeFile)
        {
            _apeFile = apeFile;

            foreach (Switch sw in _apeFile.RootElementList.Switches.SwitchList)
                _idToSwitch[sw.Label] = sw;
        }

        private static string IdToLabel(uint id)
        {
            return $"{id / 10000}:{id % 10000}";
        }

        private void DumpString(ByteString byteString, OutputStream outStream)
        {
            outStream.WriteBytes(byteString.Bytes);
        }

        private void DumpFloat(float f, OutputStream outStream)
        {
            string floatStr = f.ToString("R", CultureInfo.InvariantCulture);
            if (floatStr.Contains("E"))
            {
                floatStr = f.ToString("F", CultureInfo.InvariantCulture);

                if (floatStr.Contains('.'))
                {
                    while (floatStr.EndsWith('0'))
                        floatStr = floatStr.Substring(0, floatStr.Length - 1);

                    if (floatStr.EndsWith('.'))
                        floatStr = floatStr.Substring(0, floatStr.Length - 1);
                }
            }

            outStream.WriteString(floatStr);
        }

        private void DumpQuotedString(ByteString byteString, OutputStream outStream)
        {
            byte[] bytes = byteString.Bytes;

            int numRestrictedCharacters = 0;
            foreach (byte b in byteString.Bytes)
            {
                if (b == 10 || b == 92 || b == 34)
                    numRestrictedCharacters++;
            }

            if (numRestrictedCharacters > 0)
            {
                byte[] newBytes = new byte[bytes.Length + numRestrictedCharacters];
                int outOffset = 0;
                foreach (byte b in byteString.Bytes)
                {
                    if (b == 10)
                    {
                        // Escape \n
                        newBytes[outOffset++] = 92;
                        newBytes[outOffset++] = 110;
                    }
                    else if (b == 92)
                    {
                        // Escape \\
                        newBytes[outOffset++] = 92;
                        newBytes[outOffset++] = 92;
                    }
                    else if (b == 34)
                    {
                        // Escape \"
                        newBytes[outOffset++] = 92;
                        newBytes[outOffset++] = 34;
                    }
                    else
                        newBytes[outOffset++] = b;
                }

                bytes = newBytes;
            }

            byte[] quote = new byte[1];
            quote[0] = 34;
            outStream.WriteBytes(quote);
            outStream.WriteBytes(bytes);
            outStream.WriteBytes(quote);
        }

        private void DumpConditionPrefix(OptionalExpression condition, OutputStream outStream)
        {
            if (condition.Expression == null)
                return;

            outStream.WriteString("if (");
            DumpOptimizableExpression(condition.Expression, outStream);
            outStream.WriteLine(")");
            outStream.WriteString("\t");
        }

        private void DumpFormattingValue(FormattingValue value, OutputStream outStream)
        {
            foreach (TypedFormattingValue tfv in value.Values)
            {
                ExpressionValue.EOperandType opType = ExpressionValue.EOperandType.Expression;

                switch (tfv.Type)
                {
                    case TypedFormattingValue.EFormattingValueType.Float:
                        opType = ExpressionValue.EOperandType.FloatConst;
                        break;
                    case TypedFormattingValue.EFormattingValueType.String:
                        opType = ExpressionValue.EOperandType.StringConst;
                        break;
                    case TypedFormattingValue.EFormattingValueType.VariableName:
                        opType = ExpressionValue.EOperandType.FloatVar;
                        break;
                    case TypedFormattingValue.EFormattingValueType.StringVariableName:
                        opType = ExpressionValue.EOperandType.StringVar;
                        break;
                    default:
                        throw new Exception("Invalid formatting value type");
                }

                outStream.WriteString(", ");
                DumpExpressionOperand(opType, tfv.Value, outStream);
            }
        }

        private void DumpTitleCommand(TitleCommand command, OutputStream outStream)
        {
            DumpConditionPrefix(command.Condition, outStream);
            outStream.WriteString("title ");
            DumpQuotedString(command.Text, outStream);
            DumpFormattingValue(command.FormattingValue, outStream);
            outStream.WriteLine("");
        }

        private void DumpTalkCommand(TalkCommand command, OutputStream outStream)
        {
            // Syntaxes:
            // "talk player" -> Name1 == _click_, Name2 = playerchar0, nostay applies to Stay2
            // "talk npc" -> Name1 = playerchar0, Name2 = _click_, nostay applies to Stay2

            if (command.Animation2.Value != null)
            {
                outStream.WriteString("talk_ex ");
                DumpString(command.Name1, outStream);
                outStream.WriteString(" ");
                DumpString(command.Name2, outStream);
                outStream.WriteString(" ");
                DumpString(command.Animation1, outStream);
                outStream.WriteString(" ");
                DumpString(command.Animation2.Value, outStream);

                if (command.Stay1Flag == 0 || command.Stay2Flag == 0)
                {
                    if (command.Stay1Flag != 0)
                        outStream.WriteString(" stay");
                    else
                        outStream.WriteString(" nostay");

                    if (command.Stay2Flag != 0)
                        outStream.WriteString(" stay");
                    else
                        outStream.WriteString(" nostay");
                }
            }
            else
            {
                if (command.Name1.Equals(_talkClickConstStr) && command.Name2.Equals(_talkPlayerChar0ConstStr))
                    outStream.WriteString("talk npc ");
                else if (command.Name1.Equals(_talkPlayerChar0ConstStr) && command.Name2.Equals(_talkClickConstStr))
                    outStream.WriteString("talk player ");
                else
                    throw new Exception("Unhandled talk command configuration");

                DumpString(command.Animation1, outStream);

                if (command.Stay2Flag == 0)
                    outStream.WriteString(" nostay");
            }

            outStream.WriteLine("");
        }

        private void DumpUnoptimizedExpression(ExpressionValue expr, OutputStream outStream)
        {
            DumpExpressionOperand(expr.LeftOperandType, expr.Left, outStream);

            switch (expr.Operator)
            {
                case ExpressionValue.EOperator.Or:
                    outStream.WriteString(" || ");
                    break;
                case ExpressionValue.EOperator.And:
                    outStream.WriteString(" && ");
                    break;
                case ExpressionValue.EOperator.Xor:
                    outStream.WriteString(" ^^ ");
                    break;
                case ExpressionValue.EOperator.Gt:
                    outStream.WriteString(" > ");
                    break;
                case ExpressionValue.EOperator.Lt:
                    outStream.WriteString(" < ");
                    break;
                case ExpressionValue.EOperator.Ge:
                    outStream.WriteString(" >= ");
                    break;
                case ExpressionValue.EOperator.Le:
                    outStream.WriteString(" <= ");
                    break;
                case ExpressionValue.EOperator.Eq:
                    outStream.WriteString(" == ");
                    break;
                case ExpressionValue.EOperator.Neq:
                    outStream.WriteString(" != ");
                    break;
                case ExpressionValue.EOperator.Add:
                    outStream.WriteString(" + ");
                    break;
                case ExpressionValue.EOperator.Sub:
                    outStream.WriteString(" - ");
                    break;
                case ExpressionValue.EOperator.Mul:
                    outStream.WriteString(" * ");
                    break;
                case ExpressionValue.EOperator.Div:
                    outStream.WriteString(" / ");
                    break;

                default:
                    throw new Exception("Unknown operator");
            }

            DumpExpressionOperand(expr.RightOperandType, expr.Right, outStream);
        }

        private void DumpExpressionOperand(ExpressionValue.EOperandType opType, IExpressionOperand operand, OutputStream outStream)
        {
            switch (opType)
            {
                case ExpressionValue.EOperandType.Expression:
                    outStream.WriteString("(");
                    DumpUnoptimizedExpression(((ExpressionValueOperand)operand).Expression, outStream);
                    outStream.WriteString(")");
                    break;
                case ExpressionValue.EOperandType.FloatConst:
                    if (!(operand is FloatOperand))
                        throw new Exception("Internal error");

                    DumpFloat(((FloatOperand)operand).Value, outStream);
                    break;
                case ExpressionValue.EOperandType.FloatVar:
                case ExpressionValue.EOperandType.StringVar:
                    DumpString(((StringOperand)operand).Value, outStream);
                    break;
                case ExpressionValue.EOperandType.StringConst:
                    DumpQuotedString(((QuotedStringOperand)operand).Value, outStream);
                    break;

                default:
                    throw new Exception("Unknown operand type");
            }
        }

        private void DumpOptimizableExpression(ExpressionValue expr, OutputStream outStream)
        {
            if (expr.Operator == ExpressionValue.EOperator.Add && expr.LeftOperandType == ExpressionValue.EOperandType.FloatConst)
            {
                ExpressionValue.EOperandType rightOpType = expr.RightOperandType;

                if (rightOpType == ExpressionValue.EOperandType.FloatConst || rightOpType == ExpressionValue.EOperandType.FloatVar)
                {
                    float fv = ((FloatOperand)expr.Left).Value;

                    if (fv == 0.0f)
                    {
                        DumpExpressionOperand(expr.RightOperandType, expr.Right, outStream);
                        return;
                    }
                }
            }

            DumpUnoptimizedExpression(expr, outStream);
        }

        private void DumpDimensionsCommand(DimensionsCommand command, OutputStream outStream)
        {
            if (command.Width.Expression != null)
            {
                outStream.WriteString("width ");
                DumpOptimizableExpression(command.Width.Expression, outStream);
                outStream.WriteLine("");
            }
            if (command.Height.Expression != null)
            {
                outStream.WriteString("height ");
                DumpOptimizableExpression(command.Height.Expression, outStream);
                outStream.WriteLine("");
            }
            if (command.XPos.Expression != null)
            {
                outStream.WriteString("xpos ");
                DumpOptimizableExpression(command.XPos.Expression, outStream);
                outStream.WriteLine("");
            }
            if (command.YPos.Expression != null)
            {
                outStream.WriteString("ypos ");
                DumpOptimizableExpression(command.YPos.Expression, outStream);
                outStream.WriteLine("");
            }
        }

        private void DumpImageCommand(ImageCommand command, bool canDumpAsBackground, OutputStream outStream)
        {
            DumpConditionPrefix(command.Condition, outStream);

            if (canDumpAsBackground && command.CanEmitAsBackground())
            {
                outStream.WriteString("background ");
                DumpQuotedString(command.FileName, outStream);

                if (command.HasFlag(ImageCommand.EImageFlagBit.Stretch))
                    outStream.WriteString(" stretch");
                if (command.HasFlag(ImageCommand.EImageFlagBit.Tile))
                    outStream.WriteString(" tile");
            }
            else
            {
                if (command.XPos.Expression == null || command.YPos.Expression == null)
                    throw new Exception("Image command with no XPos or YPos");

                outStream.WriteString("image \"");
                DumpString(command.FileName, outStream);    // Intentionally not quoted, e.g. for bugaboo
                outStream.WriteString("\" ");
                DumpOptimizableExpression(command.XPos.Expression, outStream);
                outStream.WriteString(", ");
                DumpOptimizableExpression(command.YPos.Expression, outStream);

                if (command.Width.Expression != null)
                {
                    outStream.WriteString(", ");
                    DumpOptimizableExpression(command.Width.Expression, outStream);
                }

                if (command.Height.Expression != null)
                {
                    outStream.WriteString(", ");
                    DumpOptimizableExpression(command.Height.Expression, outStream);
                }

                if (command.HasFlag(ImageCommand.EImageFlagBit.Stretch))
                    outStream.WriteString(", stretch");
                if (command.HasFlag(ImageCommand.EImageFlagBit.Tile))
                    outStream.WriteString(", tile");
                if (command.HasFlag(ImageCommand.EImageFlagBit.Solid))
                    outStream.WriteString(", solid");
            }

            outStream.WriteLine("");
        }

        private void DumpFlagsCommand(FlagsCommand command, OutputStream outStream)
        {
            // NoBackground intentionally ignored
            if (command.HasFlag(FlagsCommand.FlagBit.Persist))
                outStream.WriteLine("flags persist");
            if (command.HasFlag(FlagsCommand.FlagBit.NoScroll))
                outStream.WriteLine("flags noscroll");
            if (command.HasFlag(FlagsCommand.FlagBit.NoGrab))
                outStream.WriteLine("flags nograb");
            if (command.HasFlag(FlagsCommand.FlagBit.NoRelease))
                outStream.WriteLine("flags norelease");
            if (command.HasFlag(FlagsCommand.FlagBit.Subtitle))
                outStream.WriteLine("flags subtitle");
            if (command.HasFlag(FlagsCommand.FlagBit.Passive2D))
                outStream.WriteLine("flags passive2d");
            if (command.HasFlag(FlagsCommand.FlagBit.Passive))
                outStream.WriteLine("flags passive");
        }

        private void DumpSubwindowCommand(SubWindowCommand command, OutputStream outStream)
        {
            outStream.WriteString("subwindow ");
            outStream.WriteString(IdToLabel(command.Label));
            outStream.WriteLine("");
        }

        private void DumpChoiceCommand(ChoiceCommand command, OutputStream outStream)
        {
            DumpConditionPrefix(command.Condition, outStream);

            outStream.WriteString("choice ");
            DumpQuotedString(command.Str, outStream);
            outStream.WriteString(" ");

            if (command.FormattingValue.Values.Count > 0)
            {
                DumpFormattingValue(command.FormattingValue, outStream);
                outStream.WriteString(", ");
            }

            outStream.WriteString(IdToLabel(command.Label));
            outStream.WriteLine("");
        }

        private static bool IsStringSimpleLabel(byte[] bytes)
        {
            int length = bytes.Length;

            if (length < 3)
                return false;

            int colonPos = -1;

            for (int i = 0; i < length; i++)
            {
                byte b = bytes[i];

                if (b == 58)
                {
                    if (colonPos == -1)
                        return false;

                    colonPos = i;
                }
                else if (b < 48 || b > 57)
                    return false;
            }

            if (colonPos == -1)
                return false;

            return (colonPos > 0 && colonPos < (length - 1));
        }

        private static ByteString PadBuggyGoto(ByteString str, bool isLastStatementInBlock, bool isLastExplicitSwitch, bool isLastSwitchInFile, out int gotoPaddingChars)
        {
            byte[] bytes = str.Bytes;

            int numPadBytes = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[bytes.Length - 1 - i] == 32)
                    numPadBytes++;
                else
                    break;
            }

            gotoPaddingChars = numPadBytes;

            if (numPadBytes == 0)
                return str;

            if (numPadBytes == 1)
                return str;

            {
                byte[] newBytes = new byte[bytes.Length];
                for (int i = 0; i < bytes.Length; i++)
                    newBytes[i] = bytes[i];

                bytes = newBytes;
            }

            bytes[bytes.Length - 2] = 47;
            bytes[bytes.Length - 1] = 47;

            return new ByteString(bytes);
        }

        private void DumpSimpleStringCommand(SimpleStringCommand command, OutputStream outStream)
        {
            bool isQuoted = true;
            ByteString commandStr = command.CommandStr;

            switch (command.CommandType)
            {
                case SimpleStringCommand.ECommandType.StartConsoleCommand:
                    outStream.WriteString("startconsole ");
                    break;
                case SimpleStringCommand.ECommandType.FinishConsoleCommand:
                    outStream.WriteString("finishconsole ");
                    break;
                case SimpleStringCommand.ECommandType.FontCommand:
                    outStream.WriteString("font ");
                    break;
                case SimpleStringCommand.ECommandType.StyleCommand:
                    outStream.WriteString("style ");
                    break;
                case SimpleStringCommand.ECommandType.NextWindowCommand:
                    if (IsStringSimpleLabel(command.CommandStr.Bytes))
                        outStream.WriteString("goto ");
                    else
                        outStream.WriteString("nextwindow ");

                    isQuoted = false;
                    break;
                default:
                    throw new Exception("Unknown simple string command");
            }

            if (isQuoted)
                DumpQuotedString(commandStr, outStream);
            else
                DumpString(commandStr, outStream);

            outStream.WriteLine("");
        }

        private void DumpXYPrintFXCommand(XYPrintFXCommand command, OutputStream outStream)
        {
            // If this only has XPos, YPos, Alpha, and String, then it's xyprint
            bool isSimpleXYPrint = (command.Red.Expression == null || command.Green.Expression == null || command.Blue.Expression == null || command.Font.Value == null);

            DumpConditionPrefix(command.Condition, outStream);

            if (isSimpleXYPrint)
                outStream.WriteString("xyprint ");
            else
                outStream.WriteString("xyprintfx ");

            if (command.XCoord.Expression == null || command.YCoord.Expression == null || command.Alpha.Expression == null)
                throw new Exception("Invalid XYPrintFX command");

            DumpOptimizableExpression(command.XCoord.Expression, outStream);
            outStream.WriteString(", ");
            DumpOptimizableExpression(command.YCoord.Expression, outStream);
            outStream.WriteString(", ");
            DumpOptimizableExpression(command.Alpha.Expression, outStream);
            outStream.WriteString(", ");

            if (!isSimpleXYPrint)
            {
                if (command.Red.Expression == null || command.Green.Expression == null || command.Blue.Expression == null || command.Font.Value == null)
                    throw new Exception("Internal error");

                DumpOptimizableExpression(command.Red.Expression, outStream);
                outStream.WriteString(", ");
                DumpOptimizableExpression(command.Green.Expression, outStream);
                outStream.WriteString(", ");
                DumpOptimizableExpression(command.Blue.Expression, outStream);
                outStream.WriteString(", ");
                DumpString(command.Font.Value, outStream);
                outStream.WriteString(", ");
            }

            DumpQuotedString(command.Message, outStream);
            DumpFormattingValue(command.FormattingValue, outStream);
            outStream.WriteLine("");
        }

        private void DumpWindowSwitchCommand(WindowSwitchCommand command, OutputStream outStream)
        {
            switch (command.CommandType)
            {
                case WindowSwitchCommand.ECommandType.StartSwitchCommand:
                    outStream.WriteString("startswitch ");
                    break;
                case WindowSwitchCommand.ECommandType.ThinkSwitchCommand:
                    outStream.WriteString("thinkswitch ");
                    break;
                case WindowSwitchCommand.ECommandType.FinishSwitchCommand:
                    outStream.WriteString("finishswitch ");
                    break;

                default:
                    throw new Exception("Unknown window switch command type");
            }

            if (command.Label >= 1000000000)
            {
                outStream.WriteLine("{");
                Switch sw = _idToSwitch[command.Label];

                DumpSwitchStatements(1, sw.CommandList, outStream, sw == _lastExplicitSwitch, sw == _lastSwitchInFile);

                _inlinedSwitches.Add(command.Label);
                outStream.WriteLine("}");
            }
            else
                outStream.WriteLine(IdToLabel(command.Label));
        }

        private void DumpCamCommand(CamCommand command, OutputStream outStream)
        {
            uint notSetValue = 32769;

            outStream.WriteString("cam ");
            DumpString(command.Name, outStream);

            if (command.From.Value != null)
            {
                outStream.WriteString(" from(");
                DumpString(command.From.Value, outStream);
                outStream.WriteString(")");
            }

            if (command.To.Value != null)
            {
                outStream.WriteString(" to(");
                DumpString(command.To.Value, outStream);
                outStream.WriteString(")");
            }

            if (command.Owner.Value != null)
            {
                outStream.WriteString(" owner(");
                DumpString(command.Owner.Value, outStream);
                outStream.WriteString(")");
            }

            if (command.Yaw != notSetValue)
                outStream.WriteString($" yaw({command.Yaw})");

            if (command.Pitch != notSetValue)
                outStream.WriteString($" pitch({command.Pitch})");

            if (command.Fov != notSetValue)
                outStream.WriteString($" fov({command.Fov})");

            if (command.Far != notSetValue)
                outStream.WriteString($" far({command.Far})");

            if (command.Near != notSetValue)
                outStream.WriteString($" near({command.Near})");

            if (command.Fwd != notSetValue)
                outStream.WriteString($" fwd({command.Fwd})");

            if (command.Speed != notSetValue)
                outStream.WriteString($" speed({command.Speed})");

            if (command.Lift != notSetValue)
                outStream.WriteString($" lift({command.Lift})");

            if (command.Lag != notSetValue)
                outStream.WriteString($" lag({command.Lag})");

            if (command.Occlude != notSetValue)
                outStream.WriteString($" occlude({command.Occlude})");

            if (command.Restore != notSetValue)
                outStream.WriteString(" restore");

            if (command.Zip != notSetValue)
                outStream.WriteString(" zip");

            outStream.WriteLine("");
        }

        private void DumpBackgroundCommand(BackgroundCommand command, OutputStream outStream)
        {
            outStream.WriteString("background");

            if (command.Color1 != 0)
                outStream.WriteString(" color1=" + BackgroundCommand.ColorToHex(command.Color1));
            if (command.Color2 != 0)
                outStream.WriteString(" color2=" + BackgroundCommand.ColorToHex(command.Color2));
            if (command.Color3 != 0)
                outStream.WriteString(" color3=" + BackgroundCommand.ColorToHex(command.Color3));
            if (command.Color4 != 0)
                outStream.WriteString(" color4=" + BackgroundCommand.ColorToHex(command.Color4));

            outStream.WriteLine("");
        }

        private void DumpBodyCommand(BodyCommand command, OutputStream outStream)
        {
            DumpConditionPrefix(command.Condition, outStream);
            outStream.WriteString("body ");
            DumpQuotedString(command.BodyStr, outStream);
            DumpFormattingValue(command.FormattingValue, outStream);
            outStream.WriteLine("");
        }

        public class ChainedStatement
        {
            public ChainedStatement? NextUnconditional { get; set; }
            public ChainedStatement? TrueCase { get; set; }
            public ChainedStatement? FalseCase { get; set; }

            public SwitchCommand Command { get; private set; }

            public ulong CCLabel { get; private set; }

            public ChainedStatement(SwitchCommand cmd, ulong label)
            {
                Command = cmd;
                CCLabel = label;
            }
        };

        private void DumpSimpleSwitchCommand(string name, SwitchCommand cmd, bool quoteArg, OutputStream outStream)
        {
            outStream.WriteString(name);

            if (cmd.Str.Value != null)
            {
                outStream.WriteString(" ");

                if (quoteArg)
                    DumpQuotedString(cmd.Str.Value, outStream);
                else
                    DumpString(cmd.Str.Value, outStream);

                DumpFormattingValue(cmd.FormattingValue, outStream);
            }
        }

        private void DumpSetFloatCommand(SwitchCommand cmd, OutputStream outStream)
        {
            if (cmd.Str.Value == null)
                throw new Exception("Set command string was null");

            if (cmd.Expression.Expression == null)
            {
                outStream.WriteString("unset ");
                DumpString(cmd.Str.Value, outStream);
            }
            else
            {
                outStream.WriteString("set ");
                DumpString(cmd.Str.Value, outStream);
                outStream.WriteString(" = ");
                DumpOptimizableExpression(cmd.Expression.Expression, outStream);
            }

            DumpFormattingValue(cmd.FormattingValue, outStream);
        }

        private static byte[] SliceByteArray(byte[] bytes, int start, int end)
        {
            byte[] newBytes = new byte[end - start];

            for (int i = start; i < end; i++)
                newBytes[i - start] = bytes[i];

            return newBytes;
        }

        private void DumpSetStringCommand(SwitchCommand cmd, OutputStream outStream)
        {
            if (cmd.Str.Value == null)
                throw new Exception("Set command string was null");

            byte[] cmdBytes = cmd.Str.Value.Bytes;

            int eqPos = -1;
            for (int i = 0; i < cmdBytes.Length; i++)
            {
                if (cmdBytes[i] == 61)
                {
                    eqPos = i;
                    break;
                }
            }

            if (eqPos == -1)
            {
                outStream.WriteString("unset ");
                outStream.WriteBytes(cmdBytes);
            }
            else
            {
                byte[] varName = SliceByteArray(cmdBytes, 0, eqPos);
                byte[] valueToAssign = SliceByteArray(cmdBytes, eqPos + 1, cmdBytes.Length);

                bool needToEscape = false;
                if (valueToAssign.Length > 0)
                {
                    if (valueToAssign[0] == 34)
                    {
                        if (valueToAssign.Length < 2 || valueToAssign[valueToAssign.Length - 1] != 34)
                            throw new Exception("Set string command had unusual quote handling");

                        needToEscape = true;
                        valueToAssign = SliceByteArray(valueToAssign, 1, valueToAssign.Length - 1);
                    }
                }

                outStream.WriteString("set ");
                outStream.WriteBytes(varName);
                outStream.WriteString(" = ");

                if (needToEscape)
                    DumpQuotedString(new ByteString(valueToAssign), outStream);
                else
                    outStream.WriteBytes(valueToAssign);

                DumpFormattingValue(cmd.FormattingValue, outStream);
            }
        }

        private void DumpGotoCommand(SwitchCommand cmd, OutputStream outStream, bool isLastStatementInBlock, bool isLastExplicitSwitch, bool isLastSwitchInFile, out bool isReturn, out int gotoPaddingChars)
        {
            isReturn = false;
            gotoPaddingChars = 0;

            if (cmd.Str.Value == null)
                throw new Exception("Goto command missing argument");

            byte[] target = cmd.Str.Value.Bytes;

            if (target.Length == 3 && target[0] == 48 && target[1] == 58 && target[2] == 48)
            {
                isReturn = true;
                outStream.WriteString("return");
                return;
            }

            ByteString fixedUpLabel = PadBuggyGoto(cmd.Str.Value, isLastStatementInBlock, isLastExplicitSwitch, isLastSwitchInFile, out gotoPaddingChars);

            outStream.WriteString("goto ");
            DumpString(fixedUpLabel, outStream);
            DumpFormattingValue(cmd.FormattingValue, outStream);
        }

        private void DumpSwitchCommand(SwitchCommand cmd, OutputStream outStream, bool isLastStatementInBlock, bool isLastExplicitSwitch, bool isLastSwitchInFile, out bool isReturn, out int gotoPaddingChars)
        {
            gotoPaddingChars = 0;
            isReturn = false;

            switch (cmd.CommandType)
            {
                case SwitchCommand.ECommandType.NoOpCommand:
                    // Only emitted in empty
                    break;

                case SwitchCommand.ECommandType.SetFloatCommand:
                    DumpSetFloatCommand(cmd, outStream);
                    break;

                case SwitchCommand.ECommandType.SetStringCommand:
                    DumpSetStringCommand(cmd, outStream);
                    break;

                case SwitchCommand.ECommandType.GotoCommand:
                    DumpGotoCommand(cmd, outStream, isLastStatementInBlock, isLastExplicitSwitch, isLastSwitchInFile, out isReturn, out gotoPaddingChars);
                    break;

                case SwitchCommand.ECommandType.GoSubCommand:
                    DumpSimpleSwitchCommand("gosub", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.ConsoleCommand:
                    DumpSimpleSwitchCommand("console", cmd, true, outStream);
                    break;

                case SwitchCommand.ECommandType.EchoCommand:
                    DumpSimpleSwitchCommand("echo", cmd, true, outStream);
                    break;

                case SwitchCommand.ECommandType.TargetCommand:
                    DumpSimpleSwitchCommand("target", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.PathTargetCommand:
                    DumpSimpleSwitchCommand("pathtarget", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.ExternCommand:
                    DumpSimpleSwitchCommand("extern", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.PlayAmbientCommand:
                    DumpSimpleSwitchCommand("playambient", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.LoopAmbientCommand:
                    DumpSimpleSwitchCommand("loopambient", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.StopAmbientCommand:
                    DumpSimpleSwitchCommand("stopambient", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.PlaySceneCommand:
                    DumpSimpleSwitchCommand("playscene", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.LoopSceneCommand:
                    DumpSimpleSwitchCommand("loopscene", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.StopSceneCommand:
                    DumpSimpleSwitchCommand("stopscene", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.ChainScriptsCommand:
                    DumpSimpleSwitchCommand("chainscripts", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.CloseWindowCommand:
                    DumpSimpleSwitchCommand("closewindow", cmd, false, outStream);
                    break;

                case SwitchCommand.ECommandType.LoadAPECommand:
                    DumpSimpleSwitchCommand("loadape", cmd, true, outStream);
                    break;

                case SwitchCommand.ECommandType.SetFocusCommand:
                    DumpSimpleSwitchCommand("setfocus", cmd, true, outStream);
                    break;

                default:
                    throw new Exception("Invalid switch command");
            }
        }

        private static bool IsCommandTypeControlFlow(SwitchCommand.ECommandType ctype)
        {
            return (ctype == SwitchCommand.ECommandType.IfCommand || ctype == SwitchCommand.ECommandType.WhileCommand);
        }

        private void DumpChainedStatementBlock(int indent, bool skipInitialIndent, ChainedStatement? stmt, OutputStream outStream, bool containsLastStatement, bool isLastExplicitSwitch, bool isLastSwitchInFile)
        {
            int numIndentChars = indent * 2;

            byte[] indentBytes = new byte[numIndentChars];

            for (int i = 0; i < numIndentChars; i++)
                indentBytes[i] = 32;

            while (stmt != null)
            {
                SwitchCommand cmd = stmt.Command;

                if (IsCommandTypeControlFlow(cmd.CommandType))
                {
                    ChainedStatement? trueCaseStmt = stmt.TrueCase;
                    if (trueCaseStmt == null)
                        throw new Exception("Control flow block was empty");

                    ChainedStatement? falseCaseStmt = stmt.FalseCase;
                    if (falseCaseStmt != null && cmd.CommandType != SwitchCommand.ECommandType.IfCommand)
                        throw new Exception("False case existed for non-if statement");

                    if (cmd.Expression.Expression == null)
                        throw new Exception("Control flow missing condition");

                    if (!skipInitialIndent)
                        outStream.WriteBytes(indentBytes);

                    if (cmd.CommandType == SwitchCommand.ECommandType.IfCommand)
                        outStream.WriteString("if (");
                    if (cmd.CommandType == SwitchCommand.ECommandType.WhileCommand)
                        outStream.WriteString("while (");

                    DumpOptimizableExpression(cmd.Expression.Expression, outStream);

                    outStream.WriteString(")");

                    bool isTrueCaseSingleCommand = false;
                    if (!IsCommandTypeControlFlow(trueCaseStmt.Command.CommandType) && trueCaseStmt.NextUnconditional == null)
                        isTrueCaseSingleCommand = true;

                    if (!isTrueCaseSingleCommand)
                        outStream.WriteString(" {");
                    outStream.WriteLine("");

                    bool trueCaseContainsLastStatement = (containsLastStatement && isTrueCaseSingleCommand && stmt.NextUnconditional == null && stmt.FalseCase == null);
                    DumpChainedStatementBlock(indent + 1, false, trueCaseStmt, outStream, trueCaseContainsLastStatement, isLastExplicitSwitch, isLastSwitchInFile);

                    bool isTrailingBrace = false;
                    if (!isTrueCaseSingleCommand)
                    {
                        outStream.WriteBytes(indentBytes);
                        outStream.WriteString("}");
                        isTrailingBrace = true;
                    }

                    if (falseCaseStmt != null)
                    {
                        if (isTrailingBrace)
                            outStream.WriteString(" ");
                        else
                            outStream.WriteBytes(indentBytes);

                        bool isFalseCaseSingleCommand = false;
                        bool isElseIf = false;
                        if (falseCaseStmt.Command.CommandType != SwitchCommand.ECommandType.WhileCommand && falseCaseStmt.NextUnconditional == null)
                        {
                            isFalseCaseSingleCommand = true;
                            if (falseCaseStmt.Command.CommandType == SwitchCommand.ECommandType.IfCommand)
                                isElseIf = true;
                        }

                        bool falseCaseContainsLastStatement = (containsLastStatement && isFalseCaseSingleCommand && stmt.NextUnconditional == null);

                        isTrailingBrace = false;
                        if (isElseIf)
                        {
                            outStream.WriteString("else ");
                            DumpChainedStatementBlock(indent, true, falseCaseStmt, outStream, falseCaseContainsLastStatement, isLastExplicitSwitch, isLastSwitchInFile);
                        }
                        else if (isFalseCaseSingleCommand)
                        {
                            outStream.WriteLine("else");
                            DumpChainedStatementBlock(indent + 1, false, falseCaseStmt, outStream, falseCaseContainsLastStatement, isLastExplicitSwitch, isLastSwitchInFile);
                        }
                        else
                        {
                            outStream.WriteLine("else {");
                            outStream.WriteBytes(indentBytes);
                            DumpChainedStatementBlock(indent + 1, false, falseCaseStmt, outStream, false, isLastExplicitSwitch, isLastSwitchInFile);
                            outStream.WriteString("}");
                            isTrailingBrace = true;
                        }
                    }

                    if (isTrailingBrace)
                        outStream.WriteLine("");
                }
                else
                {
                    bool isLastStatementInBlock = (containsLastStatement && stmt.NextUnconditional == null);
                    bool isReturn;
                    int gotoPaddingChars;

                    outStream.WriteBytes(indentBytes);
                    DumpSwitchCommand(cmd, outStream, isLastStatementInBlock, isLastExplicitSwitch, isLastSwitchInFile, out isReturn, out gotoPaddingChars);

                    bool suppressNewline = false;

                    if (isReturn && isLastExplicitSwitch && isLastStatementInBlock)
                        suppressNewline = true;

                    // Global53 crash
                    if (cmd.CommandType == SwitchCommand.ECommandType.GotoCommand && !isReturn && isLastStatementInBlock && !isLastSwitchInFile && isLastExplicitSwitch && gotoPaddingChars == 0)
                        suppressNewline = true;

                    // crevice99 crash
                    if (cmd.CommandType == SwitchCommand.ECommandType.ExternCommand && isLastStatementInBlock && isLastExplicitSwitch)
                        suppressNewline = true;

                    // grumpos crash
                    if (cmd.CommandType == SwitchCommand.ECommandType.SetFloatCommand && isLastStatementInBlock && isLastExplicitSwitch && !isLastSwitchInFile)
                        suppressNewline = true;

                    if (!suppressNewline)
                        outStream.WriteLine("");
                }

                stmt = stmt.NextUnconditional;
                skipInitialIndent = false;
            }
        }

        private void DumpSwitchStatements(int startIndent, SwitchCommandList commandList, OutputStream outStream, bool isLastExplicitSwitch, bool isLastSwitchInFile)
        {
            Dictionary<ulong, ChainedStatement> ccToStatement = new Dictionary<ulong, ChainedStatement>();

            foreach (CCPrefixedCommand prefixedCommand in commandList.Commands)
            {
                ChainedStatement stmt = new ChainedStatement(prefixedCommand.Command, prefixedCommand.ConditionControl);
                ccToStatement[prefixedCommand.ConditionControl] = stmt;
            }

            foreach (ChainedStatement stmt in ccToStatement.Values)
            {
                if (stmt.CCLabel == 1)
                    continue;

                ulong precedingInstrCC = (stmt.CCLabel >> 2);
                ChainedStatement precedingInstr = ccToStatement[precedingInstrCC];

                switch ((int)(stmt.CCLabel & 3))
                {
                    default:
                    case 0:
                        throw new Exception("Invalid CC context");
                    case 1:
                        if (precedingInstr == null || precedingInstr.TrueCase != null)
                            throw new Exception("Invalid CC context");
                        precedingInstr.TrueCase = stmt;
                        break;
                    case 2:
                        if (precedingInstr == null || precedingInstr.FalseCase != null)
                            throw new Exception("Invalid CC context");
                        precedingInstr.FalseCase = stmt;
                        break;
                    case 3:
                        if (precedingInstr == null || precedingInstr.NextUnconditional != null)
                            throw new Exception("Invalid CC context");
                        precedingInstr.NextUnconditional = stmt;
                        break;
                }
            }

            ChainedStatement? firstStatement = null;
            if (ccToStatement.TryGetValue(1, out firstStatement))
                DumpChainedStatementBlock(startIndent, false, firstStatement, outStream, true, isLastExplicitSwitch, isLastSwitchInFile);
        }

        public void Dump(OutputStream outStream)
        {
            foreach (Switch sw in _apeFile.RootElementList.Switches.SwitchList)
            {
                _lastSwitchInFile = sw;

                if (sw.Label < 1000000000)
                    _lastExplicitSwitch = sw;
            }

            foreach (Window window in _apeFile.RootElementList.Windows)
            {
                outStream.WriteLine($"#window {IdToLabel(window.WindowId)}");

                bool hasNoBackgroundFlag = false;
                bool hasBackgroundCommand = false;
                bool hasBackgroundEmittableImage = false;
                foreach (IWindowCommand cmd in window.CommandList.Commands)
                {
                    WindowCommandType cmdType = cmd.WindowCommandType;

                    if (cmdType == WindowCommandType.Flags)
                        hasNoBackgroundFlag = ((FlagsCommand)cmd).HasFlag(FlagsCommand.FlagBit.NoBackground);
                    else if (cmdType == WindowCommandType.Background)
                        hasBackgroundCommand = true;
                    else if (cmdType == WindowCommandType.Image)
                    {
                        if (((ImageCommand)cmd).CanEmitAsBackground())
                            hasBackgroundEmittableImage = true;
                    }
                }

                bool shouldEmitImageAsBackground = (hasBackgroundEmittableImage && !hasBackgroundCommand && !hasNoBackgroundFlag);

                List<WindowSwitchCommand> windowSwitchCommands = new List<WindowSwitchCommand>();
                foreach (IWindowCommand cmd in window.CommandList.Commands)
                {
                    switch (cmd.WindowCommandType)
                    {
                        case WindowCommandType.Title:
                            DumpTitleCommand((TitleCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Talk:
                            DumpTalkCommand((TalkCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Dimensions:
                            DumpDimensionsCommand((DimensionsCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Image:
                            DumpImageCommand((ImageCommand)cmd, shouldEmitImageAsBackground, outStream);
                            break;
                        case WindowCommandType.Flags:
                            DumpFlagsCommand((FlagsCommand)cmd, outStream);
                            break;
                        case WindowCommandType.SubWindow:
                            DumpSubwindowCommand((SubWindowCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Choice:
                            DumpChoiceCommand((ChoiceCommand)cmd, outStream);
                            break;
                        case WindowCommandType.SimpleStringCommand:
                            DumpSimpleStringCommand((SimpleStringCommand)cmd, outStream);
                            break;
                        case WindowCommandType.XYPrintFX:
                            DumpXYPrintFXCommand((XYPrintFXCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Switch:
                            windowSwitchCommands.Add((WindowSwitchCommand)cmd);
                            break;
                        case WindowCommandType.Body:
                            DumpBodyCommand((BodyCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Background:
                            DumpBackgroundCommand((BackgroundCommand)cmd, outStream);
                            break;
                        case WindowCommandType.Cam:
                            DumpCamCommand((CamCommand)cmd, outStream);
                            break;
                        default:
                            throw new Exception("Unknown window command type");
                    }
                }

                if (!hasNoBackgroundFlag && !shouldEmitImageAsBackground && !hasBackgroundCommand)
                {
                    outStream.WriteLine("background color1=00000000");
                }

                windowSwitchCommands.Sort(new WindowSwitchCommandComparer());

                foreach (WindowSwitchCommand windowSwitchCommand in windowSwitchCommands)
                    DumpWindowSwitchCommand(windowSwitchCommand, outStream);

                outStream.WriteLine("");
            }

            foreach (Switch sw in _apeFile.RootElementList.Switches.SwitchList)
            {
                if (_inlinedSwitches.Contains(sw.Label))
                    continue;

                outStream.WriteLine("");
                outStream.WriteLine($"#switch {IdToLabel(sw.Label)}");
                DumpSwitchStatements(0, sw.CommandList, outStream, sw == _lastExplicitSwitch, sw == _lastSwitchInFile);
            }
        }
    }
}
