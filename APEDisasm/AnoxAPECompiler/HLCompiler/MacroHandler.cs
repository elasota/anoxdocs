using AnoxAPE;

namespace AnoxAPECompiler.HLCompiler
{
    internal class MacroHandler
    {
        private Dictionary<ByteStringSlice, Macro> _macros;

        public MacroHandler()
        {
            _macros = new Dictionary<ByteStringSlice, Macro>();
        }

        public Macro? FindMacro(ByteStringSlice name)
        {
            Macro? macro;
            if (_macros.TryGetValue(name, out macro))
                return macro;

            return null;
        }

        internal void AddMacro(Macro macro)
        {
            _macros.Add(macro.Name, macro);
        }
    }
}
