using System.IO;
using System;

namespace AnoxAPE.Elements
{
    public class SwitchCommand
    {
        public enum ECommandType
        {
            NoOpCommand = 0,
            IfCommand = 1,
            SetFloatCommand = 2,
            SetStringCommand = 3,
            GotoCommand = 4,
            GoSubCommand = 5,
            ConsoleCommand = 6,
            EchoCommand = 7,
            TargetCommand = 8,
            PathTargetCommand = 9,
            ExternCommand = 10,
            WhileCommand = 11,
            PlayAmbientCommand = 12,
            LoopAmbientCommand = 13,
            StopAmbientCommand = 14,
            PlaySceneCommand = 15,
            LoopSceneCommand = 16,
            StopSceneCommand = 17,
            ChainScriptsCommand = 18,
            CloseWindowCommand = 19,
            LoadAPECommand = 20,
            SetFocusCommand = 21,
        }

        public OptionalString Str { get; private set; }
        public ECommandType CommandType { get; private set; }
        public FormattingValue FormattingValue { get; private set; }
        public OptionalExpression Expression { get; private set; }

        public SwitchCommand(byte commandByte)
        {
            CommandType = (ECommandType)commandByte;
            Str = new OptionalString();
            FormattingValue = new FormattingValue();
            Expression = new OptionalExpression();
        }

        public SwitchCommand(ECommandType cmdType, OptionalString str, FormattingValue fmt, OptionalExpression expr)
        {
            CommandType = cmdType;
            Str = str;
            FormattingValue = fmt;
            Expression = expr;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, CommandType.ToString());

            Str.Load(inStream, indent + 1, disasmStream);
            FormattingValue.Load(inStream, indent + 1, disasmStream);
            Expression.Load(inStream, indent + 1, disasmStream);
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteByte((byte)CommandType);
            Str.Write(outStream);
            FormattingValue.Write(outStream);
            Expression.Write(outStream);
        }
    }
}
