using System;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(MackySoft.SerializeReferenceExtensions.Tests.BatchTestProgress))]

namespace MackySoft.SerializeReferenceExtensions.Tests
{
    // Results XML is written only after the run finishes. Keep the current test
    // in the Editor log even when a native crash prevents that final report.
    public sealed class BatchTestProgress : ITestRunCallback
    {
        public void RunStarted (ITest testsToRun) => Write("RUN START", testsToRun.FullName);

        public void RunFinished (ITestResult result) => Write("RUN FINISH", result.ResultState.ToString());

        public void TestStarted (ITest test)
        {
            if (!test.IsSuite)
            {
                Write("START", test.FullName);
            }
        }

        public void TestFinished (ITestResult result)
        {
            if (!result.Test.IsSuite)
            {
                Write("FINISH", result.Test.FullName + " " + result.ResultState);
            }
        }

        private static void Write (string phase, string detail)
        {
            if (Application.isBatchMode)
            {
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null,
                    "[EditMode {0:O}] {1} {2}", DateTime.UtcNow, phase, detail);
            }
        }
    }
}
