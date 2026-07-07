// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AvrdudeUI.Core
{
    public enum UpdateCheckState
    {
        Delay,
        Begin,
        Failed,
        Success
    }

    public class UpdateCheckEventArgs : EventArgs
    {
        public UpdateCheckState State { get; set; }
        public UpdateCheckEventArgs(UpdateCheckState state) { State = state; }
    }

    [XmlRoot("version")]
    public class UpdateData
    {
        public int major;
        public int minor;
        public long date;
        public string updateAddr = "";

        [XmlArray("releases")]
        [XmlArrayItem("release")]
        public List<UpdateReleaseEntry> releases = new List<UpdateReleaseEntry>();

        [XmlIgnore] public Version currentVersion;

        [XmlIgnore]
        public UpdateReleaseEntry Latest
        {
            get
            {
                if (releases == null) return null;

                UpdateReleaseEntry latestRelease = null;
                releases.ForEach(release =>
                {
                    if (latestRelease == null || release.Version.CompareTo(latestRelease.Version) > 0)
                        latestRelease = release;
                });
                return latestRelease;
            }
        }

        [XmlIgnore]
        public bool UpdateAvailable
        {
            get
            {
                var latest = Latest;
                return latest != null && latest.Version.CompareTo(currentVersion) > 0;
            }
        }

        public UpdateData()
        {
#if DEBUG
            currentVersion = new Version(0, 1);
#else
            currentVersion = new Version(AssemblyData.version.Major, AssemblyData.version.Minor);
#endif
        }
    }

    public class UpdateReleaseEntry
    {
        public int major;
        public int minor;
        public long date;
        public string info;

        [XmlIgnore] public DateTime Date => new DateTime(1970, 1, 1).AddSeconds(date);
        [XmlIgnore] public Version Version => new Version(major, minor);
    }

    // Original code used ServicePointManager + HttpWebRequest with manual TLS setup.
    // .NET 8+ picks TLS automatically and HttpClient is the recommended API — swapped in.
    public class UpdateCheck
    {
        private const string UPDATE_ADDR = "https://versions.zakkemble.net/avrdudess2.xml";

        public Exception Ex { get; private set; }
        public UpdateData UpdateData = new UpdateData();

        private UpdateCheckState _state;
        public UpdateCheckState State
        {
            get => _state;
            private set
            {
                _state = value;
                OnUpdateCheck?.Invoke(this, new UpdateCheckEventArgs(value));
            }
        }

        public event EventHandler<UpdateCheckEventArgs> OnUpdateCheck;

        public UpdateCheck() { }

        public void Run()
        {
            if (!Needed() || !Config.Prop.checkForUpdates)
                return;

            State = UpdateCheckState.Delay;

            var t = new Thread(async () =>
            {
                Thread.Sleep(5000);
                State = UpdateCheckState.Begin;
                try
                {
                    await NowAsync();
                    Config.Prop.updateCheck = Util.UnixTimeStamp();
                    State = UpdateCheckState.Success;
                }
                catch (Exception ex)
                {
                    Ex = ex;
                    State = UpdateCheckState.Failed;
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private bool Needed()
        {
#if DEBUG
            return true;
#else
            return Util.UnixTimeStamp() - Config.Prop.updateCheck > TimeSpan.FromDays(1).TotalSeconds;
#endif
        }

        private async Task NowAsync()
        {
            UpdateData.releases = new List<UpdateReleaseEntry>();

            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                $"Mozilla/5.0 (compatible; AvrdudeUI VERSION CHECKER {AssemblyData.version})");

            await using var responseStream = await http.GetStreamAsync(UPDATE_ADDR);
            UpdateData = (UpdateData)new XmlSerializer(typeof(UpdateData)).Deserialize(responseStream);

            if (UpdateData.releases.Count == 0)
                throw new Exception(Language.Translation.get("_UPDATE_BADXML"));
        }
    }
}
