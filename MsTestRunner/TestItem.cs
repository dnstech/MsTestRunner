﻿namespace MsTestRunner
{
    using System;
    using System.Threading.Tasks;

    public sealed class TestItem
    {
        public TestItem(string name, Func<TestItem, TestRunResult, Task<int>> execute)
        {
            if (execute == null)
            {
                throw new ArgumentNullException("execute");
            }

            this.Name = name;
            this.Execute = execute;
        }

        public readonly string Name;

        public readonly Func<TestItem, TestRunResult, Task<int>> Execute;
    }
}