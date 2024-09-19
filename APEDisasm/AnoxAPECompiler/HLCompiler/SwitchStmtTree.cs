using AnoxAPE.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnoxAPECompiler.HLCompiler
{
    internal class SwitchStmtTree
    {
        public SwitchStmtTree? NextStmt { get; set; }
        public SwitchStmtTree? TrueTree { get; set; }
        public SwitchStmtTree? FalseTree { get; set; }

        public SwitchCommand.ECommandType CmdType { get; private set; }
        public OptionalString Str { get; private set; }
        public FormattingValue FormattingValue { get; private set; }
        public IExprValue? Expr { get; private set; }

        public SwitchStmtTree(SwitchCommand.ECommandType cmdType, OptionalString str, FormattingValue fmt, IExprValue? expr)
        {
            CmdType = cmdType;
            Str = str;
            FormattingValue = fmt;
            Expr = expr;
        }
    }
}
