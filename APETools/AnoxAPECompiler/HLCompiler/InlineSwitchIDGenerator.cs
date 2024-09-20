// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPECompiler.HLCompiler
{
    internal class InlineSwitchIDGenerator : IInlineSwitchIDGenerator
    {
        public uint _nextBaseID;
        public uint _inlineSwitchHash;

        public InlineSwitchIDGenerator(uint inlineSwitchHash)
        {
            _nextBaseID = 0;
            _inlineSwitchHash = inlineSwitchHash;
        }

        public bool TryGenerateNextID(out uint id)
        {
            if (_nextBaseID == 10000)
            {
                id = 0;
                return false;
            }

            uint baseID = _nextBaseID++;

            id = baseID + _inlineSwitchHash * 10000u + 1000000000u;
            return true;
        }
    }
}
