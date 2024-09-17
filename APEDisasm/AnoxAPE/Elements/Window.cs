namespace AnoxAPE.Elements
{
    public class Window
    {
        public uint WindowId { get; private set; }
        public WindowCommandList CommandList { get; private set; }

        public Window(uint windowID)
        {
            WindowId = windowID;
            CommandList = new WindowCommandList();
        }

        public Window(uint windowID, WindowCommandList commandList)
        {
            WindowId = windowID;
            CommandList = commandList;
        }

        public void Load(InputStream inStream, OutputStream? disasmStream)
        {
            this.CommandList.Load(inStream, 1, disasmStream);
        }

        public void WriteWithID(OutputStream outStream)
        {
            outStream.WriteUInt32(WindowId);
            CommandList.Write(outStream);
        }
    }
}
