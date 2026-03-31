using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

class PaalMuziek
{
    // PAD NAAR DE MAKEY MAKEY (event6 is je Arduino Leonardo Keyboard)
    static string devicePath = "/dev/input/event6"; 
    static string basisPad = Path.Combine(Directory.GetCurrentDirectory(), "muziek");
    static Random rng = new Random();

    // RUWE LINUX CODES
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
        Console.WriteLine("--- Muziekpaal: FFmpeg Mode (event6) ---");
        
        if (!File.Exists(devicePath)) {
            Console.WriteLine($"FOUT: {devicePath} niet gevonden! Gebruik: sudo dotnet run");
            return;
        }

        // Start input reader in de achtergrond
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
                if (stilteTeller > 3) { // Ongeveer 300ms buffer
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
    if (!Directory.Exists(categoriePad)) return;
    var fragmenten = Directory.GetFiles(categoriePad, "*.mp3");
    if (fragmenten.Length == 0) return;

    string track = fragmenten[rng.Next(fragmenten.Length)];
    StopAlleMuziek();

    // We loggen nu specifiek dat we kaart 2 gebruiken
    Console.WriteLine($"[PLAY] Direct naar AIR 192 (hw:2,0): {Path.GetFileName(track)}");
    
    try {
        var psi = new ProcessStartInfo {
            FileName = "mpg123",
            // -a hw:2,0 vertelt mpg123 om card 2, device 0 te gebruiken
            // -q zorgt dat je geen tekst-spam krijgt
            Arguments = $"-a hw:2,0 -q \"{track}\"", 
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var p = Process.Start(psi);
        if (p != null) actieveSpelers.Add(p);
    } catch (Exception ex) { 
        Console.WriteLine($"Fout bij afspelen: {ex.Message}"); 
    }
}
    static void StopAlleMuziek()
    {
        if (actieveSpelers.Count == 0) return;
        Console.WriteLine("[STOP]");
        foreach (var p in actieveSpelers) {
            try { 
                if (!p.HasExited) p.Kill(); 
                p.Dispose(); 
            } catch { }
        }
        actieveSpelers.Clear();
        
        // Extra cleanup voor hangende ffplay processen
        try { Process.Start("killall", "ffplay"); } catch { }
    }
}