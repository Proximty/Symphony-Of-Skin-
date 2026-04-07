using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

class PaalMuziek
{
    // ── Config (loaded from config.json, hot-reloaded every 5s) ───────────────
    static string devicePath   = "/dev/input/event6";
    static string basisPad     = "/home/admin/Symphony-Of-Skin-/muziek";
    static string audioDevice  = "default";
    static string outputDriver = "";  // "pulse" for PulseAudio, "alsa" for ALSA
    static int    volume       = 200;   // mpg123 -f flag (100 = normal, 200 = 2x)
    static bool   loopTracks   = false;
    static bool   debugLog     = true;
    static Dictionary<string, string> mappen = new();

    static readonly string ConfigPath = Path.Combine(
        AppContext.BaseDirectory, "config.json");

    // ── State ─────────────────────────────────────────────────────────────────
    static Random            rng               = new();
    static Dictionary<string, Process> actieveSpelers = new();
    static readonly string[] _metadataLabels  = ["Title:", "Artist:", "Album:", "Year:", "Genre:", "Comment:"];
    static HashSet<string>      actieveToetsenSessie = new();
    static HashSet<int>      ingedrukteToetsen  = new();

    [StructLayout(LayoutKind.Sequential)]
    struct InputEvent {
        public IntPtr TimeSec; public IntPtr TimeUSec;
        public ushort Type; public ushort Code; public int Value;
    }

    // ── Entry point ───────────────────────────────────────────────────────────
    static async Task Main()
    {
        Console.WriteLine("=== Symphony-Of-Skin | Multi-Track Mode ===");

        LaadConfig();   // first load

        if (!File.Exists(devicePath)) {
            Console.WriteLine($"FOUT: Input device niet gevonden: {devicePath}");
            Console.WriteLine("  → Run:  ls /dev/input/event*   en pas config.json aan.");
            return;
        }
        if (!Directory.Exists(basisPad)) {
            Console.WriteLine($"FOUT: Muziekmap niet gevonden: {basisPad}");
            Console.WriteLine("  → Pas 'musicBasePath' in config.json aan.");
            return;
        }

        _ = Task.Run(LeesRawInput);
        _ = Task.Run(HotReloadConfig);

        Log($"Luisteren op {devicePath}");
        Log($"Audio device : {audioDevice}   Volume: {volume}   Driver: {outputDriver}");
        Log($"Mappings     : {string.Join(", ", mappen.Select(kv => $"{kv.Key}={kv.Value}"))}");
        Log("Druk een Makey Makey-knop in...");

        while (true)
        {
            List<int> huidigeLijst;
            lock (ingedrukteToetsen) { huidigeLijst = ingedrukteToetsen.ToList(); }

            List<string> actieveCombinaties = new List<string>();
            lock (mappen) {
                foreach (var map in mappen.Keys) {
                    var keys = map.Split(',').Select(int.Parse).ToList();
                    if (keys.All(k => huidigeLijst.Contains(k))) {
                        actieveCombinaties.Add(map);
                    }
                }
            }

            List<string> filteredCombinaties = new List<string>();
            foreach (var combo in actieveCombinaties) {
                var comboKeys = combo.Split(',').Select(int.Parse).ToList();
                bool isSubset = actieveCombinaties.Any(other => 
                    other != combo && 
                    !comboKeys.Except(other.Split(',').Select(int.Parse)).Any()
                );
                if (!isSubset) filteredCombinaties.Add(combo);
            }

            foreach (string combo in filteredCombinaties)
            {
                lock (actieveToetsenSessie) {
                    if (!actieveToetsenSessie.Contains(combo)) {
                        actieveToetsenSessie.Add(combo);
                        string captured = combo;
                        _ = Task.Run(() => SpeelTrack(captured));
                    }
                }
            }

            // Stop tracks whose key was released
            List<string> gestopt = new List<string>();
            lock (actieveToetsenSessie) {
                var teVerwijderen = actieveToetsenSessie.Where(c => !filteredCombinaties.Contains(c)).ToList();
                foreach (var c in teVerwijderen) {
                    actieveToetsenSessie.Remove(c);
                    gestopt.Add(c);
                }
            }

            foreach (var stopCombo in gestopt) {
                lock (actieveSpelers) {
                    if (actieveSpelers.TryGetValue(stopCombo, out var p)) {
                        try { if (!p.HasExited) p.Kill(); } catch { }
                        actieveSpelers.Remove(stopCombo);
                        Log($"[STOP] Track gestopt (toets losgelaten: {stopCombo})");
                    }
                }
            }

            await Task.Delay(50);
        }
    }

    // ── Config loader (also used for hot-reload) ──────────────────────────────
    static void LaadConfig()
    {
        try {
            if (!File.Exists(ConfigPath)) {
                Console.WriteLine($"[CONFIG] Niet gevonden op {ConfigPath}. Gebruik standaardwaarden.");
                // keep existing defaults
                if (mappen.Count == 0)
                    mappen = new() {
                        { "103", "2000s" }, { "108", "2010s" }, { "105", "Anime" },
                        { "106", "EDM"   }, { "57",  "Game"  }, { "17",  "Pop"   }, { "103,108", "Secret" }
                    };
                return;
            }

            var json = File.ReadAllText(ConfigPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("devicePath",    out var dp))  devicePath  = dp.GetString()!;
            if (root.TryGetProperty("musicBasePath", out var mp))  basisPad    = mp.GetString()!;
            if (root.TryGetProperty("audioDevice",   out var ad))  audioDevice = ad.GetString()!;
            if (root.TryGetProperty("outputDriver",  out var od))  outputDriver = od.GetString()!;
            if (root.TryGetProperty("volume",        out var vl))  volume      = vl.GetInt32();
            if (root.TryGetProperty("loopTracks",    out var lt))  loopTracks  = lt.GetBoolean();
            if (root.TryGetProperty("debugLogging",  out var dl))  debugLog    = dl.GetBoolean();

            // Key mappings
            if (root.TryGetProperty("keyMappings", out var km)) {
                var nieuweMap = new Dictionary<string, string>();
                foreach (var entry in km.EnumerateObject()) {
                    try {
                        var sortedKeys = string.Join(",", entry.Name.Split(',').Select(k => k.Trim()).Select(int.Parse).OrderBy(k => k));
                        nieuweMap[sortedKeys] = entry.Value.GetString()!;
                    } catch { /* skip invalid keys */ }
                }
                lock (mappen) { mappen = nieuweMap; }
                Log($"[CONFIG] Geladen: {mappen.Count} mappings, device={audioDevice}, vol={volume}");
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"[CONFIG] Fout bij laden: {ex.Message}");
        }
    }

    // Hot-reload: check config.json every 5 seconds
    static async Task HotReloadConfig()
    {
        DateTime lastWrite = File.Exists(ConfigPath)
            ? File.GetLastWriteTimeUtc(ConfigPath)
            : DateTime.MinValue;

        while (true) {
            await Task.Delay(5000);
            try {
                if (!File.Exists(ConfigPath)) continue;
                var current = File.GetLastWriteTimeUtc(ConfigPath);
                if (current != lastWrite) {
                    lastWrite = current;
                    Console.WriteLine("[CONFIG] Wijziging gedetecteerd — herladen...");
                    LaadConfig();
                }
            } catch { /* ignore transient IO errors */ }
        }
    }

    // ── Raw input reader ──────────────────────────────────────────────────────
    static void LeesRawInput()
    {
        int size = Marshal.SizeOf<InputEvent>();
        byte[] buffer = new byte[size];
        try {
            using var fs = new FileStream(devicePath, FileMode.Open,
                                          FileAccess.Read, FileShare.ReadWrite);
            while (true) {
                if (fs.Read(buffer, 0, size) < size) continue;

                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                InputEvent ev = Marshal.PtrToStructure<InputEvent>(handle.AddrOfPinnedObject());
                handle.Free();

                if (ev.Type == 0x01) { // EV_KEY
                    int code = (int)ev.Code;   // explicit cast so it matches dict keys
                    lock (ingedrukteToetsen) {
                        if      (ev.Value == 1) { ingedrukteToetsen.Add(code);    Log($"[KEY↓] code={code}"); }
                        else if (ev.Value == 0) { ingedrukteToetsen.Remove(code); Log($"[KEY↑] code={code}"); }
                    }
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[INPUT] Fout: {ex.Message}"); }
    }

    // ── Track picker ──────────────────────────────────────────────────────────
    static string GetWillekeurigeTrack(string toetsCode)
    {
        try {
            string mapNaam;
            lock (mappen) { if (!mappen.TryGetValue(toetsCode, out mapNaam!)) return ""; }

            string pad = Path.Combine(basisPad, mapNaam);
            if (!Directory.Exists(pad)) {
                Console.WriteLine($"[WARN] Map niet gevonden: {pad}");
                return "";
            }

            var bestanden = Directory.GetFiles(pad, "*.mp3")
                            .Concat(Directory.GetFiles(pad, "*.wav"))
                            .Concat(Directory.GetFiles(pad, "*.ogg"))
                            .ToArray();

            if (bestanden.Length == 0) {
                Console.WriteLine($"[WARN] Geen audiobestanden in {pad}");
                return "";
            }

            return bestanden[rng.Next(bestanden.Length)];
        }
        catch (Exception ex) { Console.WriteLine($"[TRACK] Fout: {ex.Message}"); return ""; }
    }

    // ── Player ────────────────────────────────────────────────────────────────
    static void SpeelTrack(string toetsCode)
    {
        string track = GetWillekeurigeTrack(toetsCode);
        if (string.IsNullOrEmpty(track)) {
            Console.WriteLine($"[PLAY] Geen track gevonden voor toets {toetsCode}");
            return;
        }

        Console.WriteLine($"[PLAY] {Path.GetFileName(track)}  (toets {toetsCode})");

        try {
            var args = $"-o {outputDriver} -a {audioDevice} -f {volume} --resync-limit -1";
            if (loopTracks) args += " --loop -1";
            args += $" \"{track}\"";

            Log($"[CMD]  mpg123 {args}");

            // Kill previous process for this key if it exists
            lock (actieveSpelers) {
                if (actieveSpelers.TryGetValue(toetsCode, out var oldP)) {
                    try { if (!oldP.HasExited) oldP.Kill(); } catch { }
                    actieveSpelers.Remove(toetsCode);
                }
            }

            var p = new Process();
            p.StartInfo.FileName               = "mpg123";
            p.StartInfo.Arguments              = args;
            p.StartInfo.UseShellExecute        = false;
            p.StartInfo.CreateNoWindow         = true;
            p.StartInfo.RedirectStandardError  = true;   // capture mpg123 errors

            p.ErrorDataReceived += (_, e) => {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                if (_metadataLabels.Any(label => e.Data.TrimStart().StartsWith(label))) return;
                Console.WriteLine($"[mpg123] {e.Data}");
            };

            p.Start();
            p.BeginErrorReadLine();

            lock (actieveSpelers) { actieveSpelers[toetsCode] = p; }
            p.WaitForExit();
            lock (actieveSpelers) { 
                if (actieveSpelers.TryGetValue(toetsCode, out var currentP) && currentP == p)
                    actieveSpelers.Remove(toetsCode); 
            }

            if (p.ExitCode != 0 && p.ExitCode != 1 && p.ExitCode != 137) // 137 is SIGKILL
                Console.WriteLine($"[WARN] mpg123 exitcode {p.ExitCode} voor {Path.GetFileName(track)}");
        }
        catch (Exception ex) { Console.WriteLine($"[PLAY] Fout: {ex.Message}"); }
    }

    static void Log(string msg) { if (debugLog) Console.WriteLine(msg); }
}