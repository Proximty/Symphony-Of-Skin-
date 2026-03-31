using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class PaalMuziek
{
    static string basisPad = Path.Combine(Directory.GetCurrentDirectory(), "muziek");
    static Random rng = new Random();
    
    // Deze teller voorkomt dat de muziek stopt bij kleine haperingen van de Makey Makey
    static int stilteTeller = 0;
    const int MAX_STILTE = 3; // 3 x 150ms = 450ms buffer

    static Dictionary<ConsoleKey, string> mappen = new() {
        { ConsoleKey.UpArrow,    "2000s" },
        { ConsoleKey.DownArrow,  "2010s" },
        { ConsoleKey.LeftArrow,  "Anime" },
        { ConsoleKey.RightArrow, "EDM" },
        { ConsoleKey.Spacebar,   "Game" },
        { ConsoleKey.W,          "Pop" }
    };

    static Dictionary<string, string> comboMappen = new() {
        { "37,39", "secret1" }, 
        { "38,40", "Secret" }, 
        { "32,38", "secret3" }  
    };

    static List<Process> actieveSpelers = new();
    static string huidigeActieveCombo = "";
    static HashSet<int> ingedrukteToetsen = new();

    static async Task Main()
    {
        Console.WriteLine("--- Muziekpaal: Gecorrigeerde Modus ---");
        Console.WriteLine($"Pad: {basisPad}");
        
        if (!Directory.Exists(basisPad)) Directory.CreateDirectory(basisPad);

        while (true)
        {
            // 1. Input opvangen
            while (Console.KeyAvailable) 
            {
                var key = Console.ReadKey(true).Key;
                if (mappen.ContainsKey(key)) 
                {
                    ingedrukteToetsen.Add((int)key);
                    stilteTeller = 0; // Reset teller bij input
                }
            }

            var lijst = ingedrukteToetsen.OrderBy(x => x).ToList();
            string comboId = string.Join(",", lijst);

            // 2. Afspeel logica
            if (lijst.Count > 0)
            {
                if (comboId != huidigeActieveCombo)
                {
                    string? categorieMap = null;
                    if (comboMappen.ContainsKey(comboId)) categorieMap = comboMappen[comboId];
                    else if (lijst.Count == 1) categorieMap = mappen[(ConsoleKey)lijst[0]];

                    if (categorieMap != null) 
                    {
                        SpeelEénTrackUitMap(Path.Combine(basisPad, categorieMap));
                    }
                    huidigeActieveCombo = comboId;
                }
            }
            else 
            {
                // Alleen stoppen als er een tijdje GEEN input is geweest (Sustain)
                stilteTeller++;
                if (stilteTeller >= MAX_STILTE && huidigeActieveCombo != "")
                {
                    StopAlleMuziek();
                    huidigeActieveCombo = "";
                }
            }

            // 3. Wachten en lijst legen voor de volgende check
            await Task.Delay(150); 
            ingedrukteToetsen.Clear();
        }
    }

    static void SpeelEénTrackUitMap(string categoriePad)
    {
        if (!Directory.Exists(categoriePad)) return;

        var fragmenten = Directory.GetFiles(categoriePad, "*.mp3");
        if (fragmenten.Length == 0) return;

        string gekozenTrack = fragmenten[rng.Next(fragmenten.Length)];

        StopAlleMuziek();
        Console.WriteLine($"[PLAY] {Path.GetFileName(gekozenTrack)}");

        try {
            var psi = new ProcessStartInfo
            {
                FileName = "mpg123",
                Arguments = $"-q \"{gekozenTrack}\"", 
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var p = Process.Start(psi);
            if (p != null) actieveSpelers.Add(p);
        } catch (Exception ex) {
            Console.WriteLine($"Fout bij starten: {ex.Message}");
        }
    }

    static void StopAlleMuziek()
    {
        if (actieveSpelers.Count == 0) return;
        
        Console.WriteLine("[STOP]");
        foreach (var p in actieveSpelers)
        {
            try { 
                if (!p.HasExited) p.Kill(true); 
                p.Dispose(); 
            } catch { }
        }
        actieveSpelers.Clear();

        // Killall weggelaten om "no process found" meldingen te voorkomen
    }
}