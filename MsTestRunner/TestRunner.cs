using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MsTestRunner
{
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Dynamic;
    using System.IO;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Security.Permissions;
    using System.Threading;

    public sealed class TestRunner
    {
        private readonly HashSet<string> fileNames = new HashSet<string>();

        private readonly List<Func<int>> testList = new List<Func<int>>();

        public void AddTestAssembly(string filePath)
        {
            var testClasses = new List<Type>();
            if (this.fileNames.Add(Path.GetFileName(filePath)))
            {
                try
                {
                    var allTypes = Assembly.LoadFrom(Path.GetFullPath(filePath)).GetTypes();
                    foreach (var type in allTypes)
                    {
                        if (type.GetCustomAttributes().Any(c => c.GetType().Name == "TestClassAttribute") && !type.GetCustomAttributes().Any(c => c.GetType().Name == "IgnoreAttribute"))
                        {
                            testClasses.Add(type);
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine("Ignoring {0} due to file exception", filePath);
                    return;
                }
            }

            this.AddTestClasses(testClasses);
        }

        public void AddTestClasses(IEnumerable<Type> testClassTypes)
        {
            foreach (var testClassType in testClassTypes)
            {
                var ci = testClassType.GetMethods().FirstOrDefault(m => m.IsStatic && m.GetCustomAttributes().Any(c => c.GetType().Name == "ClassInitializeAttribute"));
                if (ci != null)
                {
                    ci.Invoke(null, new object[] { null });
                }

                if (!testClassType.IsAbstract)
                {
                    this.testList.Add(this.CreateTestInvoker(testClassType));
                }
            }
        }

        public TestRunResult Execute()
        {
            var result = new TestRunResult();
            result.Start();
            Parallel.ForEach(
                this.testList,
                a =>
                    {
                        try
                        {
                            var testCount = a();
                            result.Success(testCount);
                        }
                        catch (Exception e)
                        {
                            result.Failure(e.ToString());
                        } 
                    });
            return result;
        }

        private Func<int> CreateTestInvoker(Type testClassType)
        {
            var v = Expression.Variable(testClassType, "test");
            var allMethods = testClassType.GetRuntimeMethods().ToList();
            var testMethods = new List<Expression>(allMethods.Count);
            testMethods.Add(Expression.Assign(v, Expression.New(testClassType.GetConstructor(new Type[0]))));

            var initMethod =
                allMethods.FirstOrDefault(
                    a => a.GetCustomAttributes().Any(c => c.GetType().Name == "TestInitializeAttribute"));
            if (initMethod != null)
            {
                testMethods.Add(Expression.Call(v, initMethod));
            }

            var testCount = 0;
            for (int i = 0; i < allMethods.Count; i++)
            {
                var m = allMethods[i];
                if (m.ReturnType == typeof(void) && m.GetCustomAttributes().Any(c => c.GetType().Name == "TestMethodAttribute") && !m.GetCustomAttributes().Any(c => c.GetType().Name == "IgnoreAttribute"))
                {
                    testMethods.Add(Expression.Call(v, m));
                    testCount++;
                }
            }

            testMethods.Add(Expression.Constant(testCount));
            return Expression.Lambda<Func<int>>(Expression.Block(new[] { v }, testMethods)).Compile();
        }
    }

    public sealed class TestRunResult
    {
        private Stopwatch timer;

        private long succeeded;
        private long failed;
        private long ignored;

        public TestRunResult()
        {
            this.FailureMessages = new ConcurrentBag<string>();
        }

        public long Succeeded
        {
            get
            {
                return Interlocked.Read(ref this.succeeded);
            }
        }

        public long Ignored
        {
            get
            {
                return Interlocked.Read(ref this.ignored);
            }
        }

        public long Failed
        {
            get
            {
                return Interlocked.Read(ref this.failed);
            }
        }

        public TimeSpan TimeTaken
        {
            get
            {
                return this.timer.Elapsed;
            }
        }

        public ConcurrentBag<string> FailureMessages { get; private set; }

        public void Success(int testCount)
        {
            Interlocked.Add(ref this.succeeded, testCount);
        }

        public void Failure(string message)
        {
            Interlocked.Increment(ref this.failed);
            this.FailureMessages.Add(message);
        }

        public void Start()
        {
            this.timer = Stopwatch.StartNew();
        }

        public void Stop()
        {
            this.timer.Stop();
        }
    }
}
