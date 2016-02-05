// --------------------------------------------------------------------------------------------------
//  <copyright file="TestRunner.cs" company="DNS Technology Pty Ltd.">
//    Copyright (c) 2016 DNS Technology Pty Ltd. All rights reserved.
//  </copyright>
// --------------------------------------------------------------------------------------------------

namespace MsTestRunner
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading.Tasks;

    public sealed class TestRunner
    {
        private static readonly MethodInfo FailureMethod = typeof(TestRunResult).GetMethod("Failure", new[] { typeof(TestItem), typeof(Exception) });

        #region Fields

        private readonly Dictionary<string, string> deploymentFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> testAssemblyFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly string path;

        private readonly List<TestItem> testList = new List<TestItem>();

        private readonly List<string> filters = new List<string>();

        #endregion

        #region Constructors and Destructors

        public TestRunner(string path)
        {
            this.path = path;
            this.Parallelism = 4;
        }

        #endregion

        public int Parallelism { get; set; }

        public void AddFilter(string text)
        {
            this.filters.Add(text);
        }

        #region Public Methods and Operators

        public void AddTestAssembly(string filePath)
        {
            var testClasses = new List<Type>();
            if (this.testAssemblyFileNames.Add(Path.GetFileName(filePath)))
            {
                try
                {
                    var assemblyToLoad = Path.GetFullPath(filePath);
                    var allTypes = Assembly.LoadFrom(assemblyToLoad).GetTypes();
                    foreach (var type in allTypes)
                    {
                        if (IsTestClass(type) && !IsIgnored(type) && this.IsIncludedInFilter(type))
                        {
                            var deploymentItems = DeploymentItems(type);
                            this.IncludeDeploymentItems(assemblyToLoad, deploymentItems);
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
                if (!testClassType.IsAbstract)
                {
                    var ci = testClassType.GetMethods().FirstOrDefault(IsClassInitialize);
                    if (ci != null)
                    {
                        ci.Invoke(
                            null,
                            new object[]
                            {
                                null
                            });
                    }

                    this.testList.Add(new TestItem(testClassType.FullName, this.CreateTestInvoker(testClassType)));
                }
            }
        }

        public TestRunResult Execute()
        {
            Directory.CreateDirectory(this.path);
            Directory.SetCurrentDirectory(this.path);
            this.CopyDeploymentFiles();
            var result = new TestRunResult();
            result.Start();
            Parallel.ForEach(
                this.testList,
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = this.Parallelism
                },
                async a =>
                    {
                        var testCount = await a.Execute(a, result);
                        result.Success(testCount);
                    });
            result.Stop();
            return result;
        }

        #endregion

        #region Methods

        private static IEnumerable<Tuple<string, string>> DeploymentItems(Type type)
        {
            return type.GetCustomAttributes(true).Where(c => c.GetType().Name == "DeploymentItemAttribute").Select(
                d =>
                {
                    dynamic item = d;
                    return Tuple.Create((string)item.Path, (string)item.OutputDirectory);
                });
        }

        private static bool IsTestClass(Type type)
        {
            return type.GetCustomAttributes().Any(c => c.GetType().Name == "TestClassAttribute");
        }

        private static bool IsIgnored(Type type)
        {
            return type.GetCustomAttributes().Any(c => c.GetType().Name == "IgnoreAttribute");
        }

        private static bool IsClassInitialize(MethodInfo m)
        {
            return m.IsStatic && m.GetCustomAttributes().Any(c => c.GetType().Name == "ClassInitializeAttribute");
        }

        private void IncludeDeploymentItems(string filePath, IEnumerable<Tuple<string, string>> deploymentItems)
        {
            foreach (var deploymentItem in deploymentItems)
            {
                var copyFromFolder = Path.GetDirectoryName(filePath) ?? string.Empty;
                var binIndex = copyFromFolder.LastIndexOf("\\bin\\", StringComparison.InvariantCultureIgnoreCase);
                if (binIndex != -1)
                {
                    copyFromFolder = copyFromFolder.Substring(0, binIndex);
                }

                var sourcePath = Path.Combine(copyFromFolder, deploymentItem.Item1);
                if (!this.deploymentFiles.ContainsKey(sourcePath))
                {
                    var destPath = Path.Combine(this.path, deploymentItem.Item2 ?? string.Empty, Path.GetFileName(deploymentItem.Item1));
                    this.deploymentFiles[sourcePath] = destPath;
                }
            }
        }

        private bool IsIncludedInFilter(Type type)
        {
            return this.filters.Count == 0 || this.filters.Any(f => type.FullName.IndexOf(f, StringComparison.OrdinalIgnoreCase) != -1);
        }

        private void CopyDeploymentFiles()
        {
            foreach (var filePath in this.deploymentFiles)
            {
                if (File.Exists(filePath.Value))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("The Deployment Item {0} has the same File Name as another Deployment Item, please add an output directory path to each of the deployment items", filePath.Value);
                }
                else
                {
                    if (!File.Exists(filePath.Key))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("The Deployment File {0} was not found", filePath.Key);
                    }
                    else
                    {
                        File.Copy(filePath.Key, filePath.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a single
        /// </summary>
        /// <param name="testClassType"></param>
        /// <returns></returns>
        private Func<TestItem, TestRunResult, Task<int>> CreateTestInvoker(Type testClassType)
        {
            var testItemInstance = Expression.Parameter(typeof(TestItem), "testItem");
            var testClassInstance = Expression.Variable(testClassType, "test");
            var resultsParameter = Expression.Parameter(typeof(TestRunResult), "results");
            var allMethods = testClassType.GetRuntimeMethods().ToList();
            var testMethods = new List<Expression>(allMethods.Count)
                              {
                                  Expression.Assign(testClassInstance, Expression.New(testClassType.GetConstructor(new Type[0])))
                              };

            var initMethods = allMethods.Where(IsTestInitialize).ToList();
            if (initMethods.Count() > 1)
            {
                testMethods.Add(
                    Expression.Throw(
                        Expression.New(
                            typeof(InvalidOperationException).GetConstructor(
                                new Type[]
                                {
                                    typeof(string)
                                }),
                            Expression.Constant(string.Format("The test {0} has more than 1 method decorated with the [TestInitialize] attribute", testClassType.Name)))));
            }
            else
            {
                var initMethod = initMethods.FirstOrDefault();
                if (initMethod != null)
                {
                    testMethods.Add(Expression.Call(testClassInstance, initMethod));
                }
            }

            var testCount = 0;
            for (int i = 0; i < allMethods.Count; i++)
            {
                var m = allMethods[i];
                if (IsTestMethod(m) && !IsIgnored(m))
                {
                    var expectedExceptionType = ExpectedExceptions(m).FirstOrDefault();
                    if (expectedExceptionType != null)
                    {
                        var tryBlock = Expression.Block(
                            Expression.Call(testClassInstance, m),
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
                        testMethods.Add(Expression.Call(testClassInstance, m));
                    }

                    testCount++;
                }
            }
            
            testMethods.Add(Expression.Constant(Task.FromResult(testCount)));
            var parameters = new[]
                             {
                                 resultsParameter,
                                 testClassInstance
                             };

            Expression finallyCall = CleanupCall(testClassType, resultsParameter, testItemInstance, testClassInstance, allMethods);

            var failureExceptionParameter = Expression.Parameter(typeof(Exception));
            var tryCatchFinally = Expression.TryCatchFinally(
                Expression.Block(parameters, testMethods), 
                finallyCall, 
                Expression.Catch(failureExceptionParameter, 
                    Expression.Block(parameters,     
                        Expression.Call(
                                resultsParameter,
                                FailureMethod,
                                testItemInstance,
                                failureExceptionParameter),
                        Expression.Constant(Task.FromResult(0)))));
            return Expression.Lambda<Func<TestItem, TestRunResult, Task<int>>>(tryCatchFinally, new[] { testItemInstance, resultsParameter }).Compile();
        }

        private static Expression CleanupCall(Type testClassType, ParameterExpression resultsParameter, ParameterExpression testItemInstance, ParameterExpression testClassInstance, List<MethodInfo> allMethods)
        {
            var cleanupMethods = allMethods.Where(IsTestCleanup).ToList();
            if (cleanupMethods.Count() > 1)
            {
                var exceptionExpression = Expression.New(
                    typeof(InvalidOperationException).GetConstructor(
                        new[]
                        {
                            typeof(string)
                        }),
                    Expression.Constant(string.Format("The test {0} has more than 1 method decorated with the [TestCleanup] attribute", testClassType.Name)));
                return Expression.Call(
                    resultsParameter,
                    FailureMethod,
                    testItemInstance,
                    exceptionExpression);
            }

            var cleanupMethod = cleanupMethods.FirstOrDefault();
            if (cleanupMethod != null)
            {
                var parameters = new[]
                             {
                                 resultsParameter,
                                 testClassInstance
                             };
                return Expression.Block(parameters, Expression.IfThen(Expression.ReferenceNotEqual(testClassInstance, Expression.Constant(null)), Expression.Call(testClassInstance, cleanupMethod)));
            }

            return Expression.Empty();
        }

        private static bool IsTestCleanup(MethodInfo m)
        {
            return m.GetCustomAttributes().Any(c => c.GetType().Name == "TestCleanupAttribute");
        }

        private static IEnumerable<Type> ExpectedExceptions(MethodInfo m)
        {
            return m.GetCustomAttributes().Where(c => c.GetType().Name == "ExpectedExceptionAttribute").Select(c => (Type)((dynamic)c).ExceptionType);
        }

        private static bool IsTestInitialize(MethodInfo m)
        {
            return m.GetCustomAttributes().Any(c => c.GetType().Name == "TestInitializeAttribute");
        }

        private static bool IsTestMethod(MethodInfo m)
        {
            return m.ReturnType == typeof(void) && m.GetCustomAttributes().Any(c => c.GetType().Name == "TestMethodAttribute");
        }

        private static bool IsIgnored(MethodInfo m)
        {
            return m.GetCustomAttributes().Any(c => c.GetType().Name == "IgnoreAttribute");
        }

        #endregion
    }
}