using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MsTestRunner
{
    using System.IO;
    using System.Reflection;

    class Program
    {
        static void Main(string[] args)
        {
            var testRunner = new TestRunner();
            foreach (var path in args)
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

            var result = testRunner.Execute();
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine("Took {0} to Run {1} Tests", result.TimeTaken, result.Succeeded + result.Failed);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("{0} Suceeded", result.Succeeded);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("{0} Failed", result.Failed);

            Console.ForegroundColor = ConsoleColor.White;
            foreach (var failure in result.FailureMessages)
            {
                Console.WriteLine(failure);
                Console.WriteLine();
                Console.ReadKey();
            }

            Console.ForegroundColor = ConsoleColor.Gray;

            Console.ReadKey();
        }
    }
}
