using Pesca;

var checks = 0;
foreach (var pause in new[] { 5000, 7000 })
{
    foreach (var castHold in new[] { 1200, 1300 })
    {
        const int pauseAfterClicks = 5000;
        var mouse = new RecordingMouse();
        var sequence = new MouseSequence(mouse, mouse.Wait, token => mouse.Wait(pause, token));
        var completedClicks = sequence.Run(castHold, pauseAfterClicks, CancellationToken.None);
        Check(!mouse.IsDown, "El botón debe terminar suelto.");
        Check(completedClicks == 14 && mouse.Presses.Count == 15, "Debe haber un lanzamiento y exactamente 14 clics.");
        Check(mouse.Presses[0] == (0, castHold), "El lanzamiento debe respetar la duración de 1,2–1,3 segundos.");
        Check(mouse.Presses[1].Start == castHold + pause, "La espera del pez debe empezar al soltar el botón.");
        Check(mouse.Presses.Skip(1).All(press => press.End - press.Start == 40), "Cada clic debe tener pulsación y liberación independientes.");
        Check(mouse.Now == castHold + pause + 14 * 40 + 13 * 80 + pauseAfterClicks,
            "Los clics deben estar separados 120 ms y terminar con la pausa pedida.");
        var lastClickReleased = mouse.Presses[^1].End;
        sequence.Run(castHold, pauseAfterClicks, CancellationToken.None);
        Check(mouse.Presses.Count == 30 && mouse.Presses[15].Start - lastClickReleased == pauseAfterClicks,
            "El siguiente lanzamiento debe empezar después de la pausa de 5 segundos.");
        checks++;
    }
}

foreach (var invalidHold in new[] { 1199, 1301, 1397, 1494 })
{
    var mouse = new RecordingMouse();
    try
    {
        new MouseSequence(mouse, mouse.Wait, _ => { }).Run(invalidHold, 5000, CancellationToken.None);
        throw new Exception("Se ha aceptado un lanzamiento fuera de 1200–1300 ms.");
    }
    catch (ArgumentOutOfRangeException)
    {
        Check(mouse.Presses.Count == 0 && !mouse.IsDown,
            "Una duración fuera de rango debe rechazarse antes de pulsar el ratón.");
        checks++;
    }
}

foreach (var cancellation in new[] { false, true })
{
    var mouse = new RecordingMouse();
    void InterruptedWait(int milliseconds, CancellationToken token)
    {
        if (mouse.IsDown)
        {
            mouse.Now += 500;
            if (cancellation)
            {
                throw new OperationCanceledException();
            }

            throw new InvalidOperationException("Roblox ha perdido el foco.");
        }

        mouse.Wait(milliseconds, token);
    }

    try
    {
        new MouseSequence(mouse, InterruptedWait, _ => { }).Run(1250, 5000, CancellationToken.None);
        throw new Exception("La interrupción no se ha propagado.");
    }
    catch (Exception error) when (error is OperationCanceledException or InvalidOperationException)
    {
        Check(!mouse.IsDown && mouse.Presses.Count == 1, "Interrumpir la pulsación debe soltar el botón y evitar más clics.");
        checks++;
    }
}

var unavailable = new RecordingMouse { TargetAvailable = false };
try
{
    new MouseSequence(unavailable, unavailable.Wait, _ => { }).Run(1250, 5000, CancellationToken.None);
    throw new Exception("Se ha permitido enviar entrada sin una ventana válida.");
}
catch (InvalidOperationException)
{
    Check(unavailable.Presses.Count == 0 && !unavailable.IsDown && unavailable.Now == 0,
        "Sin foco no debe comenzar la secuencia.");
    checks++;
}

foreach (var fixture in new[] { (Name: "idle", Expected: false), (Name: "fish", Expected: true), (Name: "escaped", Expected: false) })
{
    var rgb = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture.Name + ".rgb"));
    Check(FishingCue.IsPresent(rgb, 160, 96) == fixture.Expected, "Detección incorrecta en captura real: " + fixture.Name);
    checks++;
}
var noFish = new RecordingMouse();
try
{
    new MouseSequence(noFish, noFish.Wait, _ => throw new TimeoutException()).Run(1250, 5000, CancellationToken.None);
    throw new Exception("No se ha propagado el tiempo de espera agotado.");
}
catch (TimeoutException)
{
    Check(noFish.Presses.Count == 1 && !noFish.IsDown, "Sin pez solo debe enviarse el lanzamiento.");
    checks++;
}
var pausedMouse = new RecordingMouse();
void CancelDuringPause(int milliseconds, CancellationToken token)
{
    if (milliseconds == 5000) throw new OperationCanceledException();
    pausedMouse.Wait(milliseconds, token);
}
try
{
    new MouseSequence(pausedMouse, CancelDuringPause, _ => { }).Run(1250, 5000, CancellationToken.None);
    throw new Exception("No se ha propagado la cancelación durante la pausa.");
}
catch (OperationCanceledException)
{
    Check(pausedMouse.Presses.Count == 15 && !pausedMouse.IsDown,
        "Cancelar después de los 14 clics debe mantener el botón suelto y evitar otro lanzamiento.");
    checks++;
}

var failureAttempts = 0;
var failureDelays = new List<int>();
var failureReports = 0;
FishingRecovery.Run(() => ++failureAttempts < 3 ? 1 : 0, milliseconds =>
{
    failureDelays.Add(milliseconds);
    return true;
}, _ => failureReports++);
Check(failureAttempts == 3 && failureDelays.SequenceEqual(new[] { 30000, 30000 }) && failureReports == 0,
    "Un error devuelto debe esperar 30 segundos y reiniciar, incluso tras errores consecutivos.");
checks++;

foreach (var sampleError in new Exception[] { new NullReferenceException(), new IOException(), new OperationCanceledException() })
{
    var attempts = 0;
    var delays = new List<int>();
    Exception? reportedError = null;
    FishingRecovery.Run(() =>
    {
        if (++attempts == 1) throw sampleError;
        return 0;
    }, milliseconds =>
    {
        delays.Add(milliseconds);
        return true;
    }, error => reportedError = error);
    Check(attempts == 2 && delays.SequenceEqual(new[] { 30000 }) && ReferenceEquals(reportedError, sampleError),
        "Una excepción inesperada debe esperar 30 segundos y reiniciar: " + sampleError.GetType().Name);
    checks++;
}

var stoppedAttempts = 0;
var stoppedRetries = 0;
var stoppedReports = 0;
FishingRecovery.Run(() =>
{
    stoppedAttempts++;
    throw new FishingInterruptedException();
}, _ => { stoppedRetries++; return true; }, _ => stoppedReports++);
Check(stoppedAttempts == 1 && stoppedRetries == 0 && stoppedReports == 0,
    "La interrupción voluntaria debe detener la pesca sin reiniciarla.");
checks++;

var canceledAttempts = 0;
var canceledDelays = new List<int>();
FishingRecovery.Run(() => { canceledAttempts++; return 1; }, milliseconds =>
{
    canceledDelays.Add(milliseconds);
    return false;
}, _ => { });
Check(canceledAttempts == 1 && canceledDelays.SequenceEqual(new[] { 30000 }),
    "Q o S durante la espera deben impedir el siguiente intento.");
checks++;

var waitFailureAttempts = 0;
var waitFailureDelays = new List<int>();
var waitFailureReports = 0;
FishingRecovery.Run(() => ++waitFailureAttempts == 1 ? 1 : 0, milliseconds =>
{
    waitFailureDelays.Add(milliseconds);
    if (waitFailureDelays.Count == 1) throw new IOException("Fallo durante la espera.");
    return true;
}, _ => waitFailureReports++);
Check(waitFailureAttempts == 2 && waitFailureDelays.SequenceEqual(new[] { 30000, 30000 }) && waitFailureReports == 1,
    "Un error durante la recuperación también debe reiniciar la espera de 30 segundos.");
checks++;

var logFailureAttempts = 0;
var logFailureDelays = new List<int>();
FishingRecovery.Run(() =>
{
    if (++logFailureAttempts == 1) throw new InvalidOperationException("Fallo de pesca.");
    return 0;
}, milliseconds => { logFailureDelays.Add(milliseconds); return true; },
    _ => throw new IOException("Fallo del registro."));
Check(logFailureAttempts == 2 && logFailureDelays.SequenceEqual(new[] { 30000 }),
    "Un fallo del registro no debe impedir recuperar la pesca.");
checks++;

Console.WriteLine($"Correcto: {checks} escenarios de temporización, recuperación y detección; no se ha enviado entrada real.");

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new Exception(message);
    }
}

internal sealed class RecordingMouse : IMouseInput
{
    public int Now { get; set; }
    public bool IsDown { get; private set; }
    public bool TargetAvailable { get; set; } = true;
    public List<(int Start, int End)> Presses { get; } = [];
    private int started;

    public void EnsureTarget()
    {
        if (!TargetAvailable)
        {
            throw new InvalidOperationException("Ventana sin foco.");
        }
    }

    public void LeftDown()
    {
        if (IsDown)
        {
            throw new Exception("El botón ya estaba pulsado.");
        }

        IsDown = true;
        started = Now;
    }

    public void LeftUp()
    {
        if (IsDown)
        {
            Presses.Add((started, Now));
            IsDown = false;
        }
    }

    public void Wait(int milliseconds, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Now += milliseconds;
    }
}
