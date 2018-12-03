// --------------------------------------------------------------------------------------------------
//  <copyright file="Program.cs" company="DNS Technology Pty Ltd.">
//    Copyright (c) 2015 DNS Technology Pty Ltd. All rights reserved.
//  </copyright>
// --------------------------------------------------------------------------------------------------

namespace MsTestRunner
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using MsTestRunner.Common;

    internal class Program
    {
        #region Methods

        private static int Main(string[] args)
        {
            var testRunner = new TestRunner(Path.Combine(Environment.CurrentDirectory, "TestResults", DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss")));
            var filter = false;
            var parallelism = false;
            string trxPath = null;

            var testAssemblies = new List<string>();
            foreach (var arg in args)
            {
                if (filter)
                {
                    filter = false;
                    testRunner.AddFilter(arg);
                }
                else if (parallelism)
                {
                    parallelism = false;
                    testRunner.Parallelism = int.Parse(arg);
                }
                else if (arg.Equals("-q", StringComparison.OrdinalIgnoreCase))
                {
                    testRunner.QuietMode = true;
                }
                else if (arg.Equals("-f", StringComparison.OrdinalIgnoreCase))
                {
                    filter = true;
                }
                else if (arg.Equals("-p", StringComparison.OrdinalIgnoreCase))
                {
                    parallelism = true;
                }
                else if (arg.StartsWith("/resultsfile:", StringComparison.OrdinalIgnoreCase))
                {
                    trxPath = arg.Substring("/resultsfile:".Length);
                }
                else if (!arg.StartsWith("-"))
                {
                    var path = arg.StartsWith("/testcontainer:") ? arg.Substring("/testcontainer:".Length) : arg;

                    if (File.Exists(Path.GetFullPath(path)))
                    {
                        testAssemblies.Add(path);
                    }
                    else if (Directory.Exists(Path.GetFullPath(path)))
                    {
                        foreach (var filePath in Directory.EnumerateFiles(path, "*Tests.dll", SearchOption.AllDirectories))
                        {
                            testAssemblies.Add(filePath);
                        }
                    }
                }
            }

            foreach (var assemblyFile in testAssemblies)
            {
                testRunner.AddTestAssembly(assemblyFile);
            }

            var result = testRunner.Execute();

            if (trxPath != null)
            {
                TrxGenerator.Generate(trxPath, result);
            }

            Console.ForegroundColor = ConsoleColor.Gray;

            Console.WriteLine("Took {0} to Run {1} Tests Completed at {2}", result.TimeTaken, result.Succeeded + result.Failed, DateTime.Now.ToString("s"));
            if (result.Succeeded > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("{0} Succeeded", result.Succeeded);
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

            return result.Failed < int.MaxValue ? (int)result.Failed : int.MaxValue;
        }

        private static void ListFailures(TestRunResult result)
        {
            for (int i = 0; i < result.FailureMessages.Count; i++)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failure #{0} of {1}", i + 1, result.FailureMessages.Count);
                Console.ForegroundColor = ConsoleColor.White;
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