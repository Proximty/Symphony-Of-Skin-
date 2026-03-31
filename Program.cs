using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class PaalMuziek
{
    static string basisPad = Path.Combine(Directory.GetCurrentDirectory(), "muziek");
    static Random rng = new Random(); // Toegevoegd voor het kiezen van 1 track

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
        Console.WriteLine("--- Muziekpaal: 1 Track Modus ---");
        
        if (!Directory.Exists(basisPad)) Directory.CreateDirectory(basisPad);

        while (true)
        {
            while (Console.KeyAvailable) 
            {
                var key = Console.ReadKey(true).Key;
                if (mappen.ContainsKey(key)) ingedrukteToetsen.Add((int)key);
            }

            var lijst = ingedrukteToetsen.OrderBy(x => x).ToList();
            string comboId = string.Join(",", lijst);

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
            else if (huidigeActieveCombo != "")
            {
                StopAlleMuziek();
                huidigeActieveCombo = "";
            }

            await Task.Delay(150); 
            ingedrukteToetsen.Clear();
        }
    }

    static void SpeelEénTrackUitMap(string categoriePad)
    {
        if (!Directory.Exists(categoriePad)) return;

        var fragmenten = Directory.GetFiles(categoriePad, "*.mp3");
        if (fragmenten.Length == 0) return;

        // KIES HIER 1 WILLEKEURIGE TRACK
        string gekozenTrack = fragmenten[rng.Next(fragmenten.Length)];

        StopAlleMuziek();

        Console.WriteLine($"[PLAY] {Path.GetFileName(gekozenTrack)}");

        try {
            var psi = new ProcessStartInfo
            {
                FileName = "mpg123",
                Arguments = $"-q \"{gekozenTrack}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var p = Process.Start(psi);
            if (p != null) actieveSpelers.Add(p);
        } catch (Exception ex) {
            Console.WriteLine($"Fout: {ex.Message}");
        }
    }

    static void StopAlleMuziek()
    {
        if (actieveSpelers.Count == 0) return;
        foreach (var p in actieveSpelers)
        {
            try { if (!p.HasExited) p.Kill(true); p.Dispose(); } catch { }
        }
        actieveSpelers.Clear();
        try { Process.Start("killall", "mpg123"); } catch { }
    }
}