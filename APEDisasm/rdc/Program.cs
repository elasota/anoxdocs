namespace rdc
{
    internal class Program
    {
        private static void PrintUsage()
        {
            Console.Error.WriteLine("RKit dparse compiler");
            Console.Error.WriteLine("Usage: rdc [options] <input>[.txt]");
            Console.Error.WriteLine("    -f <name>    Override file name (for inline switch hash)");
            Console.Error.WriteLine("    -is <value>  Override inline switch value");
            Console.Error.WriteLine("    -d           Enable all dparse compatibility options");
            Console.Error.WriteLine("    -dm          Use dparse macro behavior");
            Console.Error.WriteLine("    -dc          Use dparse comment removal behavior");
            Console.Error.WriteLine("    -dg          Use dparse goto trailing whitespace behavior");
            Console.Error.WriteLine("    -dp          Use dparse operator precedences");
            Console.Error.WriteLine("    -dt          Use dparse top-level directive handling");
            Console.Error.WriteLine("    -o <file>    Set output file path");
            Console.Error.WriteLine("    -opt         Optimize constant expressions");
            Console.Error.WriteLine("    -e           Warnings are errors");

        }

        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
        }
    }
}
