namespace AnoxAPE.Elements
{
    public class APEFile
    {
        public RootElementList RootElementList { get; private set; }


        public APEFile()
        {
            RootElementList = new RootElementList();
        }

        public APEFile(RootElementList rootElementList)
        {
            RootElementList = rootElementList;
        }

        public void Load(InputStream inStream, OutputStream? disasmStream)
        {
            uint header1 = inStream.ReadUInt32();
            uint header2 = inStream.ReadUInt32();

            if (header1 != 317 && header2 != 0xffffffffu)
                inStream.ReportError("Header is invalid");

            RootElementList.Load(inStream, disasmStream);
        }

        public void Write(Stream stream)
        {
            OutputStream outStream = new OutputStream(stream);

            outStream.WriteUInt32(317);
            outStream.WriteUInt32(0xffffffffu);

            RootElementList.Write(outStream);
        }
    }
}
