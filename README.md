# MsTestRunner

A simple console app that runs MsTest based tests QUICKLY (because Visual Studio takes forever).
A test run of about 5500 tests currently appears to be about 4x as fast as Visual Studio's test runner.

### Usage

    MsTestRunner "C:\MyCode\bin\Debug\MyUnitTests.dll" "C:\MyCode\bin\Debug\MyOtherTests.dll"
This runs all tests found in the specified files and runs them.

    MsTestRunner "C:\MyCode\bin\Debug"
This will search for any *Tests.dll files in any sub folders of the specified folder and will run them.


## IMPORTANT
The execution semantics of this test runner differ from that of Visual Studio's.
Our test runner currently only creates a single test instance per class and calls it's [TestInitialize] method once, 
and then calls each method decorated with [TestMethod] attributes once.
This results in much faster tests, but will only output the first failing test method for any given Test Class (this may be improved in future).
