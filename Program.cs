using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

class PaalMuziek
{
    // TIP: Gebruik /dev/input/by-id/ voor een vaste naam als event2 verandert
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
        public IntPtr TimeSec; 
        public IntPtr TimeUSec;
        public ushort Type; 
        public ushort Code; 
        public int Value;
    }

    static async Task Main()
    {
        Console.WriteLine("--- Muziekpaal: AIR 192 & mpg123 Edit ---");

        // Rechten check
        if (!File.Exists(devicePath)) {
            Console.WriteLine($"FOUT: Device {devicePath} niet gevonden!");
            return;
        }

        if (!Directory.Exists(basisPad)) {
            Console.WriteLine($"FOUT: Muziekmap niet gevonden op {basisPad}");
            return;
        }

        // Start de input thread
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
        int size = Marshal.SizeOf<InputEvent>();
        byte[] buffer = new byte[size];

        try {
            using FileStream fs = new FileStream(devicePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            while (true) {
                int bytesRead = fs.Read(buffer, 0, buffer.Length);
                if (bytesRead < size) continue; 

                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                InputEvent ev = Marshal.PtrToStructure<InputEvent>(handle.AddrOfPinnedObject());
                handle.Free();

                if (ev.Type == 0x01) { // EV_KEY
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
            Console.WriteLine($"[!] Map niet gevonden: {categoriePad}");
            return;
        }

        var fragmenten = Directory.GetFiles(categoriePad, "*.mp3");
        if (fragmenten.Length == 0) return;

        string track = fragmenten[rng.Next(fragmenten.Length)];
        StopAlleMuziek();

        Console.WriteLine($"[PLAY] {Path.GetFileName(track)}");
        
        try {
            var psi = new ProcessStartInfo {
                FileName = "mpg123",
                // we gebruiken 'default' of 'hw:1,0'. Test dit met 'aplay -l'
                // -q is quiet, --realtime voorkomt haperingen
               // Voeg -o alsa toe om JACK te negeren
              Arguments = $"-o alsa -a default -q \"{track}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process? p = Process.Start(psi);
            if (p != null) { lock(actieveSpelers) { actieveSpelers.Add(p); } }
        } catch (Exception ex) { 
            Console.WriteLine($"Audio Fout: {ex.Message}"); 
        }
    }

    static void StopAlleMuziek()
    {
        lock(actieveSpelers) {
            foreach (var p in actieveSpelers) {
                try { 
                    if (!p.HasExited) {
                        p.Kill(); 
                        p.WaitForExit(200); 
                    }
                    p.Dispose(); 
                } catch { }
            }
            actieveSpelers.Clear();
        }
        // Extra veiligheid: pkill mpg123
        try { 
            Process.Start(new ProcessStartInfo("pkill", "mpg123") { CreateNoWindow = true, UseShellExecute = false }); 
        } catch { }
    } 
}