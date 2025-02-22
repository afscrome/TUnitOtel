namespace TUnitOtel;

// Some long running tests to show varying length spans in traces
public class LongRunningTests
{
    [Test]
    [Repeat(6)]
    [NotInParallel]
    public async Task Sequential()
    {
        await Task.Delay(50);
    }

    [Test]
    [Arguments(1)]
    [Arguments(23)]
    [Arguments(150)]
    [Arguments(1023)]
    [Arguments(2500)]
    public async Task Delay(int milliseconds)
    {
        await Task.Delay(milliseconds);
    }
}