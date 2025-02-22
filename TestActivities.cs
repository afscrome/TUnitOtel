using System.Diagnostics;

namespace TUnitOtel;

// Test how further activities within tests are handled
public class TestActivities
{
    [Test]
    public async Task CurrentActivity_ShouldBeAvialable()
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