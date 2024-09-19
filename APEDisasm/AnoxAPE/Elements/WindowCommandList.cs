namespace AnoxAPE.Elements
{
    public class WindowCommandList
    {
        public IReadOnlyList<IWindowCommand> Commands { get; private set; }
        private List<IWindowCommand> _commandsList;

        public WindowCommandList()
        {
            _commandsList = new List<IWindowCommand>();
            Commands = _commandsList;
        }

        public WindowCommandList(IEnumerable<IWindowCommand> commands)
        {
            _commandsList = new List<IWindowCommand>();
            _commandsList.AddRange(commands);
            Commands = _commandsList;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            while (true)
            {
                byte commandByte = inStream.ReadByte();
                IWindowCommand? cmd = null;

                switch (commandByte)
                {
                    case 49:
                    case 50:
                    case 51:
                        cmd = new WindowSwitchCommand(commandByte);
                        break;
                    case 65:
                    case 70:
                    case 78:
                    case 79:
                    case 87:
                        cmd = new SimpleStringCommand(commandByte);
                        break;
                    case 66:
                    case 84:
                        cmd = new ConditionalFormattedStringCommand(commandByte);
                        break;
                    case 67:
                        cmd = new ChoiceCommand();
                        break;
                    case 68:
                        cmd = new BackgroundCommand();
                        break;
                    case 69:
                        if (disasmStream != null)
                            disasmStream.WriteLineIndented(indent, "EndCommand");
                        return;
                    case 71:
                        cmd = new DimensionsCommand();
                        break;
                    case 72:
                        cmd = new SubWindowCommand();
                        break;
                    case 73:
                        cmd = new ImageCommand();
                        break;
                    case 76:
                        cmd = new FlagsCommand();
                        break;
                    case 77:
                        cmd = new CamCommand();
                        break;
                    case 80:
                        cmd = new XYPrintFXCommand();
                        break;
                    case 89:
                        cmd = new TalkCommand();
                        break;
                    default:
                        inStream.ReportError($"Unknown command code {commandByte}");
                        break;
                }

                if (cmd == null)
                    throw new Exception("Internal error: Didn't create a command");

                cmd.Load(inStream, indent, disasmStream);
                _commandsList.Add(cmd);
            }
        }

        public void Write(OutputStream outStream)
        {
            foreach (IWindowCommand windowCommand in _commandsList)
                windowCommand.WriteWithID(outStream);
            outStream.WriteByte(69);
        }
    }
}
