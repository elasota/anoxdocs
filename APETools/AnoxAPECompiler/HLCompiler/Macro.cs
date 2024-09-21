// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
using AnoxAPE;

namespace AnoxAPECompiler.HLCompiler
{
    internal struct MacroToken
    {
        public TokenType TokenType { get; private set; }
        public ByteStringSlice Token { get; private set; }

        public MacroToken(TokenType tokenType, ByteStringSlice token)
        {
            TokenType = tokenType;
            Token = token;
        }
    }

    internal class Macro
    {
        public ByteStringSlice Name { get; private set; }
        public IReadOnlyList<MacroToken> Tokens { get { return _tokens; } }

        private List<MacroToken> _tokens;

        public Macro(ByteStringSlice name)
        {
            Name = name;
            _tokens = new List<MacroToken>();
        }

        public static Macro CreateSimpleMacro(ByteStringSlice name, IEnumerable<MacroToken> tokens)
        {
            Macro macro = new Macro(name);

            macro._tokens.AddRange(tokens);

            return macro;
        }
    }
}
