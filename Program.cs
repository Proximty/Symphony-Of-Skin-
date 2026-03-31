using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

class PaalMuziek
{
    static string devicePath = "/dev/input/event2"; 
    static string basisPad = "/home/admin/Symphony-Of-Skin-/muziek"; 
    static Random rng = new Random();

    static Dictionary<int, string> mappen = new() {
        { 103, "2000s" },
        { 108, "2010s" },
        { 105, "Anime" },
        { 106, "EDM"   },
        { 57,  "Game"  },
        { 17,  "Pop"   }
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
        Console.WriteLine("--- Muziekpaal: AIR 192 & MPV Fix ---");

        if (!Directory.Exists(basisPad)) {
            Console.WriteLine($"FOUT: De hoofdmap {basisPad} bestaat niet!");
            return;
        }

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
                // FIX CA2022: Controleren of de volledige structuur is gelezen
                int bytesRead = fs.Read(buffer, 0, buffer.Length);
                if (bytesRead < size) continue; 

                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.Copy(buffer, 0, ptr, size);
                // FIX CS8600: Null-check toevoegen of casten
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
        if (!Directory.Exists(categoriePad)) return;

        var fragmenten = Directory.GetFiles(categoriePad, "*.mp3");
        if (fragmenten.Length == 0) return;

        string track = fragmenten[rng.Next(fragmenten.Length)];
        StopAlleMuziek();

        Console.WriteLine($"[PLAY] {Path.GetFileName(track)}");
        
        try {
            var psi = new ProcessStartInfo {
                // GEWIJZIGD NAAR MPV VOOR STABIELE USB AUDIO
                FileName = "mpv",
                Arguments = $"--no-video --ao=alsa \"{track}\"", 
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process? p = Process.Start(psi);
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
        // KILLALL MPV VOOR DE ZEKERHEID
        try { Process.Start("killall", "mpv"); } catch { }
    } 
}