using System.ComponentModel;
using System.Diagnostics;
using Pesca;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("Este programa requiere Windows.");
    return 1;
}

Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
Console.Title = "Pesca - Roblox";
var closeRequested = false;
var stopFishingRequested = false;
var cycleLogPath = Path.Combine(AppContext.BaseDirectory, "logs", $"pesca-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log");

var buildIdentity = typeof(MouseSequence).Assembly.ManifestModule.ModuleVersionId;
var executionDetails = $"Compilación {buildIdentity}; lanzamiento configurado: {MouseSequence.MinimumCastHoldMilliseconds}–{MouseSequence.MaximumCastHoldMilliseconds} ms; archivo: {typeof(MouseSequence).Assembly.Location}";
if (args.Length == 1 && args[0] == "--version")
{
    Console.WriteLine(executionDetails);
    return 0;
}

if (args.Length == 1 && args[0] == "--list-screens")
{
    ShowScreens();
    return 0;
}

if (args.Length == 1 && args[0] == "--dry-run")
{
    return RunFishing(true);
}

if (args.Length != 0)
{
    Console.Error.WriteLine("Uso: Pesca [--list-screens | --dry-run | --version]");
    return 1;
}

ReportCycle(executionDetails);

while (!closeRequested)
{
    Console.WriteLine();
    Console.WriteLine("=== MENÚ PRINCIPAL ===");
    Console.WriteLine("1) Ejecutar pesca");
    Console.WriteLine("Q) Interrumpir la pesca y volver al menú");
    Console.WriteLine("S) Salir");
    Console.Write("Selecciona una opción: ");

    var choice = ReadMenuChoice();
    Console.WriteLine(choice);

    switch (choice)
    {
        case null:
        case "S":
            closeRequested = true;
            break;
        case "1":
            var menuWindow = RobloxWindowActivator.ForegroundWindow;
            RunFishingWithRecovery();
            if (!closeRequested && menuWindow != 0)
            {
                RobloxWindowActivator.TryShowMenu(menuWindow);
            }
            break;
        case "Q":
            Console.WriteLine("La pesca está detenida.");
            break;
        case "":
            break;
        default:
            Console.WriteLine("Opción no válida. Usa 1, Q o S.");
            break;
    }
}

Console.WriteLine("Programa cerrado.");
return 0;

string? ReadMenuChoice()
{
    Task<string?>? redirectedLine = Console.IsInputRedirected ? Console.In.ReadLineAsync() : null;
    while (true)
    {
        if (redirectedLine?.IsCompleted == true)
        {
            var line = redirectedLine.GetAwaiter().GetResult();
            if (line != null) return line.Trim().ToUpperInvariant();
            if (!Environment.UserInteractive) return null;
            redirectedLine = null;
        }

        if (!Console.IsInputRedirected && Console.KeyAvailable)
        {
            return char.ToUpperInvariant(Console.ReadKey(intercept: true).KeyChar).ToString();
        }

        // El menú también responde cuando Roblox conserva el foco.
        if (RobloxWindowActivator.IsKeyDown(0x31) || RobloxWindowActivator.IsKeyDown(0x61)) return "1";
        if (RobloxWindowActivator.IsKeyDown(0x53)) return "S";
        Thread.Sleep(25);
    }
}

bool ShouldStopFishing()
{
    if (closeRequested || stopFishingRequested) return true;

    if (RobloxWindowActivator.IsKeyDown(0x53)) // S
    {
        closeRequested = true;
        stopFishingRequested = true;
        return true;
    }

    if (RobloxWindowActivator.IsKeyDown(0x51) || RobloxWindowActivator.IsKeyDown(0x1B)) // Q, Esc
    {
        stopFishingRequested = true;
        return true;
    }

    if (!Console.IsInputRedirected)
    {
        try
        {
            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true).Key;
                if (key == ConsoleKey.S) closeRequested = true;
                if (key is ConsoleKey.Q or ConsoleKey.Escape or ConsoleKey.S)
                {
                    stopFishingRequested = true;
                    return true;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            // Las teclas globales siguen disponibles si se desconecta la consola.
        }
    }

    return false;
}

void RunFishingWithRecovery()
{
    stopFishingRequested = false;
    using var recoveryCancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler onSessionCancel = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        stopFishingRequested = true;
        recoveryCancellation.Cancel();
    };
    Console.CancelKeyPress += onSessionCancel;

    try
    {
        bool WaitBeforeRetry(int milliseconds)
        {
            ReportCycle($"Reinicio automático en {milliseconds / 1000} segundos. Q: menú. S: salir.");
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < milliseconds)
            {
                if (recoveryCancellation.IsCancellationRequested || ShouldStopFishing()) return false;
                var remaining = milliseconds - (int)timer.ElapsedMilliseconds;
                if (remaining > 0) recoveryCancellation.Token.WaitHandle.WaitOne(Math.Min(25, remaining));
            }

            if (recoveryCancellation.IsCancellationRequested || ShouldStopFishing()) return false;
            ReportCycle("Reiniciando la pesca: buscando de nuevo Roblox y su pantalla...");
            return true;
        }

        FishingRecovery.Run(() =>
        {
            if (recoveryCancellation.IsCancellationRequested || ShouldStopFishing()) return 0;
            var result = RunFishing(false);
            return stopFishingRequested || recoveryCancellation.IsCancellationRequested ? 0 : result;
        }, WaitBeforeRetry, exception =>
            ReportCycle($"Error de pesca ({exception.GetType().Name}): {exception.Message}"));
    }
    finally
    {
        Console.CancelKeyPress -= onSessionCancel;
    }
}

void ReportCycle(string message)
{
    try
    {
        Console.WriteLine(message);
    }
    catch (Exception exception) when (exception is IOException or InvalidOperationException)
    {
        // Conserva el registro en archivo aunque falle la salida de la consola.
    }
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(cycleLogPath)!);
        File.AppendAllText(cycleLogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        // La pesca puede continuar aunque no se pueda escribir el registro.
    }
}

static void ShowScreens()
{
    var screens = Screen.AllScreens;
    Console.WriteLine($"Pantallas detectadas: {screens.Length}");
    for (var index = 0; index < screens.Length; index++)
    {
        var screen = screens[index];
        Console.WriteLine($"  {index + 1}: {screen.DeviceName} | {screen.Bounds.Width}x{screen.Bounds.Height} | posición ({screen.Bounds.X}, {screen.Bounds.Y}){(screen.Primary ? " | principal" : "")}");
    }
}

int RunFishing(bool dryRun)
{
    Console.WriteLine("Buscando el proceso y la ventana de Roblox...");
    var matches = new List<(int Id, string Name, nint Window)>();
    var unreadableProcesses = 0;

    foreach (var process in Process.GetProcesses())
    {
        using (process)
        {
            try
            {
                var name = process.ProcessName;
                if (name.Contains("Roblox", StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add((process.Id, name, RobloxWindowActivator.FindWindow(process.Id)));
                }
            }
            catch (InvalidOperationException)
            {
                // Un proceso puede terminar mientras se consulta la lista.
            }
            catch (Win32Exception)
            {
                unreadableProcesses++;
            }
        }
    }

    Console.WriteLine($"{"PID",-10} {"Proceso",-28} Pantalla");
    foreach (var match in matches.OrderBy(match => match.Id))
    {
        var display = match.Window == 0 ? "sin ventana visible" : Screen.FromHandle(match.Window).DeviceName;
        Console.WriteLine($"{match.Id,-10} {match.Name,-28} {display}");
    }

    if (unreadableProcesses > 0)
    {
        Console.Error.WriteLine($"No se pudieron consultar {unreadableProcesses} proceso(s) por restricciones de acceso.");
    }

    var candidates = matches
        .Where(match => match.Window != 0 &&
            (match.Name.Equals("RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase) ||
             match.Name.Equals("RobloxPlayer", StringComparison.OrdinalIgnoreCase)))
        .OrderBy(match => match.Id)
        .ToList();

    if (candidates.Count == 0)
    {
        Console.Error.WriteLine("No se ha encontrado una ventana del jugador de Roblox en este escritorio. Abre el juego y ejecuta Pesca en la misma sesión interactiva.");
        return 1;
    }

    var selected = candidates[0];
    var targetScreen = Screen.FromHandle(selected.Window);
    Console.WriteLine($"Roblox identificado: {selected.Name} (PID {selected.Id})");
    Console.WriteLine($"Pantalla de Roblox: {targetScreen.DeviceName}");
    ShowScreens();
    Console.WriteLine("Ciclo: lanzamiento aleatorio de 1,2–1,3 s; espera visual del pez; 14 clics separados 120 ms; pausa de 5 s antes de volver a lanzar.");

    if (dryRun)
    {
        Console.WriteLine("Comprobación terminada. No se ha activado la ventana ni enviado pulsaciones.");
        return 0;
    }

    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler onCancel = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };
    Console.CancelKeyPress += onCancel;

    void CheckControls(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (ShouldStopFishing()) throw new FishingInterruptedException();
    }

    try
    {
        CheckControls(cancellation.Token);
        var result = RobloxWindowActivator.Activate(selected.Window, targetScreen);
        Console.WriteLine($"Roblox en primer plano: {(result.InForeground ? "sí" : "no")}");
        Console.WriteLine($"Roblox maximizado: {(result.Maximized ? "sí" : "no")}");
        if (!result.OnTargetScreen || !result.InForeground || !result.Maximized)
        {
            Console.Error.WriteLine("No se ha podido activar Roblox. No se enviarán pulsaciones.");
            return 1;
        }

        Cursor.Position = RobloxWindowActivator.GetScreenCenter(targetScreen);
        var mouse = new WindowsMouseInput(selected.Window, targetScreen);
        mouse.EnsureTarget();
        Console.WriteLine($"Cursor en Roblox: ({Cursor.Position.X}, {Cursor.Position.Y}). Q: menú. S: salir.");

        void Wait(int milliseconds, CancellationToken token)
        {
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < milliseconds)
            {
                CheckControls(token);
                mouse.EnsureTarget();
                var remaining = milliseconds - (int)timer.ElapsedMilliseconds;
                if (remaining > 0)
                {
                    token.WaitHandle.WaitOne(Math.Min(25, remaining));
                }
            }

            CheckControls(token);
            mouse.EnsureTarget();
        }

        var detector = new FishBiteDetector(selected.Window);

        void WaitForCue(bool present, int timeoutMilliseconds, CancellationToken token)
        {
            var timer = Stopwatch.StartNew();
            var confirmations = 0;
            while (timer.ElapsedMilliseconds < timeoutMilliseconds)
            {
                CheckControls(token);
                mouse.EnsureTarget();
                confirmations = detector.IsVisible() == present ? confirmations + 1 : 0;
                if (confirmations >= 2) return;
                Wait(100, token);
            }

            throw new TimeoutException(present
                ? "No apareció el indicador inferior del pez en 15 segundos."
                : "El indicador de captura no ha desaparecido. Revisa la partida.");
        }

        void WaitForFish(CancellationToken token)
        {
            ReportCycle("Esperando al pez en la parte inferior (normalmente 5–7 s)...");
            WaitForCue(true, 15000, token);
            ReportCycle("Indicador de captura detectado. Iniciando clics.");
        }

        var sequence = new MouseSequence(mouse, Wait, WaitForFish);
        var cycle = 0;
        Wait(250, cancellation.Token);
        while (true)
        {
            CheckControls(cancellation.Token);
            WaitForCue(false, 10000, cancellation.Token);
            const int pauseAfterClicksMilliseconds = 5000;
            var castHoldMilliseconds = Random.Shared.Next(MouseSequence.MinimumCastHoldMilliseconds, MouseSequence.MaximumCastHoldMilliseconds + 1);
            ReportCycle($"Ciclo {++cycle}: lanzamiento de {castHoldMilliseconds / 1000.0:F3} s; {MouseSequence.ClickCount} clics al aparecer el pez; pausa posterior de {pauseAfterClicksMilliseconds / 1000.0:F3} s.");
            var completedClicks = sequence.Run(castHoldMilliseconds, pauseAfterClicksMilliseconds, cancellation.Token);
            ReportCycle($"Ciclo {cycle} completado: {completedClicks} clics.");
        }
    }
    catch (OperationCanceledException exception) when (
        exception is FishingInterruptedException || cancellation.IsCancellationRequested || stopFishingRequested)
    {
        ReportCycle(closeRequested ? "Cerrando el programa..." : "Pesca interrumpida. Volviendo al menú principal...");
        return 0;
    }
    catch (Exception exception)
    {
        ReportCycle($"Pesca detenida: {exception.Message}");
        return 1;
    }
    finally
    {
        Console.CancelKeyPress -= onCancel;
    }
}
