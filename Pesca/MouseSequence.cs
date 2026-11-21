namespace Pesca;

internal interface IMouseInput
{
    void EnsureTarget();
    void LeftDown();
    void LeftUp();
}

internal sealed class MouseSequence(IMouseInput mouse, Action<int, CancellationToken> wait,
    Action<CancellationToken> waitForFish)
{
    public const int ClickCount = 14;
    public const int MinimumCastHoldMilliseconds = 1200;
    public const int MaximumCastHoldMilliseconds = 1300;

    public int Run(int castHoldMilliseconds, int pauseAfterClicksMilliseconds, CancellationToken cancellationToken)
    {
        if (castHoldMilliseconds is < MinimumCastHoldMilliseconds or > MaximumCastHoldMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(castHoldMilliseconds));
        }

        if (pauseAfterClicksMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pauseAfterClicksMilliseconds));
        }

        cancellationToken.ThrowIfCancellationRequested();
        mouse.EnsureTarget();
        HoldLeft(castHoldMilliseconds, cancellationToken);
        waitForFish(cancellationToken);

        var completedClicks = 0;
        for (var index = 0; index < ClickCount; index++)
        {
            HoldLeft(40, cancellationToken);
            completedClicks++;
            if (index < ClickCount - 1)
            {
                wait(80, cancellationToken);
            }
        }

        wait(pauseAfterClicksMilliseconds, cancellationToken);
        return completedClicks;
    }

    private void HoldLeft(int milliseconds, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        mouse.EnsureTarget();
        try
        {
            mouse.LeftDown();
            wait(milliseconds, cancellationToken);
        }
        finally
        {
            // Suelta el botón incluso si se cancela la espera o cambia el foco.
            mouse.LeftUp();
        }
    }
}
