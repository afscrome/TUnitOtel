using System.Diagnostics.Metrics;
using Status = TUnit.Core.Enums.Status;

namespace TUnitOtel.OpenTelemetry;

internal static class Metrics
{
    public static readonly Meter Meter = new("TUnit", "0.0.1");

    private static Histogram<double> _testTime = Meter.CreateHistogram<double>("testtime", unit: "ms", description: "Test execution time (in ms)");
    private static Counter<int> _passedTests = Meter.CreateCounter<int>("passedTests", description: "Number of passed tests");
    private static Counter<int> _failedTests = Meter.CreateCounter<int>("failedTests", description: "Number of passed tests");
    private static Counter<int> _skippedTests = Meter.CreateCounter<int>("skippedTests", description: "Number of passed tests");

    internal static void RecordTest(TestResult? result)
    {
        if (result == null)
            return;

        if (result.Status == Status.Passed)
            _passedTests.Add(1);
        else if (result.Status == Status.Failed)
            _failedTests.Add(1);
        else if (result.Status == Status.Skipped)
            _skippedTests.Add(1);

        if (result.Duration != null)
        {
            _testTime.Record(result.Duration.Value.TotalMilliseconds);
        }

    }

}
