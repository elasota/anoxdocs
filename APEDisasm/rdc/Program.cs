using AnoxAPE.Elements;
using AnoxAPECompiler;

namespace rdc
{
    internal class Program
    {
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

        private static void PrintUsageAndExit()
        {
            Console.Error.WriteLine("rdc Anachronox APE compiler");
            Console.Error.WriteLine("Copyright (c) 2024 Eric Lasota / Gale Force Games");
            Console.Error.WriteLine("");
            Console.Error.WriteLine("Usage: rdc [options] <input>[.txt]");
            Console.Error.WriteLine("    -o <file>    Set output file path");
            Console.Error.WriteLine("    -f <name>    Override file name (for inline switch hash)");
            Console.Error.WriteLine("    -is <value>  Override inline switch value");
            Console.Error.WriteLine("    -dparse      Enable all dparse compatibility options");
            Console.Error.WriteLine("    -dm          Use dparse macro behavior");
            Console.Error.WriteLine("    -dc          Use dparse comment removal behavior");
            Console.Error.WriteLine("    -dcam        Use dparse cam command behavior");
            Console.Error.WriteLine("    -dp          Use dparse operator precedences");
            Console.Error.WriteLine("    -dt          Use dparse top-level directive handling");
            Console.Error.WriteLine("    -dl          Use dparse labeled command handling");
            Console.Error.WriteLine("    -ds          Use dparse set variable name behavior");
            Console.Error.WriteLine("    -noexpfloat  Disable exponential float syntax support");
            Console.Error.WriteLine("    -noemptycond Disable empty condition support");
            Console.Error.WriteLine("    -opt         Optimize constant expressions");
            Console.Error.WriteLine("    -e           Warnings are errors");

            System.Environment.Exit(-1);
        }

        static void Main(string[] args)
        {
            string? outFilePath = null;
            string? overrideInputFileName = null;

            CompilerOptions options = new CompilerOptions();

            int argIndex = 0;

            if (args.Length == 0)
                PrintUsageAndExit();

            while (argIndex < args.Length)
            {
                string arg = args[argIndex++];

                if (arg == "-f")
                {
                    if (argIndex == args.Length)
                        PrintUsageAndExit();

                    overrideInputFileName = args[argIndex++];
                }
                else if (arg == "-is")
                {
                    if (argIndex == args.Length)
                        PrintUsageAndExit();

                    uint hash = 0;
                    if (!uint.TryParse(args[argIndex++], out hash))
                    {
                        Console.Error.WriteLine("-is argument was invalid (should be a number)");
                        System.Environment.Exit(-1);
                    }

                    if (hash >= 100000)
                    {
                        Console.Error.WriteLine("-is maximum value is 99999");
                        System.Environment.Exit(-1);
                    }

                    options.UseExplicitInlineSwitchHash = true;
                    options.ExplicitInlineSwitchHash = hash;
                }
                else if (arg == "-o")
                {
                    if (argIndex == args.Length)
                        PrintUsageAndExit();

                    outFilePath = args[argIndex++];
                }
                else if (arg == "-dt")
                    options.DParseTopLevelDirectiveHandling = true;
                else if (arg == "-dl")
                {
                    options.DParseLabeledCommandHandling = true;
                    options.DParseCommentHandling = true;
                }
                else if (arg == "-dc")
                    options.DParseCommentHandling = true;
                else if (arg == "-dm")
                    options.DParseMacroHandling = true;
                else if (arg == "-dp")
                    options.DParseOperatorPrecedences = true;
                else if (arg == "-ds")
                    options.DParseSetVariableNameHandling = true;
                else if (arg == "-dcam")
                    options.DParseCamCommandHandling = true;
                else if (arg == "-de")
                    options.AllowMalformedExprs = true;
                else if (arg == "-dparse")
                    options.SetAllDParseOptions();
                else if (arg == "-opt")
                    options.Optimize = true;
                else if (arg == "-noexpfloat")
                    options.AllowExpFloatSyntax = false;
                else if (arg == "-noemptycond")
                    options.AllowEmptyConditionBlocks = false;
                else
                    break;
            }

            if (args.Length - argIndex != 0)
                PrintUsageAndExit();

            string inFilePath = args[argIndex - 1];

            if (overrideInputFileName == null)
                options.InputFileName = Path.GetFileName(inFilePath);
            else
                options.InputFileName = overrideInputFileName;

            options.Logger = new CompilerLogger();

            if (outFilePath == null)
            {
                outFilePath = Path.ChangeExtension(inFilePath, ".ape");

                using (FileStream inStream = new FileStream(inFilePath, FileMode.Open, FileAccess.Read))
                {
                    Compiler compiler = new Compiler(options, inStream);

                    APEFile? apeFile = compiler.Compile();

                    if (apeFile != null)
                    {
                        using (FileStream outStream = new FileStream(outFilePath, FileMode.Create, FileAccess.Write))
                        {
                            apeFile.Write(outStream);
                        }
                    }
                }
            }
        }
    }
}
