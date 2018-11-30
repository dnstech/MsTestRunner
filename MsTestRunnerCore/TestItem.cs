namespace MsTestRunner
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public sealed class TestItem
    {
        public TestItem(string name, Func<TestItem, TestRunResult, Task<int>> execute, IReadOnlyList<string> tests)
        {
            if (execute == null)
            {
                throw new ArgumentNullException("execute");
            }

            this.Name = name;
            this.Execute = execute;
            this.Tests = tests;
        }

        public readonly string Name;

        public readonly Func<TestItem, TestRunResult, Task<int>> Execute;

        public IReadOnlyList<string> Tests
        {
            get;
            private set;
        }
    }
}