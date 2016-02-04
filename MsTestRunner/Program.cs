// --------------------------------------------------------------------------------------------------
//  <copyright file="Program.cs" company="DNS Technology Pty Ltd.">
//    Copyright (c) 2015 DNS Technology Pty Ltd. All rights reserved.
//  </copyright>
// --------------------------------------------------------------------------------------------------

namespace MsTestRunner
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    internal class Program
    {
        #region Methods

        private static void Main(string[] args)
        {
            var testRunner = new TestRunner(Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss")));
            var filter = false;
            foreach (var path in args)
            {
                if (filter)
                {
                    filter = false;
                    testRunner.AddFilter(path);
                }
                else if (path.Equals("-f", StringComparison.OrdinalIgnoreCase))
                {
                    filter = true;
                }
                else if (!path.StartsWith("-"))
                {
                    if (File.Exists(Path.GetFullPath(path)))
                    {
                        testRunner.AddTestAssembly(path);
                    }
                    else if (Directory.Exists(Path.GetFullPath(path)))
                    {
                        foreach (var filePath in Directory.EnumerateFiles(path, "*Tests.dll", SearchOption.AllDirectories))
                        {
                            testRunner.AddTestAssembly(filePath);
                        }
                    }
                }
            }

            var result = testRunner.Execute();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Took {0} to Run {1} Tests", result.TimeTaken, result.Succeeded + result.Failed);
            if (result.Succeeded > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("{0} Suceeded", result.Succeeded);
            }

            if (result.Failed > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("{0} Failed", result.Failed);
            }
                
            Console.ForegroundColor = ConsoleColor.White;

            var interactive = args.Contains("-i", StringComparer.OrdinalIgnoreCase);
            if (interactive)
            {
                InteractiveMode(result);
            }
            else
            {
                ListFailures(result);
            }

            Console.ForegroundColor = ConsoleColor.Gray;

            if (Debugger.IsAttached)
            {
                Console.ReadKey();
            }
        }

        private static void ListFailures(TestRunResult result)
        {
            for (int i = 0; i < result.FailureMessages.Count; i++)
            {
                Console.WriteLine("Failure #{0} of {1}", i + 1, result.FailureMessages.Count);
                Console.WriteLine(result.FailureMessages[i]);
                Console.WriteLine();
            }
        }

        private static void InteractiveMode(TestRunResult result)
        {
            var currentFailure = 0;
            var lastKey = Console.ReadKey().Key;
            while (lastKey != ConsoleKey.Escape && currentFailure < result.FailureMessages.Count)
            {
                Console.WriteLine("Failure #{0} of {1}", currentFailure + 1, result.FailureMessages.Count);
                Console.WriteLine(result.FailureMessages[currentFailure]);
                Console.WriteLine();
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.UpArrow && currentFailure > 0)
                {
                    Console.Clear();
                    currentFailure--;
                }
                else
                {
                    if (key.Key == ConsoleKey.DownArrow)
                    {
                        Console.Clear();
                        currentFailure++;
                    }
                }
            }

            Console.ForegroundColor = ConsoleColor.Gray;

            Console.ReadKey();
        }

        #endregion
    }
}