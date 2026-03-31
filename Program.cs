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
    static HashSet<int> actieveToetsenSessie = new(); // Om te voorkomen dat 1 toets 100 keer start
    static HashSet<int> ingedrukteToetsen = new();

    [StructLayout(LayoutKind.Sequential)]
    struct InputEvent {
        public IntPtr TimeSec; public IntPtr TimeUSec;
        public ushort Type; public ushort Code; public int Value;
    }

    static async Task Main()
    {
        Console.WriteLine("--- Muziekpaal: Multi-Track Mode (Layering) ---");

        if (!File.Exists(devicePath) || !Directory.Exists(basisPad)) {
            Console.WriteLine("FOUT: Check paden of rechten!");
            return;
        }

        _ = Task.Run(() => LeesRawInput());

        while (true)
        {
            List<int> huidigeLijst;
            lock (ingedrukteToetsen) {
                huidigeLijst = ingedrukteToetsen.ToList();
            }

            foreach (int toets in huidigeLijst)
            {
                // Start alleen als deze toets nog niet in de huidige sessie speelt
                lock (actieveToetsenSessie) {
                    if (!actieveToetsenSessie.Contains(toets) && mappen.ContainsKey(toets)) {
                        actieveToetsenSessie.Add(toets);
                        _ = Task.Run(() => SpeelTrack(toets));
                    }
                }
            }

            // Als toetsen worden losgelaten, verwijderen we ze uit de sessie zodat ze opnieuw gestart kunnen worden
            lock (actieveToetsenSessie) {
                actieveToetsenSessie.RemoveWhere(t => !huidigeLijst.Contains(t));
            }

            await Task.Delay(50);
        }
    }

    static void LeesRawInput()
    {
        int size = Marshal.SizeOf<InputEvent>();
        byte[] buffer = new byte[size];
        try {
            using FileStream fs = new FileStream(devicePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            while (true) {
                if (fs.Read(buffer, 0, size) < size) continue;
                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                InputEvent ev = Marshal.PtrToStructure<InputEvent>(handle.AddrOfPinnedObject());
                handle.Free();

                if (ev.Type == 0x01) { // EV_KEY
                    lock (ingedrukteToetsen) {
                        if (ev.Value == 1) ingedrukteToetsen.Add(ev.Code); // Alleen bij eerste indruk (geen repeat)
                        else if (ev.Value == 0) ingedrukteToetsen.Remove(ev.Code);
                    }
                }
            }
        } catch (Exception ex) { Console.WriteLine($"Input Fout: {ex.Message}"); }
    }

static void SpeelTrack(int toetsCode)
{
    string categoriePad = Path.Combine(basisPad, mappen[toetsCode]);
    if (!Directory.Exists(categoriePad)) return;

    var fragmenten = Directory.GetFiles(categoriePad, "*.mp3");
    if (fragmenten.Length == 0) return;

    string track = fragmenten[rng.Next(fragmenten.Length)];
    Console.WriteLine($"[LAYER START] {mappen[toetsCode]}: {Path.GetFileName(track)}");

    try {
        var psi = new ProcessStartInfo {
            FileName = "/bin/sh",
            // Uitleg: 
            // mpg123 -s stuurt RAW audio naar de uitgang
            // aplay pikt dit op en stuurt het naar hw:1,0 (of hw:2,0 afhankelijk van je kaart)
            Arguments = $"-c \"mpg123 -s \\\"{track}\\\" | aplay -D hw:1,0 -f cd\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? p = Process.Start(psi);
        if (p != null) {
            lock (actieveSpelers) { actieveSpelers.Add(p); }
            p.WaitForExit(); 
            lock (actieveSpelers) { actieveSpelers.Remove(p); }
            p.Dispose();
            Console.WriteLine($"[LAYER END] Klaar.");
        }
    }
    catch (Exception ex) { Console.WriteLine($"Audio Fout: {ex.Message}"); }
}
}