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

    private static readonly AsyncLocal<Activity?> DiscoveryActivity = new();
    private static readonly AsyncLocal<Activity?> TestSessionActivity = new();
    private static readonly AsyncLocal<Activity?> AssemblyActivity = new();
    private static readonly AsyncLocal<Activity?> ClassActivity = new();
    private static readonly AsyncLocal<Activity?> TestActivity = new();

    public static class TestDiscovery
    {
        [Before(HookType.TestDiscovery)]
        public static void BeforeDiscovery()
            => DiscoveryActivity.Value = _activitySource.StartActivity($"TestDiscovery {DateTime.UtcNow}");


        [After(HookType.TestDiscovery)]
        public static void AfterDiscovery(TestDiscoveryContext context)
        {
            using var activity = DiscoveryActivity.Value;
            if (activity == null)
                return;
            
            activity.AddTag("Tests", context.AllTests.Count());
            activity.AddTag("TestClasses", context.TestClasses.Count());
            activity.AddTag("Assemblies", context.Assemblies.Count());
            activity?.AddTag("Filter", context.TestFilter);
        }
    }

    public static class TestSession
    {
        [BeforeEvery(HookType.TestSession)]
        public static void BeforeTestSession(TestSessionContext context)
            => TestSessionActivity.Value = _activitySource.StartActivity($"TestSession {DateTime.UtcNow}");

        [AfterEvery(HookType.TestSession)]
        public static void AfterTestSession(TestSessionContext context)
        {
            using var activity = TestSessionActivity.Value;
            if (activity == null)
                return;

            TagActivityWithTestResults(activity, context.AllTests);

            _otelExporter.Dispose();
        }
    }

    public static class Assembly
    {


        [BeforeEvery(HookType.Assembly)]
        public static void BeforeAssembly(AssemblyHookContext context)
        {
            AssemblyActivity.Value = _activitySource.StartActivity("Assembly " + context.Assembly.GetName().Name);
        }

        [AfterEvery(HookType.Assembly)]
        public static void AfterAssembly(AssemblyHookContext context)
        {
            using var activity = AssemblyActivity.Value;
            if (activity == null)
                return;

            TagActivityWithTestResults(activity, context.AllTests);
        }
    }

    public static class Class
    {
        [BeforeEvery(HookType.Class)]
        public static void BeforeClass(ClassHookContext context)
        {
            var activity = ClassActivity.Value = _activitySource.StartActivity("Class " + context.ClassType.Name);
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
