# MsTestRunner

A simple console app that runs MsTest based tests QUICKLY (because Visual Studio takes forever).
A test run of about 5500 tests currently appears to be about 4x as fast as Visual Studio's test runner.

### Usage

    MsTestRunner "C:\MyCode\bin\Debug\MyUnitTests.dll" "C:\MyCode\bin\Debug\MyOtherTests.dll"
This runs all tests found in the specified files and runs them.

    MsTestRunner /testcontainer:C:\MyCode\bin\Debug\MyUnitTests.dll /testcontainer:C:\MyCode\bin\Debug\MyOtherTests.dll
This is equivalent and is compatible with mstest.exe 


    MsTestRunner "C:\MyCode\bin\Debug"
This will search for any *Tests.dll files in any sub folders of the specified folder and will run them.

    MsTestRunner "C:\MyCode\bin\Debug" -p 8
This tells the test runner to run at most 8 test classes in parallel.

## Differences between MsTestRunner and Visual Studio's Test Runner
The execution semantics of this test runner differ from that of Visual Studio's in the following ways.

* MsTestRunner currently only creates a single test instance per class and calls it's [TestInitialize] method once, and then calls each method decorated with [TestMethod] attributes once. This results in much faster tests, but will only output the first failing test method for any given Test Class (a -compatibility switch may be added in future).
* MsTestRunner will fail any test class that has more than one method decorated with [TestInitialize] (including the full inheritance hierarchy). We believe that tests should be stable and unambigious, the expected order of multiple [TestInitialize] methods makes them unpredictable and can introduce subtle bugs in tests.
* Failures are reported for the first failing test at the class level (i.e. if the first test method fails it is reported and further methods on the instance are not called)
* No .trx output at the moment (planned)