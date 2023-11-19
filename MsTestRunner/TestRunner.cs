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
    using System.Text;
    using System.Threading.Tasks;

    public sealed class TestRunner
    {
        private static readonly MethodInfo FailureMethod = typeof(TestRunResult).GetMethod("Failure", new[] { typeof(TestItem), typeof(string), typeof(Exception) });

        private static readonly MethodInfo TaskWaitMethod = typeof(Task).GetMethod("Wait", new Type[0]);

        #region Fields

        private readonly Dictionary<string, string> deploymentFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> testAssemblyFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly string path;

        private readonly List<TestItem> testList = new List<TestItem>();

        private readonly List<string> filters = new List<string>();

        private TextWriter originalConsoleOut;

        #endregion

        #region Constructors and Destructors

        public TestRunner(string path)
        {
            this.path = path;
            this.Parallelism = 4;
            this.QuietMode = false;
        }

        #endregion

        public int Parallelism { get; set; }

        public bool QuietMode
        {
            get;
            set;
        }

        public void AddFilter(string text)
        {
            this.filters.Add(text);
        }

        #region Public Methods and Operators

        public void AddTestAssembly(string filePath)
        {
            var testClasses = new List<Type>();
            var deploymentsByType = new Dictionary<Type, Tuple<string, List<Tuple<string, string>>>>();
            if (this.testAssemblyFileNames.Add(Path.GetFileName(filePath)))
            {
                try
                {
                    var assemblyToLoad = Path.GetFullPath(filePath);
                    var allTypes = Assembly.LoadFrom(assemblyToLoad).GetTypes();

                    foreach (var type in allTypes)
                    {
                        if (IsTestClass(type) && !IsIgnored(type))
                        {
                            var deploymentItems = DeploymentItems(type).ToList();
                            if (deploymentItems.Any())
                            {
                                deploymentsByType[type] = Tuple.Create(assemblyToLoad, deploymentItems);
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
                catch (ReflectionTypeLoadException reflectionTypeLoadException)
                {
                    var loaderExceptionFullDetail = new StringBuilder();

                    Console.WriteLine("Failed loading assembly " + filePath + " - unable to load one or more of the requested types. Loader exceptions follow:");
                    foreach (var loaderException in reflectionTypeLoadException.LoaderExceptions)
                    {
                        Console.WriteLine(" >>> " + loaderException.Message);
                        loaderExceptionFullDetail.AppendLine(loaderException.ToString());
                    }

                    // Write dump of loaderexception to file in current directory
                    const string loaderExceptionDetailsFileName = ".\\LoaderExceptions.txt";
                    Console.WriteLine("Writing all loader exception details to " + loaderExceptionDetailsFileName);
                    File.WriteAllText(loaderExceptionDetailsFileName, loaderExceptionFullDetail.ToString());
                }
            }

            this.AddTestClasses(testClasses, deploymentsByType);
        }

        public void AddTestClasses(IEnumerable<Type> testClassTypes, Dictionary<Type, Tuple<string, List<Tuple<string, string>>>> deploymentsByType)
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

                    var testInvoker = this.CreateTestInvoker(testClassType);
                    if (this.IsIncludedInFilter(testInvoker))
                    {
                        if (deploymentsByType.ContainsKey(testClassType))
                        {
                            var d = deploymentsByType[testClassType];
                            this.IncludeDeploymentItems(d.Item1, d.Item2);
                        }

                        this.testList.Add(testInvoker);
                    }
                }
            }
        }

        public TestRunResult Execute()
        {
            this.InitializeQuietMode();
            Directory.CreateDirectory(this.path);
            Directory.SetCurrentDirectory(this.path);
            this.CopyDeploymentFiles();
            var result = new TestRunResult(this.QuietMode);
            result.Start();
            Parallel.ForEach(
                this.testList,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = this.Parallelism
                },
                async testItem =>
                    {
                        try
                        {
                            var successCount = await testItem.Execute(testItem, result).ConfigureAwait(false);
                            result.Success(testItem, successCount);
                        }
                        catch (Exception ex)
                        {
                            result.Failure(testItem, string.Empty, ex);
                        }
                    });
            result.Stop();
            this.StopQuietMode();
            return result;
        }

        private void InitializeQuietMode()
        {
            this.originalConsoleOut = Console.Out;
            if (this.QuietMode)
            {
                Console.SetOut(new StringWriter(new StringBuilder()));
            }
        }

        private void StopQuietMode()
        {
            Console.Out.Flush();
            Console.SetOut(this.originalConsoleOut);
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

        private bool IsIncludedInFilter(TestItem testItem)
        {
            return this.filters.Count == 0 || this.filters.Any(f => testItem.Name.IndexOf(f, StringComparison.OrdinalIgnoreCase) != -1 || testItem.Tests.Any(t => (testItem.Name + "." + t).IndexOf(f, StringComparison.OrdinalIgnoreCase) != -1));
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
                        Directory.CreateDirectory(Path.GetDirectoryName(filePath.Value));
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
        private TestItem CreateTestInvoker(Type testClassType)
        {
            // new TestItem(testClassType.FullName,
            var taskResult = Expression.Variable(typeof(Task), "taskResult");
            var lastTestMethod = Expression.Variable(typeof(string), "lastTestMethod");
            var testItemInstance = Expression.Parameter(typeof(TestItem), "testItem");
            var testClassInstance = Expression.Variable(testClassType, "test");
            var resultsParameter = Expression.Parameter(typeof(TestRunResult), "results");
            var allMethods = testClassType.GetRuntimeMethods().ToList();

            var outerExpressions = new List<Expression>(2)
                              {
                                  Expression.Assign(lastTestMethod, Expression.Constant("ctor")),
                                  Expression.Assign(testClassInstance, Expression.New(testClassType.GetConstructor(new Type[0])))
                              };

            var testMethodNames = new List<string>(allMethods.Count);
            var testMethods = new List<Expression>(allMethods.Count);
            var initMethods = allMethods.Where(IsTestInitialize).ToList();
            if (initMethods.Count > 1)
            {
                testMethods.Add(ThrowMoreThanOneTestInitialize(testClassType));
                testMethods.Add(Expression.Constant(Task.FromResult(0)));
            }
            else
            {
                var initMethod = initMethods.FirstOrDefault();
                if (initMethod != null)
                {
                    testMethods.Add(Expression.Assign(lastTestMethod, Expression.Constant(initMethod.Name)));

                    var testCall = IsAsyncMethod(initMethod) ? Expression.Call(Expression.Call(testClassInstance, initMethod), TaskWaitMethod) : Expression.Call(testClassInstance, initMethod);
                    testMethods.Add(testCall);
                }

                var testCount = 0;
                for (int i = 0; i < allMethods.Count; i++)
                {
                    var m = allMethods[i];
                    if (IsTestMethod(m) && !IsIgnored(m))
                    {
                        testMethods.Add(Expression.Assign(lastTestMethod, Expression.Constant(m.Name)));
                        testMethods.Add(InvokeTestMethod(testClassInstance, m));
                        testMethodNames.Add(m.Name);
                        testCount++;
                    }
                }

                if (testCount == 0)
                {
                    testMethods.Clear();
                }

                testMethods.Add(Expression.Constant(Task.FromResult(testCount)));
            }

            var parameters = new[]
                             {
                                 resultsParameter
                             };
            Expression finallyCall = InvokeTestCleanup(testClassType, resultsParameter, testItemInstance, testClassInstance, allMethods);

            var failureExceptionParameter = Expression.Parameter(typeof(Exception), "testFailureException");

            var innerTryCatchFinally = Expression.TryCatchFinally(
                Expression.Block(parameters, testMethods),
                finallyCall,
                Expression.Catch(failureExceptionParameter,
                    Expression.Block(new ParameterExpression[] { },
                        Expression.Call(
                                resultsParameter,
                                FailureMethod,
                                testItemInstance,
                                lastTestMethod,
                                failureExceptionParameter),
                        Expression.Constant(Task.FromResult(0)))
                        ));
            outerExpressions.Add(innerTryCatchFinally);
            var outerBlock = Expression.Block(
                new[]
                {
                    lastTestMethod,
                    testClassInstance
                },
                outerExpressions.ToArray());
            var execute = Expression.Lambda<Func<TestItem, TestRunResult, Task<int>>>(outerBlock, testClassType.Name, new[] { testItemInstance, resultsParameter }).Compile();
            return new TestItem(testClassType.FullName, execute, testMethodNames);
        }

        private static Expression InvokeTestMethod(ParameterExpression testClassInstance, MethodInfo m)
        {
            var call = IsAsyncMethod(m) ? Expression.Call(Expression.Call(testClassInstance, m), TaskWaitMethod) : Expression.Call(testClassInstance, m);
            var expectedExceptionType = ExpectedExceptions(m).FirstOrDefault();
            if (expectedExceptionType != null)
            {
                var tryBlock = Expression.Block(
                    call,
                    Expression.Throw(
                        Expression.New(
                            typeof(AssertFailedException).GetConstructor(
                                new Type[]
                                {
                                    typeof(string)
                                }),
                            Expression.Constant(string.Format("Expected exception {0} was not thrown", expectedExceptionType.Name)))));
                var tryCatch = Expression.TryCatch(tryBlock, Expression.Catch(expectedExceptionType, Expression.Empty()));
                return tryCatch;
            }

            return call;
        }

        private static Expression ThrowMoreThanOneTestInitialize(Type testClassType)
        {
            return Expression.Throw(
                                    Expression.New(
                                        typeof(InvalidOperationException).GetConstructor(
                                            new Type[]
                                            {
                                    typeof(string)
                                            }),
                                        Expression.Constant(string.Format("The test {0} has more than 1 method decorated with the [TestInitialize] attribute", testClassType.Name))));
        }

        private static Expression InvokeTestCleanup(Type testClassType, ParameterExpression resultsParameter, ParameterExpression testItemInstance, ParameterExpression testClassInstance, List<MethodInfo> allMethods)
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
                    Expression.Constant("InvokeTestCleanup"),
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
                return Expression.Block(parameters, Expression.IfThen(Expression.ReferenceNotEqual(testClassInstance, Expression.Constant(null)), Expression.Call(testClassInstance, cleanupMethod)), Expression.Constant(Task.FromResult(0)));
            }

            return Expression.Constant(Task.FromResult(0));
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

        private static bool IsAsyncMethod(MethodInfo m)
        {
            if (m.ReturnType == typeof(void))
            {
                return false;
            }

            return typeof(Task).IsAssignableFrom(m.ReturnType);
        }

        private static bool IsTestMethod(MethodInfo m)
        {
            return (m.ReturnType == typeof(void) || IsAsyncMethod(m)) && m.GetCustomAttributes().Any(c => c.GetType().Name == "TestMethodAttribute");
        }

        private static bool IsIgnored(MethodInfo m)
        {
            return m.GetCustomAttributes().Any(c => c.GetType().Name == "IgnoreAttribute");
        }

        public static void LogException(Exception ex, object p)
        {
            Console.WriteLine(ex.Message + "\r\n" + ex.StackTrace);
        }

        #endregion
    }
}