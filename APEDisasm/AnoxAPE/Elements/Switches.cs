namespace AnoxAPE.Elements
{
    public class Switches
    {
        public IReadOnlyList<Switch> SwitchList { get; private set; }
        private List<Switch> _switchList;

        public Switches()
        {
            _switchList = new List<Switch>();
            SwitchList = _switchList;
        }

        public Switches(IEnumerable<Switch> switches)
        {
            _switchList = new List<Switch>();
            _switchList.AddRange(switches);
            SwitchList = _switchList;
        }

        public void Load(InputStream inStream, int indent, OutputStream? disasmStream)
        {
            if (disasmStream != null)
                disasmStream.WriteLineIndented(indent, "Switches");

            inStream.MarkSwitchLabel();
            uint label = inStream.ReadUInt32();

            while (label != 0)
            {
                Switch sw = new Switch(label);
                sw.Load(inStream, indent, disasmStream);

                _switchList.Add(sw);

                inStream.MarkSwitchLabel();
                label = inStream.ReadUInt32();
            }
        }

        public void Write(OutputStream outStream)
        {
            foreach (Switch sw in _switchList)
                sw.WriteWithID(outStream);

            outStream.WriteUInt32(0);
        }
    }
}
