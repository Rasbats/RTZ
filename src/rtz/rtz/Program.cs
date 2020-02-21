﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace rtz
{
    class Program
    {
        static int Usage()
        {
            Console.WriteLine("RTZ -zip rtz-filename [rtzp filename]");
            Console.WriteLine("\tMakes an RTZP container with the RTZ file and attachments");
            Console.WriteLine();
            Console.WriteLine("RTZ -unzip rtzp-filename [folder]");
            Console.WriteLine("\tExtracts contents of an RTZP to a given folder or defaults to filename of RTZP as folder name");
            Console.WriteLine();
            Console.WriteLine("rtz -check <rtz or rtzp filename> [report destination] [-terse]");
            Console.WriteLine("\tChecks file against standard");
            return 1;
        }

        // Zero return means success
        // 1 - Bad parameters
        // 2 - Check failed
        // 3 - File not found
        // 666- Error occurred
        static int Main(string[] args)
        {
            try
            {
                return SafeMain(args);
            }
            catch (Exception x)
            {
                Console.WriteLine(x.ToString());
                return 666;
            }
        }

        static int SafeMain(string[] args)
        {
            // Get terse flag then "delete" it from args to leave future parsing alone
            bool terse = args.Contains("-terse", StringComparer.InvariantCultureIgnoreCase);
            args = args.Where(a => !a.Equals("-terse", StringComparison.InvariantCultureIgnoreCase)).ToArray();
            
            if (args.Length < 2) return Usage();
            string target = args[1]; // 2nd param always a file
            string destination = args.Length >= 3 ? args[2] : string.Empty;

            if (IsCommand(args,"zip"))
            {
                if (!File.Exists(target))
                {
                    return FileNotFound(target);
                }

                Zipper.Zip(target, destination);
            }
            else if (IsCommand(args, "unzip"))
            {
                if (!File.Exists(target))
                {
                    return FileNotFound(target);
                }

                UnzipCommand(target, destination);
            }
            else if (IsCommand(args, "check"))
            {
                if (Directory.Exists(target))
                {
                    return CheckFolderCommand(target, destination, terse);
                }

                if (!File.Exists(target))
                {
                    return FileNotFound(target);
                }

                return CheckCommand(target, destination, terse);
            }
            else 
            {
                return Usage();
            }

            return 0;
        }

        private static void UnzipCommand(string target, string destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                // Destination will be a folder same name as RTZP file
                var path = Path.GetDirectoryName(target);
                destination = Path.Combine(path, Path.GetFileNameWithoutExtension(target));
                if (Directory.Exists(destination))
                    Directory.Delete(destination, true);
            }

            Directory.CreateDirectory(destination);

            Console.WriteLine($"Extracting {target}");
            Console.WriteLine($"To destination {destination}");

            ZipFile.ExtractToDirectory(target, destination);

            Console.WriteLine("Successful");
        }

        private static int CheckCommand(string target, string destination, bool terse)
        {
            var checker = new Checker(target);

            string report = ReportOrTerse(checker, terse);

            if (!string.IsNullOrEmpty(destination))
            {
                Console.WriteLine($"Writing report to {destination}");
                File.WriteAllText(destination, report);
            }
            else
            {
                Console.WriteLine(report);
            }

            return checker.Passed ? 0 : 2;
        }

        private static int CheckFolderCommand(string target, string destination, bool terse)
        {
            var rtz = Directory.EnumerateFiles(target, "*.rtz", SearchOption.AllDirectories);
            var rtzp = Directory.EnumerateFiles(target, "*.rtzp", SearchOption.AllDirectories);
            var combined = rtz.Concat(rtzp);

            if (!combined.Any())
            {
                Console.WriteLine($"No files to check in {target}");
                return 3;
            }

            int result = 0;
            var sb = new StringBuilder();

            foreach (string filename in combined)
            {
                var checker = new Checker(filename);

                string report = ReportOrTerse(checker, terse);

                if (!string.IsNullOrEmpty(destination))
                {
                    Console.WriteLine($"Checked {filename}");
                    sb.AppendLine(report);
                }
                else
                {
                    Console.WriteLine(report);
                }

                if (!checker.Passed)
                {
                    result = 2;
                }
            }

            if (!string.IsNullOrEmpty(destination))
            {
                File.WriteAllText(destination, sb.ToString());
                Console.WriteLine($"Wrote report to {destination}");
            }

            return result;
        }

        private static string ReportOrTerse(Checker checker, bool terse)
        {
            if (terse)
            {
                return $"{(checker.Passed ? "Pass" : "Fail")}\t{checker.Errors.Count} errors {checker.Warnings.Count} warnings\t{Ellipsis(checker.Filename, 60)}";
            }
            else
            {
                return checker.Report();
            }
        }

        private static int FileNotFound(string target)
        {
            Console.WriteLine($"File not found {target}");
            Console.WriteLine();
            Usage();
            return 3;
        }

        static bool IsCommand(string[] args, string commandName)
        {
            string command = args[0];
            return command.Equals("-" + commandName, StringComparison.InvariantCultureIgnoreCase);
        }

        private static string Ellipsis(string input, int maxlen)
        {
            if (input.Length > maxlen)
            {
                int start = input.Length - (maxlen - 3);
                int number = maxlen - 3;

                return "..." + input.Substring(start, number);
            }

            return input;
        }

    }
}
