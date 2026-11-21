namespace Pesca;

internal sealed class FishingInterruptedException : OperationCanceledException
{
    public FishingInterruptedException() : base("Pesca interrumpida por el usuario.")
    {
    }
}

internal static class FishingRecovery
{
    public const int RetryDelayMilliseconds = 30000;

    public static void Run(Func<int> runAttempt, Func<int, bool> waitBeforeRetry,
        Action<Exception> reportError)
    {
        var retryPending = false;
        while (true)
        {
            try
            {
                if (retryPending && !waitBeforeRetry(RetryDelayMilliseconds)) return;
                if (runAttempt() == 0) return;
            }
            catch (FishingInterruptedException)
            {
                return;
            }
            catch (Exception exception)
            {
                try
                {
                    reportError(exception);
                }
                catch (Exception)
                {
                    // Un fallo del registro no debe impedir el siguiente reintento.
                }
            }

            retryPending = true;
        }
    }
}
