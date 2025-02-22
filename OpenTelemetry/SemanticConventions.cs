//https://github.com/open-telemetry/semantic-conventions/blob/main/docs/attributes-registry/test.md 
internal class SemanticConventions
{
    internal const string TestCaseName = "test.case.name";
    internal const string TestCaseStatus = "test.case.result.status";
    internal const string SuiteName = "test.suite.name";
    internal const string SuiteStatus = "test.suite.run.status";

    internal string GetTestResultStatus(TestResult result) => result switch
    {
        { Exception: TimeoutException } => "timed_out",
        { Exception: TaskCanceledException } => "aborted",
        { Status: TUnit.Core.Enums.Status.Passed } => "pass",
        { Status: TUnit.Core.Enums.Status.Failed } => "fail",
        { Status: TUnit.Core.Enums.Status.Skipped } => "skipped",
        _ => result.Status.ToString()
    };
}
