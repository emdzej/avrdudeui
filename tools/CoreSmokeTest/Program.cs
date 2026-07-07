using System;
using System.Drawing;
using AvrdudeUI.Core;

// Exercises the Core layer end-to-end from a plain console app:
//   1. Load config
//   2. Load languages
//   3. Locate avrdude, parse avrdude.conf, report version + counts
// Any wiring that requires WinForms would blow up on macOS; a green run here
// proves the port survives without a UI thread.

var stdout = new StdoutSink();
AppConsole.SetSink(stdout);

Console.WriteLine("=== AvrdudeUI Core smoke test ===");
Console.WriteLine($"AppDataDir: {AssemblyData.AppDataDir}");
Console.WriteLine($"AssemblyDir: {AssemblyData.directory}");
Console.WriteLine();

Config.Load();
Console.WriteLine($"Config loaded. configVersion={Config.Prop.configVersion} language={Config.Prop.language}");

Language.Translation.Load();
Console.WriteLine($"Languages available: {Language.Translation.Languages.Count}");
Console.WriteLine();

var avrdude = new Avrdude();
avrdude.load();

Console.WriteLine();
Console.WriteLine($"avrdude version : {(string.IsNullOrEmpty(avrdude.version) ? "<not detected>" : avrdude.version)}");
Console.WriteLine($"Programmers     : {avrdude.programmers.Count}");
Console.WriteLine($"MCUs            : {avrdude.mcus.Count}");
if (avrdude.mcus.Count > 0)
{
    var sample = avrdude.mcus.Find(m => m.id == "m328p") ?? avrdude.mcus[0];
    Console.WriteLine($"Sample MCU      : {sample.id} '{sample.desc}' sig={sample.signature} flash={sample.flash} eeprom={sample.eeprom}");
}
if (avrdude.programmers.Count > 0)
{
    var sample = avrdude.programmers.Find(p => p.id == "usbasp") ?? avrdude.programmers[0];
    Console.WriteLine($"Sample prog     : {sample.id} '{sample.desc}'");
}

Console.WriteLine();
Console.WriteLine("=== CmdLine round-trip ===");
var settings = new AvrdudeSettings
{
    prog = avrdude.programmers.Find(p => p.id == "usbasp"),
    mcu  = avrdude.mcus.Find(m => m.id == "m328p"),
    port = "usb",
    flashFile = "/tmp/blink.hex",
    flashFileOperation = FileOp.Write,
    flashFileFormat = "i",
    verbosity = 1
};
var cmd = new CmdLine(settings);
Console.WriteLine($"Flash write args: {cmd.generateFlash()}");

Console.WriteLine();
Console.WriteLine("=== Smoke test complete ===");

// Route AppConsole writes to stdout — the UI adapter would otherwise stay unregistered
// and Util.consoleWriteLine calls from inside Core would silently drop.
sealed class StdoutSink : IConsoleSink
{
    public void Write(string text, Color color) => Console.Write(text);
    public void Clear() { }
}
