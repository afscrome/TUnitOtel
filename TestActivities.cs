using System.Diagnostics;
using System.Threading.Tasks;

namespace TUnitOtel;

// Test how further activities within tests are handled
public class TestActivities
{
    [Test]
    public async Task CurrentActivity_ShouldBeAvailable()
    {
        var activity = Activity.Current;

        await Assert.That(activity).IsNotNull();
    }


    [Test]
    public async Task HttpActivity()
    {
        using var httpClient = new HttpClient();
        await httpClient.GetAsync("https://google.com/");
    }
}


public class AsyncLocalsHooks
{
    private static readonly AsyncLocal<string> _1BeforeTestSessionLocal = new AsyncLocal<string>();
    private static readonly AsyncLocal<string> _2BeforeAssemblyLocal = new AsyncLocal<string>();
    private static readonly AsyncLocal<string> _3BeforeClassLocal = new AsyncLocal<string>();
    private static readonly AsyncLocal<string> _4BeforeTestLocal = new AsyncLocal<string>();

    [BeforeEvery(TestSession)]
    public static void BeforeTestSession(TestSessionContext context)
        => _1BeforeTestSessionLocal.Value = "BeforeTestSession";

    [BeforeEvery(Assembly)]
    public static void BeforeAssembly(AssemblyHookContext context)
        => _2BeforeAssemblyLocal.Value = "BeforeAssembly";

    [BeforeEvery(Class)]
    public static void BeforeClass(ClassHookContext context)
        => _3BeforeClassLocal.Value = "BeforeClass";

    [BeforeEvery(Test)]
    public static void BeforeTest(TestContext context)
        => _4BeforeTestLocal.Value = "BeforeTest";

    [Test]
    public async Task TestAsynclocals()
    {
        using var _ = Assert.Multiple();

        await Assert.That(_1BeforeTestSessionLocal.Value).IsEqualTo("BeforeTestSession");
        await Assert.That(_2BeforeAssemblyLocal.Value).IsEqualTo("BeforeAssembly");
        await Assert.That(_3BeforeClassLocal.Value).IsEqualTo("BeforeClass");
        await Assert.That(_4BeforeTestLocal.Value).IsEqualTo("BeforeTest");
    }

}