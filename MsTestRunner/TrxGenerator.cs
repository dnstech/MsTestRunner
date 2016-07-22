// --------------------------------------------------------------------------------------------------
//  <copyright file="TrxGenerator.cs" company="DNS Technology Pty Ltd.">
//    Copyright (c) 2016 DNS Technology Pty Ltd. All rights reserved.
//  </copyright>
// --------------------------------------------------------------------------------------------------
namespace MsTestRunner
{
    using System;
    using System.Linq;
    using System.Xml.Linq;

    public static class TrxGenerator
    {
        public static void Generate(string path, TestRunResult result)
        {
            var outcome = result.Failed == 0 ? (result.Ignored == 0 ? "Completed" : "Warning") : "Failed";

            var ns = XNamespace.Get("http://microsoft.com/schemas/VisualStudio/TeamTest/2010");

            var testListId = Guid.NewGuid();
            
            var doc = new XDocument(new XElement(ns + "TestRun", 
                new XAttribute("id", Guid.NewGuid()),
                new XElement(ns + "TestSettings"),
                new XElement(ns + "Times"),
                new XElement(ns + "ResultSummary",
                    new XAttribute("outcome", outcome),
                    new XElement(ns + "Counters",
                        new XAttribute("total", result.Succeeded + result.Failed + result.Ignored),
                        new XAttribute("executed", result.Succeeded + result.Failed),
                        new XAttribute("error", 0),
                        new XAttribute("failed", result.Failed),
                        new XAttribute("timeout", 0),
                        new XAttribute("aborted", 0),
                        new XAttribute("inconclusive", 0),
                        new XAttribute("passedButRunAborted", 0),
                        new XAttribute("notRunnable", 0),
                        new XAttribute("notExecuted", 0),
                        new XAttribute("disconnected", 0),
                        new XAttribute("warning", 0),
                        new XAttribute("completed", 0),
                        new XAttribute("inProgress", 0),
                        new XAttribute("pending", 0)
                    ),
                    new XElement(ns + "RunInfos")),
                new XElement(ns + "TestDefinitions",
                    result.Tests.Select(TestDefinition)),
                new XElement(ns + "TestLists",
                    new XElement(ns + "TestList",
                        new XAttribute("name", "List"),
                        new XAttribute("id", testListId))),
                new XElement(ns + "TestEntries",
                    result.Tests.Select(test => TestEntry(test, testListId))),
                new XElement(ns + "Results",
                    result.Tests.Select(test => TestResult(test, testListId)))
                ));

            doc.Save(path);
        }

        private static XElement TestDefinition(TestResult test)
        {
            var ns = XNamespace.Get("http://microsoft.com/schemas/VisualStudio/TeamTest/2010");

            return new XElement(ns + "UnitTest",
                new XAttribute("name", test.Name),
                new XAttribute("storage", string.Empty),
                new XAttribute("id", test.Id),
                new XElement(ns + "Execution",
                    new XAttribute("id", test.ExecutionId)),
                new XElement(ns + "TestMethod",
                     new XAttribute("codeBase", string.Empty),
                     new XAttribute("adapterTypeName", string.Empty),
                     new XAttribute("className", string.Empty),
                     new XAttribute("name", test.Name)));
        }

        private static XElement TestEntry(TestResult test, Guid testListId)
        {
            var ns = XNamespace.Get("http://microsoft.com/schemas/VisualStudio/TeamTest/2010");

            return new XElement(ns + "TestEntry",
                new XAttribute("testId", test.Id),
                new XAttribute("executionId", test.ExecutionId),
                new XAttribute("testListId", testListId));
        }

        private static XElement TestResult(TestResult test, Guid testListId)
        {
            var ns = XNamespace.Get("http://microsoft.com/schemas/VisualStudio/TeamTest/2010");

            XElement output = null;

            // Assume output is error on failure and trace on success
            if (!test.Success)
            {
                output = new XElement(ns + "Output",
                    new XElement(ns + "ErrorInfo",
                        new XElement(ns + "Message",
                            test.Output)));
            }
            else if (!string.IsNullOrEmpty(test.Output))
            {
                output = new XElement(ns + "Output",
                    new XElement(ns + "DebugTrace",
                        test.Output));
            }

            return new XElement(ns + "UnitTestResult",
                new XAttribute("executionId", test.ExecutionId),
                new XAttribute("testId", test.Id),
                new XAttribute("testName", test.Name),
                new XAttribute("outcome", test.Success ? "Passed" : "Failed"),
                new XAttribute("testListId", testListId),
                new XAttribute("duration", test.Duration.ToString()),
                output);
        }
    }
}
