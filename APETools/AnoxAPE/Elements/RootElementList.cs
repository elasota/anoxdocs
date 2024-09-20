// (c) 2024 Eric Lasota / Gale Force Games
// SPDX-License-Identifier: MIT
namespace AnoxAPE.Elements
{
    public class RootElementList
    {
        public IReadOnlyList<Window> Windows { get; private set; }
        public Switches Switches { get; private set; }

        private List<Window> _windows;

        public RootElementList()
        {
            _windows = new List<Window>();
            Windows = _windows;
            Switches = new Switches();
        }

        public RootElementList(IEnumerable<Window> windows, Switches switches)
        {
            _windows = new List<Window>();
            _windows.AddRange(windows);
            Windows = _windows;
            Switches = switches;
        }

        public void Load(InputStream inStream, OutputStream? disasmStream)
        {
            while (true)
            {
                uint windowID = inStream.ReadUInt32();
                if (windowID == 0)
                {
                    uint switchesTagID = inStream.ReadUInt32();

                    if (switchesTagID != 0xfffffffeu)
                        inStream.ReportError("Unexpected switch tag ID");

                    Switches.Load(inStream, 0, disasmStream);

                    if (!inStream.IsAtEOF())
                        inStream.ReportError("Unexpected trailing data");
                    break;
                }
                else
                {
                    if (disasmStream != null)
                        disasmStream.WriteLine($"FilePosition({inStream.Position})");

                    Window window = new Window(windowID);

                    if (disasmStream != null)
                    {
                        disasmStream.WriteString($"Window {windowID}\n");
                    }

                    window.Load(inStream, disasmStream);

                    _windows.Add(window);
                }
            }
        }

        public void Write(OutputStream outStream)
        {
            foreach (Window w in _windows)
                w.WriteWithID(outStream);

            outStream.WriteUInt32(0);
            outStream.WriteUInt32(0xfffffffeu);

            Switches.Write(outStream);
        }
    }
}
