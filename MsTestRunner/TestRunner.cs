// --------------------------------------------------------------------------------------------------
//  <copyright file="TestRunner.cs" company="DNS Technology Pty Ltd.">
//    Copyright (c) 2015 DNS Technology Pty Ltd. All rights reserved.
//  </copyright>
// --------------------------------------------------------------------------------------------------

namespace MsTestRunner
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class TestRunner
    {
        #region Fields

        private readonly HashSet<string> fileNames = new HashSet<string>();

        private readonly string path;

        private readonly List<Func<int>> testList = new List<Func<int>>();

        #endregion

        #region Constructors and Destructors

        public TestRunner(string path)
        {
            this.path = path;
            Directory.CreateDirectory(this.path);
        }

        #endregion

        #region Public Methods and Operators

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
                            var deploymentItems = type.GetCustomAttributes().Where(c => c.GetType().Name == "DeploymentItemAttribute");
                            foreach (var deploymentItem in deploymentItems.Select(
                                d =>
                                    {
                                        dynamic item = d;
                                        return Tuple.Create((string)item.Path, (string)item.OutputDirectory);
                                    }))
                            {
                                var sourcePath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileName(deploymentItem.Item1));
                                var destPath = Path.Combine(this.path, deploymentItem.Item2 ?? string.Empty, Path.GetFileName(deploymentItem.Item1));

                                if (File.Exists(destPath))
                                {
                                    Trace.TraceWarning(
                                        "The Deployment Item {0} on {1} has the same File Name as another Deployment Item, please add an output directory path to each of the deployment items",
                                        deploymentItem.Item1,
                                        type.FullName);
                                }
                                else
                                {
                                    if (!File.Exists(sourcePath))
                                    {
                                        Trace.TraceWarning(
                                            "The Deployment File {0} for {1} was not found",
                                            sourcePath,
                                            type.FullName);
                                    } 
                                    else 
                                    {
                                        File.Copy(sourcePath, destPath);        
                                    }
                                    
                                }
                            }

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
                    ci.Invoke(
                        null,
                        new object[]
                        {
                            null
                        });
                }

                if (!testClassType.IsAbstract)
                {
                    this.testList.Add(this.CreateTestInvoker(testClassType));
                }
            }
        }

        public TestRunResult Execute()
        {
            Directory.SetCurrentDirectory(this.path);
            var result = new TestRunResult();
            result.Start();
            Parallel.ForEach(
                this.testList,
                new ParallelOptions() { MaxDegreeOfParallelism = 1 },
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
            result.Stop();
            return result;
        }

        #endregion

        #region Methods

        private Func<int> CreateTestInvoker(Type testClassType)
        {
            var v = Expression.Variable(testClassType, "test");
            var allMethods = testClassType.GetRuntimeMethods().ToList();
            var testMethods = new List<Expression>(allMethods.Count);
            testMethods.Add(Expression.Assign(v, Expression.New(testClassType.GetConstructor(new Type[0]))));

            var initMethod = allMethods.FirstOrDefault(a => a.GetCustomAttributes().Any(c => c.GetType().Name == "TestInitializeAttribute"));
            if (initMethod != null)
            {
                testMethods.Add(Expression.Call(v, initMethod));
            }

            var testCount = 0;
            for (int i = 0; i < allMethods.Count; i++)
            {
                var m = allMethods[i];
                if (m.ReturnType == typeof(void) && m.GetCustomAttributes().Any(c => c.GetType().Name == "TestMethodAttribute")
                    && !m.GetCustomAttributes().Any(c => c.GetType().Name == "IgnoreAttribute"))
                {
                    var expectedExceptionType = m.GetCustomAttributes().Where(c => c.GetType().Name == "ExpectedExceptionAttribute").Select(c => (Type)((dynamic)c).ExceptionType).FirstOrDefault();
                    if (expectedExceptionType != null)
                    {
                        var tryBlock = Expression.Block(
                            Expression.Call(v, m),
                            Expression.Throw(
                                Expression.New(
                                    typeof(AssertFailedException).GetConstructor(
                                        new Type[]
                                        {
                                            typeof(string)
                                        }),
                                    Expression.Constant(string.Format("Expected exception {0} was not thrown", expectedExceptionType.Name)))));
                        var tryCatch = Expression.TryCatch(tryBlock, Expression.Catch(expectedExceptionType, Expression.Empty()));
                        testMethods.Add(tryCatch);
                    }
                    else
                    {
                        testMethods.Add(Expression.Call(v, m));
                    }

                    testCount++;
                }
            }

            testMethods.Add(Expression.Constant(testCount));
            return Expression.Lambda<Func<int>>(
                Expression.Block(
                    new[]
                    {
                        v
                    },
                    testMethods)).Compile();
        }

        #endregion
    }

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