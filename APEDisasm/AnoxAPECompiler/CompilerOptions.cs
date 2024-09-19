
namespace AnoxAPECompiler
{
    public class CompilerOptions
    {
        public string InputFileName { get; set; }
        public uint ExplicitInlineSwitchHash { get; set; }
        public bool UseExplicitInlineSwitchHash { get; set; }

        public ILogger? Logger { get; set; }
        public bool DParseTopLevelDirectiveHandling { get; set; }
        public bool DParseGotoHandling { get; set; }
        public bool DParseCommentHandling { get; set; }
        public bool DParseMacroHandling { get; set; }
        public bool DParseOperatorPrecedences { get; set; }
        public bool AllowMalformedExprs { get; set; }
        public bool AllowExpFloatSyntax { get; set; }
        public bool AllowEscapesInExprStrings { get; set; }
        public bool AllowEmptyConditionBlocks { get; set; }
        public bool Optimize { get; set; }

        public CompilerOptions()
        {
            InputFileName = "";
            DParseTopLevelDirectiveHandling = false;
            DParseGotoHandling = false;
            DParseCommentHandling = false;
            DParseMacroHandling = false;
            DParseOperatorPrecedences = false;
            AllowExpFloatSyntax = true;
            AllowEscapesInExprStrings = false;
            AllowEmptyConditionBlocks = true;
        }

        public void SetAllDParseOptions()
        {
            DParseTopLevelDirectiveHandling = true;
            DParseGotoHandling = true;
            DParseCommentHandling = true;
            DParseMacroHandling = true;
            DParseOperatorPrecedences = true;
            AllowMalformedExprs = true;
            AllowEscapesInExprStrings = false;
            Optimize = false;
            AllowEmptyConditionBlocks = false;
        }
    }
}
