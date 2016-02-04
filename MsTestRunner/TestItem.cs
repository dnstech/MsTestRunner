namespace MsTestRunner
{
    using System;

    internal struct TestItem
    {
        public TestItem(string name, Func<int> execute)
        {
            this.Name = name;
            this.Execute = execute;
        }

        public readonly string Name;

        public readonly Func<int> Execute;
    }
}