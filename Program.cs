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
        { 103, "2000s" }, { 108, "2010s" }, { 105, "Anime" },
        { 106, "EDM"   }, { 57,  "Game"  }, { 17,  "Pop"   }
    };

    static List<Process> actieveSpelers = new();
    static HashSet<int> actieveToetsenSessie = new(); 
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
            Console.WriteLine("FOUT: Check paden (bestaat /muziek/?) of rechten (sudo chmod 666 /dev/input/event2)");
            return;
        }

        _ = Task.Run(() => LeesRawInput());

        while (true)
        {
            List<int> huidigeLijst;
            lock (ingedrukteToetsen) { huidigeLijst = ingedrukteToetsen.ToList(); }

            foreach (int toets in huidigeLijst)
            {
                lock (actieveToetsenSessie) {
                    if (!actieveToetsenSessie.Contains(toets) && mappen.ContainsKey(toets)) {
                        actieveToetsenSessie.Add(toets);
                        _ = Task.Run(() => SpeelTrack(toets));
                    }
                }
            }

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
                        if (ev.Value == 1) ingedrukteToetsen.Add(ev.Code);
                        else if (ev.Value == 0) ingedrukteToetsen.Remove(ev.Code);
                    }
                }
            }
        } catch (Exception ex) { Console.WriteLine($"Input Fout: {ex.Message}"); }
    }

    static string GetWillekeurigeTrack(int toetsCode)
    {
        try {
            string mapNaam = mappen[toetsCode];
            string pad = Path.Combine(basisPad, mapNaam);
            if (!Directory.Exists(pad)) return "";
            
            var bestanden = Directory.GetFiles(pad, "*.mp3");
            return bestanden.Length > 0 ? bestanden[rng.Next(bestanden.Length)] : "";
        } catch { return ""; }
    }

static void SpeelTrack(int toetsCode)
{
    string track = GetWillekeurigeTrack(toetsCode);
    if (string.IsNullOrEmpty(track)) return;

    Console.WriteLine($"[PLAY] {Path.GetFileName(track)}");

    try {
        var p = new Process();
        p.StartInfo.FileName = "mpg123";
        
        // -a hw:0,0  -> Stuur direct naar de AIR 192 (Card 0)
        // --buffer 1024 -> Voorkomt haperingen
        // --resync-limit -1 -> Probeert corrupte MP3's toch te lezen
     // De 'plug:' toevoeging lost de "Unable to set up output format" error op.
p.StartInfo.Arguments = $"-a plug:hw:0,0 --buffer 1024 --resync-limit -1 \"{track}\"";
        
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.CreateNoWindow = true;
        p.Start();

        lock (actieveSpelers) { actieveSpelers.Add(p); }
        p.WaitForExit();
        lock (actieveSpelers) { actieveSpelers.Remove(p); }
    } 
    catch (Exception ex) { Console.WriteLine($"Fout: {ex.Message}"); }
}
}