namespace Fluss.Exceptions;

public class RetryException : Exception
{
    public RetryException() : base("This operation needs to be retried") { }
}
