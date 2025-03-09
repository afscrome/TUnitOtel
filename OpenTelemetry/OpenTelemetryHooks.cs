using System.Diagnostics;
using TUnit.Core.Extensions;
using Status = TUnit.Core.Enums.Status;

namespace TUnitOtel.OpenTelemetry;


internal static class OpenTelemetryHooks
{
    public static OtelExporter _otelExporter = new();
    public static readonly ActivitySource _activitySource = new("TUnit", "0.0.1");


    public static class TestDiscoveryHooks
    {
        private static readonly AsyncLocal<Activity?> DiscoveryActivity = new();

        [Before(TestDiscovery)]
        public static void BeforeDiscovery()
        { 
            DiscoveryActivity.Value = _activitySource.StartActivity($"TestDiscovery {DateTime.UtcNow}");
        }


        [After(TestDiscovery)]
        public static void AfterDiscovery(TestDiscoveryContext context)
        {
            using var activity = DiscoveryActivity.Value;
            if (activity == null)
            {
                if (_activitySource.HasListeners())
                {
                    Console.WriteLine("Unexpected null activity");
                }

                return;
            }

            activity.AddTag("Tests", context.AllTests.Count());
            activity.AddTag("TestClasses", context.TestClasses.Count());
            activity.AddTag("Assemblies", context.Assemblies.Count());
            activity?.AddTag("Filter", context.TestFilter);
        }
    }

    public static class TestSessionHooks
    {
        private static readonly AsyncLocal<Activity?> TestSessionActivity = new();

        [BeforeEvery(TestSession)]
        public static void BeforeTestSession(TestSessionContext context)
        { 
            TestSessionActivity.Value = _activitySource.StartActivity($"TestSession {DateTime.UtcNow}");
            context.AddAsyncLocalValues();
        }

        [AfterEvery(TestSession)]
        public static void AfterTestSession(TestSessionContext context)
        {
            using (var activity = TestSessionActivity.Value)
            {
                if (activity == null)
                {
                    if (_activitySource.HasListeners())
                    {
                        Console.WriteLine("Unexpected null activity");
                    }

                    return;
                }

                TagActivityWithTestResults(activity, context.AllTests);
            }
            _otelExporter.Dispose();
        }
    }

    public static class AssemblyHooks
    {
        private static readonly AsyncLocal<Activity?> AssemblyActivity = new();

        [BeforeEvery(Assembly)]
        public static void BeforeAssembly(AssemblyHookContext context)
        {
            Metrics.RecordAssembly(context.Assembly);

            AssemblyActivity.Value = _activitySource.StartActivity("Assembly " + context.Assembly.GetName().Name);
            context.AddAsyncLocalValues();
        }

        [AfterEvery(Assembly)]
        public static void AfterAssembly(AssemblyHookContext context)
        {
            using var activity = AssemblyActivity.Value;
            if (activity == null)
            {
                if (_activitySource.HasListeners())
                {
                    Console.WriteLine("Unexpected null activity");
                }

                return;
            }

            TagActivityWithTestResults(activity, context.AllTests);
        }
    }

    public static class ClassHooks
    {
        private static readonly AsyncLocal<Activity?> ClassActivity = new();

        [BeforeEvery(Class)]
        public static void BeforeClass(ClassHookContext context)
        {
            Metrics.RecordClass(context.ClassType);

            var activity = ClassActivity.Value = _activitySource.StartActivity("Class " + context.ClassType.Name);
            activity?.AddTag(SemanticConventions.SuiteName, context.ClassType.FullName);
            context.AddAsyncLocalValues();
        }

        [AfterEvery(Class)]
        public static void AfterClass(ClassHookContext context)
        {
            using var activity = ClassActivity.Value;
            if (activity == null)
            {
                if (_activitySource.HasListeners())
                {
                    Console.WriteLine("Unexpected null activity");
                }

                return;
            }

            TagActivityWithTestResults(activity, context.Tests);
        }
    }

    public static class TestHooks
    {
        private static readonly AsyncLocal<Activity?> TestActivity = new();

        [BeforeEvery(Test)]
        public static void BeforeTest(TestContext context)
        {
            //TODO: Should we have one single trace for the entire test session, or give each test it's own trace, linking to the parent trace?

            //var linkedContext = Activity.Current;
            //var activityContext = new ActivityContext(Activity.TraceIdGenerator?.Invoke() ?? ActivityTraceId.CreateRandom(), default, default);
            //var activity = TestActivity.Value = _activitySource.StartActivity("Test " + context.GetTestDisplayName(), ActivityKind.Internal, activityContext);
            var activity = TestActivity.Value = _activitySource.StartActivity("Test " + context.GetTestDisplayName(), ActivityKind.Internal);
            if (activity == null)
                return;

            activity.AddTag(SemanticConventions.TestCaseName, context.GetTestDisplayName());
            activity.AddTag(SemanticConventions.SuiteName, context.TestDetails.TestClass.Type.FullName);
            //https://opentelemetry.io/docs/specs/semconv/general/attributes/#source-code-attributes
            activity.AddTag("code.filepath", context.TestDetails.TestFilePath);
            activity.AddTag("code.line.number", context.TestDetails.TestLineNumber);

            // To avoid having one giant trace, should we make each test a dedicated trace, with a link
            // back to the overarching activity
            //if (linkedContext != null)
            //{
            //    activity?.AddLink(new ActivityLink(linkedContext.Context));
            //}
            context.AddAsyncLocalValues();
        }

        [AfterEvery(Test)]
        public static void AfterTest(TestContext context)
        {
            using var activity = TestActivity.Value;
            var result = context.Result;
            if (activity == null || result == null)
                return;

            Metrics.RecordTest(result, context.TestDetails);

            if (result.Exception != null)
                activity.AddException(result.Exception);

            activity.SetStatus(result.Status switch
            {
                Status.Passed => ActivityStatusCode.Ok,
                Status.Failed => ActivityStatusCode.Error,
                _ => ActivityStatusCode.Unset
            });

            if (result.Start != null)
                activity.SetStartTime(result.Start.Value.DateTime);

            if (result.End != null)
                activity.SetEndTime(result.End.Value.DateTime);
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
}
