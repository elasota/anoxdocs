using AnoxAPE;
using AnoxAPE.Elements;
using AnoxAPECompiler;

namespace rdc
{
    internal class Program
    {
        private static void PrintUsage()
        {
            Console.Error.WriteLine("rdc dparse-compatible APE compiler front-end");
            Console.Error.WriteLine("Copyright (c) 2024 Eric Lasota / Gale Force Games");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("Usage: dparse [options] <input>[.txt]");
        }

        private class CompilerLogger : ILogger
        {
            public void WriteLine(ILogger.MessageProperties msgProps, string message)
            {
                ILogger.LocationTag locTag = msgProps.LocationTag;

                string? strippedFileName = Path.GetFileName(locTag.FileName);
                string fileName = (strippedFileName == null) ? "" : strippedFileName;

                string formattedLocTag = $"{fileName}({locTag.FileLine + 1}:{locTag.FileCol + 1})";
                if (msgProps.Severity == ILogger.Severity.Info)
                    Console.WriteLine($"[INFO]  {formattedLocTag}: {message}");
                else if (msgProps.Severity == ILogger.Severity.Warning)
                    Console.Error.WriteLine($"[WARN]  {formattedLocTag}: {message}");
                else if (msgProps.Severity == ILogger.Severity.Error)
                    Console.Error.WriteLine($"[ERROR] {formattedLocTag}: {message}");
            }
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                System.Environment.ExitCode = -1;
                return;
            }

            string inputPath = args[args.Length - 1];

            string? ext = Path.GetExtension(inputPath);
            if (ext == null)
                inputPath = Path.ChangeExtension(inputPath, ".txt");
            else if (ext != ".txt")
            {
                Console.Error.WriteLine("Input extension is not .txt");
                System.Environment.ExitCode = -1;
                return;
            }

            string outputPath = Path.ChangeExtension(inputPath, ".ape");

            CompilerOptions options = new CompilerOptions();
            options.Logger = new CompilerLogger();
            options.InputFileName = Path.GetFileName(inputPath);
            options.SetAllDParseOptions();

            using (FileStream inStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
            {
                Compiler compiler = new Compiler(options, inStream);

                APEFile? apeFile = compiler.Compile();
                if (apeFile == null)
                {
                    System.Environment.ExitCode = -1;
                    return;
                }

                using (FileStream outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    apeFile.Write(outStream);
                }
            }
        }
    }
}
