using OpenTelemetry.Trace;
using System.Collections.Concurrent;
using System.Diagnostics;
using TUnit.Core.Enums;
using TUnit.Core.Extensions;
using Status = TUnit.Core.Enums.Status;

namespace TUnitOtel.OpenTelemetry;


internal static class OpenTelemetryHooks
{
    public static OtelExporter _otelExporter = new();
    public static readonly ActivitySource _activitySource = new("TUnit", "0.0.1");

    public static class TestDiscovery
    {
        private static Activity? _discoveryActivity = null;

        [Before(HookType.TestDiscovery)]
        public static void BeforeDiscovery()
        {
            _discoveryActivity = _activitySource.StartActivity($"TestDiscovery {DateTime.UtcNow}");
        }


        [After(HookType.TestDiscovery)]
        public static void AfterDiscovery(TestDiscoveryContext context)
        {
            _discoveryActivity?.AddTag("Tests", context.AllTests.Count());
            _discoveryActivity?.AddTag("TestClasses", context.TestClasses.Count());
            _discoveryActivity?.AddTag("Assemblies", context.Assemblies.Count());
            _discoveryActivity?.AddTag("Filter", context.TestFilter);
            _discoveryActivity?.Dispose();
        }
    }

    public static class TestSession
    {
        [BeforeEvery(HookType.TestSession)]
        public static void BeforeTestSession(TestSessionContext context)
        {
            var activity = _activitySource.StartActivity($"TestSession {DateTime.UtcNow}");
            ContextActivities.Add(context, activity);
        }

        [AfterEvery(HookType.TestSession)]
        public static void AfterTestSession(TestSessionContext context)
        {
            var activity = ContextActivities.Retrieve(context);
            TagActivityWithTestResults(activity, context.AllTests);
            activity?.Dispose();

            _otelExporter.Dispose();
        }
    }

    public static class Assembly
    {
        [BeforeEvery(HookType.Assembly)]
        public static void BeforeAssembly(AssemblyHookContext context)
        {
            var parentContext = ContextActivities.GetActivityContext(TestSessionContext.Current);

            var activity = _activitySource.StartActivity(ActivityKind.Internal, parentContext, name: "Assembly " + context.Assembly.GetName().Name);

            ContextActivities.Add(context.Assembly, activity);
        }

        [AfterEvery(HookType.Assembly)]
        public static void AfterAssembly(AssemblyHookContext context)
        {
            var activity = ContextActivities.Retrieve(context.Assembly);
            TagActivityWithTestResults(activity, context.AllTests);
            activity?.Dispose();
        }
    }

    public static class Class
    {
        [BeforeEvery(HookType.Class)]
        public static void BeforeClass(ClassHookContext context)
        {
            var parentContext = ContextActivities.GetActivityContext(context.ClassType.Assembly);

            var activity = _activitySource.StartActivity(ActivityKind.Internal, parentContext, name: "Class " + context.ClassType.Name);
            ContextActivities.Add(context.ClassType, activity);

            activity?.AddTag(SemanticConventions.SuiteName, context.ClassType.FullName);
        }

        [AfterEvery(HookType.Class)]
        public static void AfterClass(ClassHookContext context)
        {
            var activity = ContextActivities.Retrieve(context.ClassType);
            TagActivityWithTestResults(activity, context.Tests);
            activity?.Dispose();
        }
    }

    public static class Test
    {
        [BeforeEvery(HookType.Test)]
        public static void BeforeTest(TestContext context)
        {
            var parentContext = ContextActivities.GetActivityContext(context.TestDetails.TestClass.Type);
            var activity = _activitySource.StartActivity(ActivityKind.Internal, parentContext, name: "Test " + context.GetTestDisplayName());
            ContextActivities.Add(context, activity);

            activity?.AddTag(SemanticConventions.TestCaseName, context.GetTestDisplayName());
            activity?.AddTag(SemanticConventions.SuiteName, context.TestDetails.TestClass.Type.FullName);
            //https://opentelemetry.io/docs/specs/semconv/general/attributes/#source-code-attributes
            activity?.AddTag("code.filepath", context.TestDetails.TestFilePath);
            activity?.AddTag("code.line.number", context.TestDetails.TestLineNumber);

            // To avoid having one giant trace, should we make each test a dedicated trace, with a link
            // back to the overarching activity
            //activity?.AddLink(new ActivityLink(parentContext));
        }

        [AfterEvery(HookType.Test)]
        public static void AfterTest(TestContext context)
        {
            using var activity = ContextActivities.Retrieve(context);

            var result = context.Result;

            Metrics.RecordTest(result);

            if (result?.Exception != null)
            {
                activity?.AddException(result.Exception);
            }

            activity?.SetStatus(result?.Status switch
            {
                Status.Passed => ActivityStatusCode.Ok,
                Status.Failed => ActivityStatusCode.Error,
                _ => ActivityStatusCode.Unset
            });

            if (result?.Start != null)
            {
                activity?.SetStartTime(result.Start.Value.DateTime);
            }
            if (result?.End != null)
            {
                activity?.SetEndTime(result.End.Value.DateTime);
            }
        }
    }

    private static void TagActivityWithTestResults(Activity? activity, IEnumerable<TestContext> testContexts)
    {
        if (activity == null)
            return;

        int failedTests = 0;
        int passedTests = 0;
        int skippedTests = 0;

        foreach (var testContext in testContexts)
        {
            var result = testContext.Result;
            if (result?.Status == Status.Passed)
                passedTests++;
            else if (result?.Status == Status.Failed)
                failedTests++;
            else if (result?.Status == Status.Skipped)
                skippedTests++;
        }

        activity.SetTag("passed", passedTests);
        activity.SetTag("failed", failedTests);
        activity.SetTag("skipped", skippedTests);


        if (failedTests > 0)
        {
            var totalTests = failedTests + passedTests;
            var status = $"{failedTests} of {totalTests} failed";
            activity.SetStatus(ActivityStatusCode.Error, status);
        }
        else if (passedTests > 0)
            activity.SetStatus(ActivityStatusCode.Ok);
    }


    internal static class ContextActivities
    {
        private static readonly ConcurrentDictionary<object, Activity> _contextActivities = new();
        public static Activity? Add(object context, Activity? activity)
        {
            if (activity != null)
            {
                _contextActivities.TryAdd(context, activity);
            }
            return activity;
        }

        public static ActivityContext GetActivityContext(object? context)
        {
            if (context == null)
                return default;

            if (_contextActivities.TryGetValue(context, out var activity))
            {
                return activity.Context;
            }

            return default;
        }


        public static Activity? Retrieve(object context)
        {
            return _contextActivities.TryRemove(context, out var activity)
                ? activity
                : null;
        }
    }
}
