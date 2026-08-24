namespace TheHaunt.Tests;

public sealed class TestFailedException : Exception
{
    public TestFailedException(string message)
        : base(message)
    {
    }
}
