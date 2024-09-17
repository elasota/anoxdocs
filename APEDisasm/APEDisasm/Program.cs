using System.Diagnostics;
using AnoxAPE;
using AnoxAPE.Elements;

namespace APEDisasm
{
    internal class Program
    {

        static void PrintUsage()
        {
            Console.Error.WriteLine("Syntax: APEDisasm [options] <input> <output>");
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("    -src                            Decompile to source code");
            Console.Error.WriteLine("    -validate <path to dparse.exe>  Validate decompiled results");
            Environment.ExitCode = -1;
        }

        static void Decompile(Stream inStream, Stream outStream, bool enableInlineTracking, out IReadOnlySet<long>? labelLocations)
        {
            labelLocations = null;

            APEFile apeFile = new APEFile();
            InputStream wrappedInStream = new InputStream(inStream);
            OutputStream decompileStream = new OutputStream(outStream);

            if (enableInlineTracking)
            {
                HashSet<long> labelLocsHashSet = new HashSet<long>();
                wrappedInStream.LabelTracker = labelLocsHashSet;
                labelLocations = labelLocsHashSet;
            }

            apeFile.Load(wrappedInStream, null);

            Decompiler decompiler = new Decompiler();
            decompiler.Load(apeFile);

            decompiler.Dump(decompileStream);

            decompileStream.Flush();
        }

        static void Disassemble(Stream inStream, Stream outStream)
        {
            OutputStream disasmStream = new OutputStream(outStream);

            try
            {
                APEFile apeFile = new APEFile();

                apeFile.Load(new InputStream(inStream), disasmStream);
            }
            catch (Exception)
            {
                disasmStream.Flush();
                throw;
            }

            disasmStream.Flush();
        }

        static bool CompareBytes(Stream streamA, Stream streamB, long size, out long failPos)
        {
            failPos = 0;

            if (size == 0)
                return true;

            int bufferSize = 1024;
            if (size < 1024)
                bufferSize = (int)size;

            byte[] bytesA = new byte[bufferSize];
            byte[] bytesB = new byte[bufferSize];

            for (long i = 0; i < size; i += bufferSize)
            {
                int chunkSize = (int)Math.Min(bufferSize, size - i);

                long basePos = streamA.Position;
                streamA.Read(bytesA, 0, chunkSize);
                streamB.Read(bytesB, 0, chunkSize);

                for (int j = 0; j < chunkSize; j++)
                {
                    if (bytesA[j] != bytesB[j])
                    {
                        failPos = basePos + j;
                        return false;
                    }
                }
            }

            return true;
        }

        static bool CompareLabels(Stream streamA, Stream streamB)
        {
            byte[] bytes = new byte[4];
            streamA.Read(bytes, 0, 4);

            uint streamALabel = 0;
            for (int i = 0; i < 4; i++)
                streamALabel |= (uint)(bytes[i] << (i * 8));

            streamB.Read(bytes, 0, 4);

            uint streamBLabel = 0;
            for (int i = 0; i < 4; i++)
                streamBLabel |= (uint)(bytes[i] << (i * 8));

            // Non-inline IDs must match exactly
            if (streamALabel < 1000000000 || streamBLabel < 1000000000)
                return streamALabel == streamBLabel;

            uint streamAGenIndex = streamALabel % 10000u;
            uint streamBGenIndex = streamBLabel % 10000u;

            uint streamAHigh = streamALabel / 1000000000u;
            uint streamBHigh = streamBLabel / 1000000000u;

            if (streamAGenIndex != streamBGenIndex)
                return false;

            if (streamAHigh != streamBHigh)
                return false;

            return true;
        }

        static void ValidateFile(string apePath, string sourcePath, string dparsePath, IReadOnlySet<long>? labelLocations, IDictionary<string, string> failureReasons)
        {
            if (labelLocations == null)
                throw new Exception("Internal error: No label location tracker");

            List<long> sortedLabelLocations = new List<long>();
            foreach (long labelLocation in labelLocations)
                sortedLabelLocations.Add(labelLocation);

            sortedLabelLocations.Sort();

            Console.WriteLine("Validating {0}", sourcePath);

            string recompiledPath = Path.ChangeExtension(sourcePath, "ape");
            if (File.Exists(recompiledPath))
            {
                failureReasons[sourcePath] = "Compiled output already exists";
                return;
            }

            try
            {
                List<string> args = new List<string>();

                args.Add("-q");
                args.Add(Path.GetFileName(sourcePath));

                string? sourceDir = Path.GetDirectoryName(sourcePath);
                if (sourceDir == null)
                    throw new Exception("Source path wasn't in a directory");

                ProcessStartInfo psi = new ProcessStartInfo(dparsePath, args);
                psi.WorkingDirectory = sourceDir;


                System.Diagnostics.Process? process = System.Diagnostics.Process.Start(psi);

                if (process == null)
                {
                    failureReasons[sourcePath] = "dparse didn't start";
                    return;
                }


                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    failureReasons[sourcePath] = "dparse crashed";
                    return;
                }

                using (FileStream apeFile = new FileStream(apePath, FileMode.Open, FileAccess.Read))
                {
                    using (FileStream recompiledFile = new FileStream(recompiledPath, FileMode.Open, FileAccess.Read))
                    {
                        if (apeFile.Length != recompiledFile.Length)
                        {
                            if (recompiledFile.Length == 0)
                                failureReasons[sourcePath] = "dparse compile failed";
                            else
                                failureReasons[sourcePath] = "File lengths are different";
                            return;
                        }

                        long lastReadStart = 0;
                        foreach (long labelLocation in sortedLabelLocations)
                        {
                            long chunkSize = labelLocation - lastReadStart;

                            if (chunkSize > 0)
                            {
                                long failPos = 0;
                                if (!CompareBytes(apeFile, recompiledFile, chunkSize, out failPos))
                                {
                                    failureReasons[sourcePath] = $"FAILED: File contents were different (strict check at {failPos} failed)";
                                    return;
                                }
                            }

                            if (!CompareLabels(apeFile, recompiledFile))
                            {
                                failureReasons[sourcePath] = $"FAILED: File contents were different (label compare check at {labelLocation} failed)";
                                return;
                            }

                            lastReadStart = labelLocation + 4;
                        }

                        long remainderSize = apeFile.Length - lastReadStart;
                        long remainderFailPos = 0;
                        if (!CompareBytes(apeFile, recompiledFile, remainderSize, out remainderFailPos))
                        {
                            failureReasons[sourcePath] = $"FAILED: File contents were different (trailing strict check at {remainderFailPos} failed)";
                            return;
                        }
                    }
                }

                Console.WriteLine("PASSED");
            }
            finally
            {
                if (File.Exists(recompiledPath))
                {
                    try
                    {
                        File.Delete(recompiledPath);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        static void DisassembleSingleFile(string inputPath, string outputPath, bool sourceMode, bool validateMode, string dparsePath, IDictionary<string, string> failureReasons)
        {
            IReadOnlySet<long>? labelLocations = null;

            using (FileStream inFile = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
            {
                using (FileStream outFile = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    try
                    {
                        if (sourceMode)
                            Decompile(inFile, outFile, validateMode, out labelLocations);
                        else
                            Disassemble(inFile, outFile);
                    }
                    catch (Exception)
                    {
                        outFile.Flush();
                        throw;
                    }
                }
            }

            if (validateMode && sourceMode)
            {
                ValidateFile(inputPath, outputPath, dparsePath, labelLocations, failureReasons);
            }
        }

        static void DisassembleDirectory(string inputPath, string outputPath, bool sourceMode, bool validateMode, string dparsePath, IDictionary<string, string> failureReasons)
        {
            string[] inputPathFiles = Directory.GetFiles(inputPath);

            foreach (string fullPathStr in inputPathFiles)
            {
                string fileName = Path.GetFileName(fullPathStr);
                fileName = Path.ChangeExtension(fileName, ".txt");

                string outPath = Path.Combine(outputPath, fileName);

                Console.WriteLine($"Disassembling {Path.GetFileName(fullPathStr)}");
                DisassembleSingleFile(fullPathStr, outPath, sourceMode, validateMode, dparsePath, failureReasons);
            }
        }

        static void Main(string[] args)
        {
            bool sourceMode = false;
            bool validateMode = false;
            bool dirMode = false;
            string validatePath = "";

            if (args.Length < 2)
            {
                PrintUsage();
                return;
            }

            int endOpts = 0;

            while (endOpts < args.Length)
            {
                string opt = args[endOpts];

                if (opt == "-src")
                    sourceMode = true;
                else if (opt == "-validate")
                {
                    validateMode = true;
                    endOpts++;
                    if (endOpts == args.Length)
                    {
                        PrintUsage();
                        return;
                    }

                    validatePath = args[endOpts];
                }
                else if (opt == "-dir")
                    dirMode = true;
                else if (opt.StartsWith("-") && opt != "-")
                {
                    PrintUsage();
                    return;
                }
                else
                    break;

                endOpts++;
            }

            if (args.Length - endOpts != 2)
            {
                PrintUsage();
                return;
            }

            if (validateMode && !sourceMode)
            {
                PrintUsage();
                return;
            }

            string inputPath = args[endOpts];
            string outputPath = args[endOpts + 1];

            Dictionary<string, string> failureReasons = new Dictionary<string, string>();

            if (dirMode)
                DisassembleDirectory(inputPath, outputPath, sourceMode, validateMode, validatePath, failureReasons);
            else
                DisassembleSingleFile(inputPath, outputPath, sourceMode, validateMode, validatePath, failureReasons);

            if (failureReasons.Count > 0)
            {
                Console.Error.WriteLine("Failure report:");

                List<string> sortedKeys = new List<string>();
                sortedKeys.AddRange(failureReasons.Keys);
                sortedKeys.Sort();

                foreach (string key in sortedKeys)
                {
                    string fileName = Path.GetFileName(key);
                    string reason = failureReasons[key];

                    Console.Error.WriteLine($"{fileName}: {reason}");
                }
            }
        }
    }
}
