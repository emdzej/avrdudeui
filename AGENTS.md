# AGENTS.md

Instructions for AI coding agents working on this repo. Read this before making
non-trivial changes. Prefer to update this file when you learn something
non-obvious that a future agent will need.

## What this project is

AvrdudeUI is a macOS-native GUI for [AVRDUDE](https://github.com/avrdudes/avrdude),
ported from the WinForms/.NET 2.0 [AVRDUDESS](https://github.com/ZakKemble/AVRDUDESS)
project. It targets **Avalonia 12 on .NET 10**, runs natively on Apple Silicon,
and shells out to a system `avrdude` binary (Homebrew).

The upstream WinForms sources live in `../ext-AVRDUDESS/` and are the reference
for behaviour; port from there rather than reinventing.

## Repo layout

```
src/AvrdudeUI/
├── Core/          Ported non-UI logic. NO Avalonia / System.Windows.Forms references.
│                  Namespace: AvrdudeUI.Core
├── Services/      UI-side implementations of Core abstractions (IConsoleSink,
│                  IMsgBoxProvider). Registered at startup in App.axaml.cs.
├── Views/         XAML windows + code-behind (MainWindow + 7 dialogs).
├── Assets/        Runtime assets copied next to the assembly:
│                  bits.xml, portable.txt, Languages/*.xml, icons/*.png
├── App.axaml{.cs} Application bootstrap: Config.Load → Language.Load →
│                  MainWindow → register sink+provider → banner.
└── Program.cs     Standard Avalonia entry point.

tools/CoreSmokeTest/   Headless harness that exercises Core end-to-end with
                       no UI thread. Run this before assuming a Core change works.

build/
├── build.sh                       # Publishes + assembles + codesigns the .app
├── AvrdudeUI.app.template/        # Info.plist and Resources/AvrdudeUI.icns
├── icon/generate.py               # PIL master 1024×1024
├── icon/make-icns.sh              # sips → iconset → iconutil → .icns
├── out/          (gitignored)     # dotnet publish output
└── AvrdudeUI.app/(gitignored)     # assembled bundle
```

## Build / run / test

```sh
# Iterate quickly (no bundle, uses dev SDK):
dotnet run --project src/AvrdudeUI

# Full Release .app bundle:
./build/build.sh                   # → build/AvrdudeUI.app (ad-hoc signed)
open build/AvrdudeUI.app

# Regenerate the icon after editing build/icon/generate.py:
python3 build/icon/generate.py     # rewrites the master PNG
./build/icon/make-icns.sh          # updates the .icns in the .app.template

# Headless verification that Core still works (avrdude detection,
# conf parse, CmdLine generation). Run this whenever you touch Core:
dotnet run --project tools/CoreSmokeTest
```

Target framework is fixed at `net10.0`. Bundle RID is `osx-arm64`.

## Non-obvious constraints you WILL hit

1. **`InvariantGlobalization=true` is required.**
   macOS 26+ ships an ICU that faults in `umutablecptrie_buildImmutable` when
   .NET builds a culture-sensitive search iterator. Culture-sensitive
   `String.IndexOf(string)` at startup was crashing the app with a native
   data-abort. Set in `AvrdudeUI.csproj`. Do NOT remove without proving it works
   on the current macOS. All strings this app compares are ASCII (MCU IDs,
   avrdude stderr, XML tags) so invariant is safe.

2. **First-run file access throws `DirectoryNotFoundException`, not
   `FileNotFoundException`.** On macOS/Linux, when `~/Library/Application
   Support/AvrdudeUI/` doesn't exist yet, `XmlFile.Read()` fails with
   `DirectoryNotFoundException`. `Config.Load()` and `Presets.Load()` both
   catch it. If you add another first-run file read, catch both.

3. **UI thread affinity for brushes and other Avalonia types.**
   `SolidColorBrush` (and every other `AvaloniaObject`) must be created on
   the UI thread. `UiConsoleSink.Write` receives calls from any thread
   (avrdude's stderr reader is a background thread) — brush construction and
   `Inlines.Add(...)` must happen inside a `Dispatcher.UIThread.Post` closure.
   Brushes are cached per RGBA to avoid re-alloc churn.

4. **Never block the UI thread inside `UiMsgBoxProvider`.**
   `ShowDialog(...).GetAwaiter().GetResult()` on the UI thread self-deadlocks
   the dispatcher — the window ends up with 0×0 bounds and looks like it
   "didn't open". Error/Warning/Info are fire-and-forget non-modal.
   `ShowConfirm` blocks synchronously ONLY when called from a background
   thread (via `Dispatcher.UIThread.Invoke`); if called from the UI thread it
   degrades to non-modal + returns `Cancel`.

5. **`CmdLine` consumes `AvrdudeSettings`, not `MainWindow`.**
   The upstream code passes `Form1` directly into `CmdLine` — we refactored
   that into a plain DTO so Core has no UI dependency. If you add a new
   command-line flag, extend `AvrdudeSettings` and both `MainWindow.RegenerateCmdLine`
   (populates it) and the appropriate `CmdLine.generateXxx()` method.

6. **`Language.Translation.get(key)`**
   - Keys starting with `_` are looked up in the loaded language XML.
   - Keys not starting with `_` are passed through verbatim.
   - Missing keys return the input string, not a placeholder.
   - Language files under `Assets/Languages/` are copied to the assembly
     directory at build time. `_meta.xml` lists available languages;
     `english.xml` is the fallback.

7. **`avrdude` and `avrdude.conf` lookup order**
   Managed by `Executable.searchForBinary` and `Avrdude.loadConfig`. Checks
   user override (Options dialog) → app dir → cwd → PATH → these fallbacks:
   ```
   /opt/homebrew/{bin,etc}   (Apple Silicon Homebrew)
   /usr/local/{bin,etc}      (Intel Homebrew / manual)
   /etc, /opt/local/{bin,etc}
   ```
   If you add another search location, update both places.

8. **`avr-size` is optional.**
   Homebrew's `avrdude` formula doesn't include it. Use
   `Executable.load(..., optional: true, installHint: "…")` when adding
   another optional binary. The console shows a warning instead of an error.

9. **Portable mode.**
   A `portable.txt` file starting with `Y` next to the executable switches
   `XmlFile` to store config/presets alongside the app instead of in
   `~/Library/Application Support/AvrdudeUI/`. `Portable.IsPortable` is
   evaluated once and cached.

10. **`AppConsole` / `AppMsgBox` are static facades.**
    Core code calls `Util.consoleWriteLine(...)` and `MsgBox.error(...)` —
    these forward to the registered `IConsoleSink` / `IMsgBoxProvider`. Before
    the UI is up (very early startup, headless smoke test), sink writes go
    to `Console.Error` in the smoke test's stdout sink or drop silently.

11. **XML file paths.** `XmlFile<T>(fileName)` places the file under
    `AssemblyData.AppDataDir` (or app dir in portable mode). `XmlFile<T>(path,
    isFullPath: true)` treats the argument as an absolute path — use this for
    per-language XMLs and imported preset files.

## Code style

- Nullable is **off** globally (the ported code was written pre-nullable).
  Don't turn it on file-by-file; it produces noise.
- No unnecessary comments. Well-named identifiers are enough. Add a comment
  when a WHY is non-obvious (workaround, invariant, tricky ordering).
- No trailing summary blocks in commits or PRs — the diff speaks for itself.
- Prefer editing existing files over creating new ones. New `.cs` files should
  match the existing capitalization/namespace conventions
  (`AvrdudeUI.Core.*`, `AvrdudeUI.Services.*`, `AvrdudeUI.Views.*`).
- Don't add error handling for scenarios that can't happen. Trust internal
  code and framework guarantees. Only validate at boundaries (user paths,
  external process output).

## What NOT to do

- Don't reintroduce culture-sensitive string operations. If you truly need
  one, add an `[Ordinal]`-tagged path guarded by a runtime check, or convert
  to ordinal explicitly.
- Don't add `System.Windows.Forms` references anywhere. If you find yourself
  wanting `Control`, `ComboBox`, `TextBox`, etc., that's Core→UI leakage —
  refactor via an abstraction (see how `IConsoleSink`/`IMsgBoxProvider` are
  set up).
- Don't call `.Show()` / `.ShowDialog()` from Core. Push the operation up
  through a Services interface.
- Don't hardcode paths under `/Users/…` in code (they're OK in this file).
  Use `AssemblyData.directory`, `AssemblyData.AppDataDir`, or
  `Environment.GetFolderPath`.
- Don't skip the smoke test after Core changes. It's ~5 seconds and catches
  regressions before UI layering hides them.

## When you're done

- `dotnet build src/AvrdudeUI/AvrdudeUI.csproj` — must be 0 warnings, 0 errors.
- `dotnet run --project tools/CoreSmokeTest` — must print "Smoke test complete".
- If you changed anything visible: `./build/build.sh && open build/AvrdudeUI.app`
  and confirm the window opens and the log shows the startup banner.

## Reference

- Upstream AVRDUDESS: https://github.com/ZakKemble/AVRDUDESS
- avrdude project: https://github.com/avrdudes/avrdude
- Avalonia docs: https://docs.avaloniaui.net/
- .NET 10 release notes: https://github.com/dotnet/core/tree/main/release-notes/10.0
