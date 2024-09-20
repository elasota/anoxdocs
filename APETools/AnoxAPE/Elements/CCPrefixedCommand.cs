// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public struct CCPrefixedCommand
    {
        public ulong ConditionControl { get; private set; }
        public SwitchCommand Command { get; private set; }

        public CCPrefixedCommand(ulong cc, SwitchCommand command)
        {
            ConditionControl = cc;
            Command = command;
        }
    }
}
