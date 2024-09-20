// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPECompiler.HLCompiler
{
    interface IInlineSwitchIDGenerator
    {
        public bool TryGenerateNextID(out uint id);
    }
}
