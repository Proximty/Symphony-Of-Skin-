using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class PaalMuziek
{
    // Gebruik Path.Combine voor Linux/Windows compatibiliteit
    static string basisPad = Path.Combine(Directory.GetCurrentDirectory(), "muziek");

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
        Console.WriteLine("--- Interactieve Muziekpaal Gestart (Linux Mode) ---");
        Console.WriteLine($"Basispad: {basisPad}");

        if (!Directory.Exists(basisPad))
        {
            Console.WriteLine("WAARSCHUWING: De muziekmap is niet gevonden!");
            Directory.CreateDirectory(basisPad);
        }

        while (true)
        {
            while (Console.KeyAvailable) 
            {
                var key = Console.ReadKey(true).Key;
                if (mappen.ContainsKey(key)) 
                {
                    ingedrukteToetsen.Add((int)key);
                }
            }

            var lijst = ingedrukteToetsen.OrderBy(x => x).ToList();
            string comboId = string.Join(",", lijst);

            if (lijst.Count > 0)
            {
                if (comboId != huidigeActieveCombo)
                {
                    string? categorieMap = null;

                    if (comboMappen.ContainsKey(comboId)) 
                        categorieMap = comboMappen[comboId];
                    else if (lijst.Count == 1) 
                        categorieMap = mappen[(ConsoleKey)lijst[0]];

                    if (categorieMap != null) 
                    {
                        string volledigPad = Path.Combine(basisPad, categorieMap);
                        // AANGEPAST: We spelen nu direct vanuit de categorieMap
                        SpeelLiedjesUitMap(volledigPad);
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

    static void SpeelLiedjesUitMap(string categoriePad)
    {
        if (!Directory.Exists(categoriePad))
        {
            Console.WriteLine($"Map niet gevonden: {categoriePad}");
            return;
        }

        // Pak alle mp3 bestanden direct uit de map (zoals te zien in je screenshot)
        var fragmenten = Directory.GetFiles(categoriePad, "*.mp3");

        if (fragmenten.Length == 0)
        {
            Console.WriteLine($"Geen MP3's gevonden in: {categoriePad}");
            return;
        }

        StopAlleMuziek();

        Console.WriteLine($"[START] Afspelen van {fragmenten.Length} tracks uit {Path.GetFileName(categoriePad)}");

        foreach (var track in fragmenten)
        {
            try {
                var psi = new ProcessStartInfo
                {
                    FileName = "mpg123",
                    Arguments = $"-q \"{track}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                var p = Process.Start(psi);
                if (p != null) actieveSpelers.Add(p);
            } catch (Exception ex) {
                Console.WriteLine($"Fout bij starten {Path.GetFileName(track)}: {ex.Message}");
            }
        }
    }

    static void StopAlleMuziek()
    {
        if (actieveSpelers.Count == 0) return;

        Console.WriteLine("[STOP] Alle muziek");
        foreach (var p in actieveSpelers)
        {
            try { 
                if (!p.HasExited)
                {
                    p.Kill(true);
                }
                p.Dispose();
            } catch { }
        }
        actieveSpelers.Clear();
        
        try { Process.Start("killall", "mpg123"); } catch { }
    }
}