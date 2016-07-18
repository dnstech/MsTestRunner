namespace MsTestRunner
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

        public List<string> PassedTests
        {
            get;
            private set;
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
            if (error == null)
            {
                this.ReportFailure(item.Name + " - Null error???");
                return;
            }

            if (error is AssertFailedException)
            {
                this.ReportFailure(item.Name + "." + methodName + " - " + error.Source + " - " + error.Message);
            }
            else
            {
                if (error.GetType().Name.StartsWith("AssertFailed"))
                {
                    this.ReportFailure(item.Name + "." + methodName + " - " + error.Message);
                }
                else
                {
                    this.ReportFailure(item.Name + "." + methodName + " - " + error);
                }
            }
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
                this.passedTestsQueue.Enqueue(item.Name + "." + m);
            }

            Interlocked.Add(ref this.succeeded, testCount);
            if (!this.quiet)
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