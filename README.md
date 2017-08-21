# MsTestRunner

A simple console app that runs MsTest based tests QUICKLY (because Visual Studio takes forever).
A test run of about 5500 tests currently appears to be about 4x as fast as Visual Studio's test runner.

### Usage

#### Running Specific Assemblies
    MsTestRunner "C:\MyCode\bin\Debug\MyUnitTests.dll" "C:\MyCode\bin\Debug\MyOtherTests.dll"
This runs all tests found in the specified files and runs them.

    MsTestRunner /testcontainer:C:\MyCode\bin\Debug\MyUnitTests.dll /testcontainer:C:\MyCode\bin\Debug\MyOtherTests.dll
This is equivalent and is compatible with mstest.exe arguments

#### Test Assemblies in a folder
    MsTestRunner "C:\MyCode\bin\Debug"
This uses convention based naming to search for any *Tests.dll files in any sub folders of the specified folder and will run them.

#### Parallelism
    MsTestRunner "C:\MyCode\bin\Debug" -p 8
This tells the test runner to run at most 8 test classes in parallel.

#### Filtering
    MsTestRunner "C:\MyCode\bin\Debug" -f "MathTest"
This tells the test runner to filter the test classes to those that have MathTest included anywhere in their full class name (namespace + class name)

#### Interactive Mode

    MsTestRunner "C:\MyCode\bin\Debug" -i
This enables interactive results mode where UP and DOWN arrow keys can be used to navigate through test failures.

#### Generating TRX output

    MsTestRunner /resultsfile:MyResults.trx
   
Tells the test runner to output its test results into a simplified Visual Studio compatible TRX xml format.

## Differences between MsTestRunner and Visual Studio's Test Runner
The execution semantics of this test runner differ from that of Visual Studio's in the following ways.

* MsTestRunner currently only creates a single test instance per class and calls it's [TestInitialize] method once, and then calls each method decorated with [TestMethod] attributes once. This results in much faster tests, but will only output the first failing test method for any given Test Class (a -compatibility switch may be added in future).
* MsTestRunner will fail any test class that has more than one method decorated with [TestInitialize] including the full inheritance hierarchy. We believe that tests should be stable and unambigious, the expected order of multiple [TestInitialize] methods makes them unpredictable and can introduce subtle bugs in tests.
* Failures are reported for the first failing test at the class level (i.e. if the first test method fails it is reported and further methods on the instance are not called)
* No .trx output at the moment (planned)
* Does not require a reference to Microsoft.VisualStudio.QualityTools.UnitTestFramework, nor does it require that it is built with a special Ms Test project. It just looks for classes and methods decorated with attributes that follow the names and rules below:

  1. TestClassAttribute - must be declared on the class
  2. ClassInitializeAttribute - must be zero or one static method inside a TestClassAttribute decorated class
  3. ClassCleanupAttribute - must be zero or one static method inside a TestClassAttribute decorated class
  4. TestInitializeAttribute - Must be zero or one method decorated with this attribute inside a TestClassAttribute decorated class
  5. TestCleanupAttribute - Must be zero or one method decorated with this attribute inside a TestClassAttribute decorated class
