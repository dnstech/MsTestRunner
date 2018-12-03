namespace MsTestRunner.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;

    public sealed class TestRunResult
    {
        #region Fields

        private long failed;

        private long ignored;

        private long succeeded;

        private Stopwatch timer;

        private readonly bool quiet;

        private readonly ConcurrentQueue<string> failureMessagesQueue = new ConcurrentQueue<string>();

        private readonly ConcurrentQueue<string> passedTestsQueue = new ConcurrentQueue<string>();

        private readonly ConcurrentDictionary<string, bool> failedTests = new ConcurrentDictionary<string, bool>();

        private readonly ConcurrentBag<TestResult> tests = new ConcurrentBag<TestResult>();

        #endregion

        public TestRunResult(bool quiet)
        {
            this.quiet = quiet;
            this.FailureMessages = new List<string>();
        }

        #region Public Properties

        public long Failed
        {
            get
            {
                return Interlocked.Read(ref this.failed);
            }
        }

        public List<TestResult> Tests
        {
            get
            {
                return this.tests.ToList();
            }
        }

        public IList<string> FailureMessages { get; private set; }

        public long Ignored
        {
            get
            {
                return Interlocked.Read(ref this.ignored);
            }
        }

        public long Succeeded
        {
            get
            {
                return Interlocked.Read(ref this.succeeded);
            }
        }

        public TimeSpan TimeTaken
        {
            get
            {
                return this.timer.Elapsed;
            }
        }

        #endregion

        #region Public Methods and Operators

        public void Failure(TestItem item, string methodName, Exception error)
        {
            string errorMessage;

            if (error == null)
            {
                errorMessage = "Null error???";
            }
            else if (error is AssertFailedException)
            {
                errorMessage = error.Source + " - " + error.Message;
            }
            else
            {
                if (error.GetType().Name.StartsWith("AssertFailed"))
                {
                    errorMessage = error.Source + " - " + error.Message;
                }
                else
                {
                    errorMessage = error.Source + " - " + error;
                }
            }

            if (string.IsNullOrEmpty(methodName) || methodName == "ctor")
            {
                foreach (var m in item.Tests)
                {
                    var key = item.Name + "." + m;
                    this.failedTests.GetOrAdd(key, true);
                    this.tests.Add(new TestResult(key, errorMessage, false, TimeSpan.FromSeconds(1)));
                }
            }
            else
            {
                var key = item.Name + "." + methodName;
                this.failedTests.GetOrAdd(key, true);
                this.tests.Add(new TestResult(key, errorMessage, false, TimeSpan.FromSeconds(1)));
            }

            this.ReportFailure(item.Name + "." + methodName + " - " + errorMessage);
        }

        private void ReportFailure(string message)
        {
            Interlocked.Increment(ref this.failed);
            this.failureMessagesQueue.Enqueue(message);
            if (!this.quiet)
            {
                var c = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("x");
                Console.ForegroundColor = c;
            }
        }

        public void Start()
        {
            this.timer = Stopwatch.StartNew();
        }

        public void Stop()
        {
            this.timer.Stop();
            this.FailureMessages = this.failureMessagesQueue.ToList();
            if (!this.quiet)
            {
                Console.WriteLine();
            }
        }

        public void Success(TestItem item, int testCount)
        {
            foreach (var m in item.Tests)
            {
                var key = item.Name + "." + m;
                if (!this.failedTests.ContainsKey(key))
                {
                    this.passedTestsQueue.Enqueue(key);
                    this.tests.Add(new TestResult(key, null, true, TimeSpan.FromSeconds(1)));
                }
                else
                {
                    // No tests following here have been run
                    // should be ignored/not run
                    break;
                }
            }

            Interlocked.Add(ref this.succeeded, testCount);
            if (testCount > 0 && !this.quiet)
            {
                var c = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(".");
                Console.ForegroundColor = c;
            }
        }

        #endregion
    }
}
