// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public class SwitchCommandList
    {
        public IReadOnlyList<CCPrefixedCommand> Commands { get; private set; }
        private List<CCPrefixedCommand> _commandsList;

        public SwitchCommandList()
        {
            _commandsList = new List<CCPrefixedCommand>();
            Commands = _commandsList;
        }

        public SwitchCommandList(IEnumerable<CCPrefixedCommand> cmds)
        {
            _commandsList = new List<CCPrefixedCommand>(cmds);
            Commands = _commandsList;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            while (true)
            {
                ulong cc = inStream.ReadUInt64();
                byte commandByte = inStream.ReadByte();

                if (disasmStream != null)
                {
                    string ccBin = "";
                    ulong ccBits = cc;

                    while (ccBits != 0)
                    {
                        ccBin = $"{(ccBits & 1)}" + ccBin;
                        ccBits >>= 1;
                    }

                    if (ccBin.Length == 0)
                        ccBin = "0";

                    disasmStream.WriteLineIndented(indent, $"CCLabel({ccBin})");
                }

                if (commandByte > 21)
                {
                    if (commandByte == 69)
                    {
                        if (cc != 0)
                            inStream.ReportError("Invalid cc code for end command");

                        if (disasmStream != null)
                            disasmStream.WriteLineIndented(indent, "EndCommand");
                        return;
                    }

                    inStream.ReportError($"Invalid switch command code {commandByte}");
                }

                SwitchCommand cmd = new SwitchCommand(commandByte);

                cmd.Load(inStream, indent, disasmStream);

                CCPrefixedCommand prefixedCmd = new CCPrefixedCommand(cc, cmd);

                _commandsList.Add(prefixedCmd);
            }
        }

        public void Write(OutputStream outStream)
        {
            foreach (CCPrefixedCommand prefixedCommand in _commandsList)
            {
                outStream.WriteUInt64(prefixedCommand.ConditionControl);
                prefixedCommand.Command.WriteWithID(outStream);
            }

            outStream.WriteUInt64(0);
            outStream.WriteByte(69);
        }
    }
}
