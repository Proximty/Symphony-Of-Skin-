using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

class PaalMuziek
{
    // Check dit pad goed: staat er een extra '-' achter Symphony-Of-Skin?
    static string devicePath = "/dev/input/event2"; 
    static string basisPad = "/home/admin/Symphony-Of-Skin-/muziek"; 
    static Random rng = new Random();

    // Mapping van Makey Makey (Arduino) codes naar mapnamen
    static Dictionary<int, string> mappen = new() {
        { 103, "2000s" }, // Omhoog
        { 108, "2010s" }, // Omlaag
        { 105, "Anime" }, // Links
        { 106, "EDM"   }, // Rechts
        { 57,  "Game"  }, // Spatie
        { 17,  "Pop"   }  // W
    };

    static List<Process> actieveSpelers = new();
    static string huidigeActieveCombo = "";
    static HashSet<int> ingedrukteToetsen = new();
    static int stilteTeller = 0;

    [StructLayout(LayoutKind.Sequential)]
    struct InputEvent {
        public long TimeSec; public long TimeUSec;
        public ushort Type; public ushort Code; public int Value;
    }

    static async Task Main()
    {
        Console.WriteLine("--- Muziekpaal: AIR 192 Fix ---");
        Console.WriteLine($"Zoek naar muziek in: {basisPad}");

        // Controleer of de hoofdmap bestaat
        if (!Directory.Exists(basisPad)) {
            Console.WriteLine($"FOUT: De hoofdmap {basisPad} bestaat niet!");
            return;
        }

        // Toon welke submappen zijn gevonden voor controle
        foreach (var sub in mappen.Values) {
            string pad = Path.Combine(basisPad, sub);
            Console.WriteLine(Directory.Exists(pad) ? $"[OK] Map gevonden: {sub}" : $"[!!] Map NIET gevonden: {sub}");
        }

        if (!File.Exists(devicePath)) {
            Console.WriteLine($"FOUT: Makey Makey niet gevonden op {devicePath}");
            return;
        }

        _ = Task.Run(() => LeesRawInput());

        while (true)
        {
            List<int> lijst;
            lock (ingedrukteToetsen) {
                lijst = ingedrukteToetsen.OrderBy(x => x).ToList();
            }

            if (lijst.Count > 0)
            {
                string comboId = string.Join(",", lijst);
                if (comboId != huidigeActieveCombo)
                {
                    if (mappen.ContainsKey(lijst[0])) 
                    {
                        SpeelEénTrackUitMap(Path.Combine(basisPad, mappen[lijst[0]]));
                    }
                    huidigeActieveCombo = comboId;
                    stilteTeller = 0;
                }
            }
            else if (huidigeActieveCombo != "")
            {
                stilteTeller++;
                if (stilteTeller > 3) { 
                    StopAlleMuziek();
                    huidigeActieveCombo = "";
                }
            }
            await Task.Delay(100);
        }
    }

    static void LeesRawInput()
    {
        try {
            using FileStream fs = new FileStream(devicePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            int size = Marshal.SizeOf<InputEvent>();
            byte[] buffer = new byte[size];
            while (true) {
                fs.Read(buffer, 0, buffer.Length);
                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(buffer, 0, ptr, size);
                InputEvent ev = Marshal.PtrToStructure<InputEvent>(ptr);
                Marshal.FreeHGlobal(ptr);
                if (ev.Type == 1) { 
                    lock (ingedrukteToetsen) {
                        if (ev.Value == 1 || ev.Value == 2) ingedrukteToetsen.Add(ev.Code);
                        else if (ev.Value == 0) ingedrukteToetsen.Remove(ev.Code);
                    }
                }
            }
        } catch (Exception ex) { Console.WriteLine($"Input Fout: {ex.Message}"); }
    }

    static void SpeelEénTrackUitMap(string categoriePad)
    { 
        if (!Directory.Exists(categoriePad)) {
            Console.WriteLine($"[FOUT] Pad bestaat niet: {categoriePad}");
            return;
        }

        var fragmenten = Directory.GetFiles(categoriePad, "*.mp3");
        if (fragmenten.Length == 0) {
            Console.WriteLine($"[FOUT] Geen MP3's in map: {categoriePad}");
            return;
        }

        string track = fragmenten[rng.Next(fragmenten.Length)];
        StopAlleMuziek();

        Console.WriteLine($"[PLAY] {Path.GetFileName(track)}");
        
        try {
            var psi = new ProcessStartInfo {
                FileName = "ffplay",
                Arguments = $"-nodisp -autoexit -loglevel quiet \"{track}\"", 
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process p = Process.Start(psi);
            if (p != null) { lock(actieveSpelers) { actieveSpelers.Add(p); } }
        } catch (Exception ex) { Console.WriteLine($"Audio Fout: {ex.Message}"); }
    }

    static void StopAlleMuziek()
    {
        lock(actieveSpelers) {
            if (actieveSpelers.Count == 0) return;
            Console.WriteLine("[STOP]");
            foreach (var p in actieveSpelers) {
                try { if (!p.HasExited) p.Kill(); p.Dispose(); } catch { }
            }
            actieveSpelers.Clear();
        }
    }
}