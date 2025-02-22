namespace TUnitOtel;

// Test with specific results
public class Results
{
    [Test]
    public async Task Pass()
    {
        int i = 1;
        await Assert.That(i).IsEqualTo(1);
    }

    [Test]
    public async Task Skipped()
    {
        Skip.Test("reason");
        await Task.Yield();
    }

    [Test]
    public async Task Fail()
    {
        await Task.Yield();
        Assert.Fail("failure");
    }

    [Test]
    public async Task Throw()
    {
        await Task.Yield();
        throw new Exception("thrown");
    }

    [Test]
    [Timeout(1)]
    public async Task Timeout(CancellationToken cancellationToken)
    {
        await Task.Delay(1000, cancellationToken);
    }
}