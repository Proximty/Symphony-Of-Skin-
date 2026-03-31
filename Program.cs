using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

class PaalMuziek
{
    // Verifieer met 'ls /dev/input/by-id/' welk event nummer je Makey Makey ECHT heeft
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

    // Verbeterde struct voor Linux Input Events (64-bit compatibel)
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
        Console.WriteLine("--- Muziekpaal: AIR 192 Fix ---");

        // Check rechten op de input device
        try {
            using (File.OpenRead(devicePath)) { }
        } catch (UnauthorizedAccessException) {
            Console.WriteLine($"FOUT: Geen rechten op {devicePath}. Gebruik: sudo chmod 666 {devicePath}");
            return;
        } catch (Exception ex) {
            Console.WriteLine($"FOUT: {ex.Message}");
            return;
        }

        if (!Directory.Exists(basisPad)) {
            Console.WriteLine($"FOUT: Pad niet gevonden: {basisPad}");
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
                    // Check of de eerste toets in onze map staat
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
                // Iets langere delay voordat we stoppen geeft een rustiger effect
                if (stilteTeller > 2) { 
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
        if (!Directory.Exists(categoriePad)) return;

        var fragmenten = Directory.GetFiles(categoriePad, "*.mp3");
        if (fragmenten.Length == 0) return;

        string track = fragmenten[rng.Next(fragmenten.Length)];
        StopAlleMuziek();

        Console.WriteLine($"[PLAY] {Path.GetFileName(track)}");
        
        try {
            var psi = new ProcessStartInfo {
                FileName = "mpv",
                // Tip: Gebruik 'softvol' om gekraak te voorkomen
                Arguments = $"--no-video --audio-device=alsa/hw:CARD=8,DEV=0 --volume=80 \"{track}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false
            };
            Process? p = Process.Start(psi);
            if (p != null) { lock(actieveSpelers) { actieveSpelers.Add(p); } }
        } catch (Exception ex) { Console.WriteLine($"Audio Fout: {ex.Message}"); }
    }

    static void StopAlleMuziek()
    {
        lock(actieveSpelers) {
            foreach (var p in actieveSpelers) {
                try { 
                    if (!p.HasExited) {
                        p.Kill(); 
                        p.WaitForExit(500); 
                    }
                    p.Dispose(); 
                } catch { }
            }
            actieveSpelers.Clear();
        }
        // Forceer kill om 'Device Busy' errors voor de volgende track te voorkomen
        try { 
            Process.Start(new ProcessStartInfo("pkill", "mpv") { CreateNoWindow = true, UseShellExecute = false }); 
        } catch { }
    } 
}