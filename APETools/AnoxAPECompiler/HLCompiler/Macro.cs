// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
using AnoxAPE;

namespace AnoxAPECompiler.HLCompiler
{
    internal class Macro
    {
        public ByteStringSlice Name { get; private set; }

        private List<ByteStringSlice> _tokens;

        public Macro(ByteStringSlice name)
        {
            Name = name;
            _tokens = new List<ByteStringSlice>();
        }

        public static Macro CreateSimpleMacro(ByteStringSlice name, IEnumerable<ByteStringSlice> tokens)
        {
            Macro macro = new Macro(name);

            macro._tokens.AddRange(tokens);

            return macro;
        }
    }
}
