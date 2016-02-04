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

        private readonly ConcurrentQueue<string> failureMessagesQueue = new ConcurrentQueue<string>();

        #endregion

        #region Public Properties

        public long Failed
        {
            get
            {
                return Interlocked.Read(ref this.failed);
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

        public void Failure(string message)
        {
            Interlocked.Increment(ref this.failed);
            this.failureMessagesQueue.Enqueue(message);
        }

        public void Start()
        {
            this.timer = Stopwatch.StartNew();
        }

        public void Stop()
        {
            this.FailureMessages = this.failureMessagesQueue.ToList();
            this.timer.Stop();
        }

        public void Success(int testCount)
        {
            Interlocked.Add(ref this.succeeded, testCount);
        }

        #endregion
    }
}