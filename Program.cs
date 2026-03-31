using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

class PaalMuziek
{
    // AANGEPAST: event2 is jouw Arduino/Makey Makey
    static string devicePath = "/dev/input/event2"; 
    static string basisPad = Path.Combine(Directory.GetCurrentDirectory(), "muziek");
    static Random rng = new Random();

    static Dictionary<int, string> mappen = new() {
        { 103, "2000s" }, // Up Arrow
        { 108, "2010s" }, // Down Arrow
        { 105, "Anime" }, // Left Arrow
        { 106, "EDM" },   // Right Arrow
        { 57,  "Game" },  // Spacebar
        { 17,  "Pop" }    // W-key
    };

    static List<Process> actieveSpelers = new();
    static string huidigeActieveCombo = "";
    static HashSet<int> ingedrukteToetsen = new();
    static int stilteTeller = 0;

    [StructLayout(LayoutKind.Sequential)]
    struct InputEvent {
        public long TimeSec;
        public long TimeUSec;
        public ushort Type;
        public ushort Code;
        public int Value;
    }

    static async Task Main()
    {
        Console.WriteLine("--- Muziekpaal: AIR 192 & Makey Makey Mode ---");
        
        // Check of we bij de Makey Makey kunnen
        if (!File.Exists(devicePath)) {
            Console.WriteLine($"FOUT: {devicePath} niet gevonden!");
            Console.WriteLine("Voer dit eerst uit: sudo chmod 666 /dev/input/event2");
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
                        string pad = Path.Combine(basisPad, mappen[lijst[0]]);
                        SpeelEénTrackUitMap(pad);
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

                if (ev.Type == 1) { // EV_KEY
                    lock (ingedrukteToetsen) {
                        if (ev.Value == 1 || ev.Value == 2) ingedrukteToetsen.Add(ev.Code);
                        else if (ev.Value == 0) ingedrukteToetsen.Remove(ev.Code);
                    }
                }
            }
        } catch (Exception ex) {
            Console.WriteLine($"Input Fout: {ex.Message}");
        }
    }

    static void SpeelEénTrackUitMap(string categoriePad)
    { 
        if (!Directory.Exists(categoriePad)) {
            Console.WriteLine($"Map niet gevonden: {categoriePad}");
            return;
        }

        var fragmenten = Directory.GetFiles(categoriePad, "*.mp3");
        if (fragmenten.Length == 0) return;

        string track = fragmenten[rng.Next(fragmenten.Length)];
        StopAlleMuziek();

        Console.WriteLine($"[PLAY] {Path.GetFileName(track)}");
        
        try {
            var psi = new ProcessStartInfo {
                FileName = "ffplay",
                // De meest stabiele manier voor de AIR 192 (geen sudo nodig!)
                Arguments = $"-nodisp -autoexit -loglevel quiet \"{track}\"", 
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            Process p = Process.Start(psi);
            if (p != null) {
                lock(actieveSpelers) {
                    actieveSpelers.Add(p);
                }
            }
        } catch (Exception ex) { 
            Console.WriteLine($"Audio Fout: {ex.Message}"); 
        }
    }

    static void StopAlleMuziek()
    {
        lock(actieveSpelers) {
            if (actieveSpelers.Count == 0) return;
            Console.WriteLine("[STOP]");
            foreach (var p in actieveSpelers) {
                try { 
                    if (!p.HasExited) p.Kill(); 
                    p.Dispose(); 
                } catch { }
            }
            actieveSpelers.Clear();
        }
    }
}