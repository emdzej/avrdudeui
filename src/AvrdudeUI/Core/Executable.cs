// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2014-2024, Zak Kemble. GNU GPL v3.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AvrdudeUI.Core
{
    public abstract class Executable
    {
        private Process p;
        private Action<object> onFinish;
        private object param;
        public event EventHandler OnProcessStart;
        public event EventHandler OnProcessEnd;
        protected string binary;
        private bool enableConsoleUpdate;
        protected string outputLogStdErr { get; private set; } = string.Empty;
        protected string outputLogStdOut { get; private set; } = string.Empty;
        private Thread tConUpt;
        private readonly ManualResetEvent exitWait = new ManualResetEvent(false);
        private readonly ManualResetEvent stdOutWait = new ManualResetEvent(false);
        private readonly ManualResetEvent stdErrWait = new ManualResetEvent(false);

        public enum OutputTo { Memory, Console }

        private enum Stream { StdOut, StdErr }

        // Extra directories to probe when PATH doesn't already include them —
        // covers common Homebrew and Unix package layouts on macOS.
        private static readonly string[] ExtraSearchDirs = new[]
        {
            "/opt/homebrew/bin",   // Apple Silicon Homebrew
            "/usr/local/bin",      // Intel Homebrew + Unix norm
            "/opt/local/bin",      // MacPorts
            "/usr/bin"
        };

        // Whether a missing binary is a hard error (blocks main workflows) or an
        // advisory. avrdude itself is required; avr-size is not, so Avrsize passes
        // optional: true and gets a friendlier console message.
        protected void load(string defaultBinaryName, string filePath, bool enableConsoleWrite = true, bool optional = false, string installHint = null)
        {
            binary = searchForBinary(defaultBinaryName, filePath);

            if (binary == null)
            {
                if (optional)
                {
                    var hint = string.IsNullOrEmpty(installHint) ? "" : $" ({installHint})";
                    Util.consoleWarning($"{defaultBinaryName} not found — related features disabled{hint}");
                }
                else
                {
                    Util.consoleError("_EXECMISSING", defaultBinaryName);
                }
                return;
            }

            if (enableConsoleWrite && tConUpt == null)
            {
                tConUpt = new Thread(new ThreadStart(tConsoleUpdate));
                tConUpt.IsBackground = true;
                tConUpt.Start();
            }
        }

        private string searchForBinary(string defaultBinaryName, string filePath)
        {
            if (PlatformUtil.IsWindows)
                defaultBinaryName += ".exe";

            // 1. User-provided explicit path
            if (!string.IsNullOrEmpty(filePath))
                return File.Exists(filePath) ? filePath : null;

            // 2. App bundle / working dir
            var candidate = Path.Combine(AssemblyData.directory, defaultBinaryName);
            if (File.Exists(candidate)) return candidate;

            candidate = Path.Combine(Directory.GetCurrentDirectory(), defaultBinaryName);
            if (File.Exists(candidate)) return candidate;

            // 3. Directories on PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var paths = pathEnv.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in paths)
            {
                candidate = Path.Combine(p, defaultBinaryName);
                if (File.Exists(candidate)) return candidate;
            }

            // 4. Well-known extra locations (Homebrew, MacPorts, /usr/bin)
            foreach (var dir in ExtraSearchDirs)
            {
                candidate = Path.Combine(dir, defaultBinaryName);
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }

        protected bool launch(string args, Action<object> onFinish, object param, OutputTo outputTo)
        {
            if (isActive()) return false;

            outputLogStdErr = string.Empty;
            outputLogStdOut = string.Empty;

            if (binary == null || !File.Exists(binary))
                return false;

            this.onFinish = onFinish;
            this.param = param;

            return launch(args, outputTo);
        }

        private bool launch(string args, OutputTo outputTo)
        {
            exitWait.Reset();
            stdOutWait.Reset();
            stdErrWait.Reset();

            var tmp = new Process();
            tmp.StartInfo.FileName = binary;
            tmp.StartInfo.Arguments = args;
            tmp.StartInfo.CreateNoWindow = true;
            tmp.StartInfo.UseShellExecute = false;
            tmp.StartInfo.RedirectStandardOutput = true;
            tmp.StartInfo.RedirectStandardError = true;
            tmp.EnableRaisingEvents = true;
            if (outputTo == OutputTo.Memory)
            {
                tmp.OutputDataReceived += outputLogHandler;
                tmp.ErrorDataReceived += errorLogHandler;
            }
            tmp.Exited += p_Exited;

            try
            {
                tmp.Start();
            }
            catch (Exception ex)
            {
                Util.consoleError("_EXECFAIL", ex.Message);
                return false;
            }

            OnProcessStart?.Invoke(this, EventArgs.Empty);

            enableConsoleUpdate = (outputTo == OutputTo.Console);
            p = tmp;

            if (outputTo == OutputTo.Memory)
            {
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }
            else
            {
                stdOutWait.Set();
                stdErrWait.Set();
            }

            return true;
        }

        private void p_Exited(object sender, EventArgs e)
        {
            exitWait.Set();
            OnProcessEnd?.Invoke(this, EventArgs.Empty);
            onFinish?.Invoke(param);
            onFinish = null;
        }

        // Progress bars don't emit newlines, so async line-based reads miss them.
        // Poll stderr in a background thread instead — matches original AVRDUDESS behavior.
        private void tConsoleUpdate()
        {
            while (true)
            {
                Thread.Sleep(15);

                if (!enableConsoleUpdate) continue;

                try
                {
                    if (p != null)
                    {
                        var buff = new char[256];
                        if (p.StandardError.Read(buff, 0, buff.Length) > 0)
                        {
                            var s = new string(buff);
                            Util.consoleWrite(s);
                        }
                    }
                }
                catch (Exception)
                {
                    // Swallow — the process may have exited between the null-check and Read.
                }
            }
        }

        private bool logger(string s, Stream stream)
        {
            if (s != null)
            {
                var tmp = s.Replace("\0", string.Empty) + Environment.NewLine;
                if (stream == Stream.StdErr) outputLogStdErr += tmp;
                else if (stream == Stream.StdOut) outputLogStdOut += tmp;
                return true;
            }

            return false;
        }

        private void outputLogHandler(object sender, DataReceivedEventArgs e)
        {
            if (!logger(e.Data, Stream.StdOut))
                stdOutWait.Set();
        }

        private void errorLogHandler(object sender, DataReceivedEventArgs e)
        {
            if (!logger(e.Data, Stream.StdErr))
                stdErrWait.Set();
        }

        protected bool isActive() => !p?.HasExited ?? false;

        public bool kill()
        {
            if (!isActive()) return false;
            p.Kill();
            return true;
        }

        protected void waitForExit()
        {
            if (isActive())
                exitWait.WaitOne();

            stdOutWait.WaitOne();
            stdErrWait.WaitOne();
        }
    }
}
