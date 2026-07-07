# AvrdudeUI — a macOS-native GUI for AVRDUDE

A macOS port of [AVRDUDESS](https://github.com/ZakKemble/AVRDUDESS), rewritten in
[Avalonia UI](https://avaloniaui.net/) on **.NET 10**. All the non-UI logic
(configuration parser, command builder, presets, MCU/programmer models,
translations) is ported from the original C# codebase with light adaptations;
the WinForms UI has been rebuilt from scratch as XAML.

- Native on Apple Silicon (no Rosetta, no Mono, no `--arch=32`).
- Uses the system `avrdude` from Homebrew — no bundled binary to fall behind.
- Cross-platform bonus: the same source builds for Linux and Windows.

## Prerequisites

1. macOS 11 (Big Sur) or later on Apple Silicon.
2. `avrdude` installed via Homebrew:

   ```sh
   brew install avrdude
   ```

   The app looks for `avrdude` in `/opt/homebrew/bin`, `/usr/local/bin`,
   `/opt/local/bin`, and on `$PATH`; and for `avrdude.conf` in
   `/opt/homebrew/etc`, `/usr/local/etc`, `/etc`, and `/opt/local/etc`.
   Override either from **Options…** if your install is elsewhere.

## Install

### Prebuilt bundle

Grab `AvrdudeUI.app` from the releases page and drag it to `/Applications`.
The bundle is ad-hoc signed; the first time you launch it, right-click ▸ Open to
approve running it.

### Build from source

Requires the .NET 10 SDK (`brew install dotnet@10` or download from
[dot.net](https://dot.net)).

```sh
git clone <this-repo>
cd avrdudeui
./build/build.sh
open build/AvrdudeUI.app
```

`build.sh` runs `dotnet publish -c Release -r osx-arm64 --self-contained`,
assembles a `.app` bundle around the output, and ad-hoc signs it. To sign with a
Developer ID (for distribution), set `CODESIGN_IDENTITY`:

```sh
CODESIGN_IDENTITY="Developer ID Application: Your Name (TEAMID)" ./build/build.sh
```

For iterative development, skip the bundle step:

```sh
dotnet run --project src/AvrdudeUI
```

## Layout

```
src/AvrdudeUI/            Avalonia app
  ├─ Core/                Ported non-UI classes (AvrdudeUI.Core namespace)
  ├─ Services/            UI-side implementations of Core abstractions
  ├─ Views/               XAML windows + code-behind
  └─ Assets/              bits.xml, portable.txt, Languages/*.xml
build/                    Bundle template + build.sh
tools/CoreSmokeTest/      Headless Core exerciser (no UI required)
```

Config files live at `~/Library/Application Support/AvrdudeUI/` by default
(portable mode is triggered by creating a `portable.txt` file containing `Y`
next to the executable, matching AVRDUDESS behaviour).

## What's different from AVRDUDESS

- No installer, no Inno Setup — you get a `.app` you can move anywhere.
- No bundled avrdude — always uses the system one.
- Serial ports are enumerated from `/dev/cu.usbmodem*`, `/dev/cu.usbserial*`,
  `/dev/cu.SLAB_*`, `/dev/cu.wchusbserial*`, and `/dev/cu.Bluetooth*`.
- Update check uses `HttpClient` (was `WebRequest` + `ServicePointManager`).
- macOS-native config path (`~/Library/Application Support/…`) instead of
  Windows's `%AppData%`.
- UI-thread marshalling via `Dispatcher.UIThread` instead of
  `Control.InvokeRequired`.

The command-line output is byte-identical to AVRDUDESS for equivalent settings,
since `CmdLine` is the ported original with only a `Form1` → `AvrdudeSettings`
DTO refactor.

## Credits

- **Zak Kemble** — original AVRDUDESS (GNU GPL v3).
- **Simone Chifari** — fuse selector, MCU auto-detect, USBasp frequency picker,
  version-title display (contributions to upstream AVRDUDESS).
- All translators listed in `Assets/Languages/_meta.xml` and the upstream
  `Credits.txt`.

## License

GNU General Public License v3.0 (inherited from AVRDUDESS). See upstream
`License.txt` for the full text.
