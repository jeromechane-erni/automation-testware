using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TestWare.Core;
using TestWare.Core.Attributes;

namespace TestWare.Wheels.MsTestWheel;

public class TestWareMethod : DataTestMethodAttribute
{
    public override TestResult[] Execute(ITestMethod testMethod)
    {
        // This test's engines, pages and components live until the end of this method: injection, [TestInitialize],
        // the test body and [TestCleanup] all see the same ones.
        var test = TestWareProvider.BeginTest();

        TestResult[] results;
        try
        {
            results = Run(testMethod);
        }
        catch
        {
            test.Close();   // the original exception is the one to report
            throw;
        }

        ReportCloseErrors(results, test.Close());
        return results;
    }

    private TestResult[] Run(ITestMethod testMethod)
    {
        if ((testMethod.Arguments?.Count() ?? 0) < (testMethod.ParameterTypes?.Count() ?? 0))
        {
            var scopes = TestWareAttributes.GetTestWareScopes(testMethod.TestClassName, testMethod.TestMethodName);
            object[] injectedArgs = new object[testMethod.ParameterTypes.Count()];
            var argsCount = 0;
            for (var i = 0; i < testMethod.ParameterTypes.Count(); i++)
            {
                var paramType = testMethod.ParameterTypes[i].ParameterType;
                if (paramType == testMethod.Arguments?.ElementAtOrDefault(argsCount)?.GetType())
                {
                    injectedArgs[i] = testMethod.Arguments[argsCount];
                    argsCount++;
                }
                else
                {
                    string consumedScope;
                    injectedArgs[i] = TestWareProvider.GetTestWareComponentFromTags(testMethod.ParameterTypes[i].ParameterType, scopes, out consumedScope);
                    scopes = scopes.Where(x => x != consumedScope).ToArray();
                }
            }
            return [testMethod.Invoke(injectedArgs)];
        }

        return base.Execute(testMethod);
    }

    // An engine that fails to close must not hide how the test went. A failed test keeps its own failure as the headline
    // and gets the problem added to its error output. A passed test fails, because a browser may be left open.
    private static void ReportCloseErrors(TestResult[] results, IReadOnlyList<Exception> errors)
    {
        if (errors.Count == 0)
            return;

        var message = "The test's engines could not all be closed: " + string.Join("; ", errors.Select(e => e.Message));
        foreach (var result in results)
        {
            if (result.Outcome == UnitTestOutcome.Passed)
            {
                result.Outcome = UnitTestOutcome.Failed;
                result.TestFailureException = new AssertFailedException(message, errors[0]);
            }
            else
            {
                result.LogError = string.IsNullOrEmpty(result.LogError) ? message : result.LogError + Environment.NewLine + message;
            }
        }
    }
}
