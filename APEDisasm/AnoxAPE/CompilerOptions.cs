
namespace AnoxAPE
{
    public class CompilerOptions
    {
        public string InputFileName { get; set; }

        public ILogger? Logger { get; set; }
        public bool DParseGotoHandling { get; set; }
        public bool DParseCommentHandling { get; set; }
        public bool DParseMacroHandling { get; set; }
        public bool DParseOperatorPrecedences { get; set; }
        public bool AllowExpFloatSyntax { get; set; }

        public CompilerOptions()
        {
            InputFileName = "";
            DParseGotoHandling = false;
            DParseCommentHandling = false;
            DParseMacroHandling = false;
            DParseOperatorPrecedences = false;
            AllowExpFloatSyntax = true;
        }

        public void SetAllDParseOptions()
        {
            DParseGotoHandling = true;
            DParseCommentHandling = true;
            DParseMacroHandling = true;
            DParseOperatorPrecedences = true;
        }
    }
}
