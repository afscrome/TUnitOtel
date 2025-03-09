using System.Diagnostics.Metrics;
using System.Reflection;
using TUnit.Engine.Json;

namespace TUnitOtel.OpenTelemetry;

internal static class Metrics
{
    public static readonly Meter Meter = new("TUnit", "0.0.1");

    private static Histogram<double> _testTime = Meter.CreateHistogram<double>("testtime", unit: "ms", description: "Test execution time (in ms)");

    private static Counter<int> _assemblies = Meter.CreateCounter<int>("assemblies", description: "Number of assemblies loaded");
    private static Counter<int> _classes = Meter.CreateCounter<int>("classes", description: "Number of assemblies loaded");

    internal static void RecordAssembly(Assembly assembly)
    {
        _assemblies.Add(1);
    }

    internal static void RecordClass(Type type)
    {
        _classes.Add(1, [new KeyValuePair<string, object?>("class", type.FullName)]);
    }

    internal static void RecordTest(TestResult result, TestDetails details)
    {
        if (result == null)
            return;

        var tags = new KeyValuePair<string, object?>[]
        {
            // TODO: Does including the test name make this too high cardinality?
            // Ont heo ther hand, it is valuable to trace the success rate of an individual test over time.
            new("test", details.TestName),
            new("class", details.TestClass.Name),
            new("assembly", details.TestClass.Assembly.Name),
            new("status", result.Status.ToString())
        };

        if (result.Duration != null)
        {
            _testTime.Record(result.Duration.Value.TotalMilliseconds, tags);
        }
    }
}
