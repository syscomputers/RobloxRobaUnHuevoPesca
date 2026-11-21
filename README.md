# Pesca de Roblox

Aplicación de consola para Windows y .NET 10.

Abre Roblox, equipa la caña y sitúa al personaje donde quieras pescar. Ejecuta:

~~~powershell
dotnet run --project Pesca/Pesca.csproj
~~~

- **1:** identifica el jugador y su pantalla, activa la ventana y coloca el cursor en la zona de juego. Repite la pesca hasta interrumpirla.
- **Q:** interrumpe la pesca y vuelve al menú, incluso con Roblox en primer plano.
- **S:** cierra el programa desde el menú o durante la pesca.

La primera pesca comienza tras una espera inicial de 0,25 segundos. Cada ciclo mantiene pulsado el botón izquierdo un tiempo aleatorio entre 1,2 y 1,3 segundos y espera al indicador inferior del pez, que normalmente aparece a los 5–7 segundos. Después envía 14 clics separados 120 ms y espera 5 segundos antes del siguiente lanzamiento. El siguiente ciclo comienza cuando desaparece el indicador de la captura anterior.

Roblox se maximiza al iniciar y durante la pesca. Si el cursor sale del monitor donde está Roblox, vuelve automáticamente al centro horizontal y vertical de ese monitor. Ante un error o una excepción inesperada, el programa espera 30 segundos y vuelve a identificar Roblox, su ventana y su pantalla para reiniciar los ciclos. Esto incluye la pérdida de foco, una ventana cerrada y el tiempo de espera del pez agotado. Q, Esc y Ctrl+C interrumpen la pesca y vuelven al menú; S cierra el programa. Estos controles también funcionan durante la espera de recuperación. Las teclas 1 y S funcionan desde Roblox; cada ejecución deja un registro de ciclos en la carpeta logs situada junto al ejecutable. Al iniciar se muestran el rango de lanzamiento, un identificador de compilación y la ruta del archivo cargado; --version permite consultarlos sin iniciar la pesca.

El detector reconoce el botón rojo con texto blanco que acompaña a la barra de captura observada. Está ajustado a esa interfaz; un cambio de diseño puede requerir recalibrarlo. No lee la memoria del juego.

## Comprobación

~~~powershell
dotnet run --project Pesca/Pesca.csproj -- --version
dotnet build Pesca.slnx
dotnet run --project tests/Pesca.Tests/Pesca.Tests.csproj
dotnet run --project Pesca/Pesca.csproj -- --dry-run
dotnet run --project Pesca/Pesca.csproj -- --list-screens
~~~

Las pruebas son un ejecutable sin dependencias de un framework de tests. Usan un reloj y un ratón simulados, además de recortes reales del indicador en Fixtures. No envían pulsaciones al juego. El modo --dry-run identifica la ventana sin activarla ni hacer clic.
