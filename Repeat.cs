namespace TUnitOtel;

// Increase test count
public class Repeat
{

    [Test, Repeat(10)]
    public async Task MyTest2()
    {
        await Task.Yield();
    }
}