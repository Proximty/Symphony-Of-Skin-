using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class PaalMuziek
{
    // Gebruik Path.Combine voor Linux/Windows compatibiliteit
    static string basisPad = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "muziek");

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

        // Controleer of de map bestaat
        if (!Directory.Exists(basisPad))
        {
            Console.WriteLine("WAARSCHUWING: De muziekmap is niet gevonden!");
            Directory.CreateDirectory(basisPad);
        }

        while (true)
        {
            // 1. Verzamel input
            while (Console.KeyAvailable) 
            {
                // true zorgt ervoor dat de letter niet in de console verschijnt
                var key = Console.ReadKey(true).Key;
                if (mappen.ContainsKey(key)) 
                {
                    ingedrukteToetsen.Add((int)key);
                }
            }

            var lijst = ingedrukteToetsen.OrderBy(x => x).ToList();
            string comboId = string.Join(",", lijst);

            // 2. Logica voor afspelen
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
                        SpeelLiedjeMetLagen(volledigPad);
                    }
                    huidigeActieveCombo = comboId;
                }
            }
            else if (huidigeActieveCombo != "")
            {
                StopAlleMuziek();
                huidigeActieveCombo = "";
            }

            // 3. De Makey Makey herhaalt toetsaanslagen. 
            // We wachten even en legen de lijst om te kijken of de toets nog steeds 'vast' zit.
            await Task.Delay(150); 
            ingedrukteToetsen.Clear();
        }
    }

    static void SpeelLiedjeMetLagen(string categoriePad)
    {
        if (!Directory.Exists(categoriePad)) return;

        var liedjeMappen = Directory.GetDirectories(categoriePad);
        if (liedjeMappen.Length == 0) return;

        string gekozenLiedjeMap = liedjeMappen[new Random().Next(liedjeMappen.Length)];
        
        StopAlleMuziek();

        // Pak alle mp3 bestanden
        var fragmenten = Directory.GetFiles(gekozenLiedjeMap, "*.mp3");

        Console.WriteLine($"[START] {Path.GetFileName(gekozenLiedjeMap)}");

        foreach (var track in fragmenten)
        {
            try {
                // Op Raspberry Pi is 'mpg123' goed, maar 'ffplay' is vaak beter voor sync.
                // We gebruiken bash om het proces aan te roepen voor betere stabiliteit.
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
                Console.WriteLine($"Fout: {ex.Message}");
            }
        }
    }

    static void StopAlleMuziek()
    {
        if (actieveSpelers.Count == 0) return;

        Console.WriteLine("[STOP] Alle lagen");
        foreach (var p in actieveSpelers)
        {
            try { 
                if (!p.HasExited)
                {
                    p.Kill(true); // true zorgt dat ook child-processes doodgaan
                }
                p.Dispose();
            } catch { }
        }
        actieveSpelers.Clear();
        
        // Extra veiligheid voor Linux: stop alle hangende mpg123 processen
        try { Process.Start("killall", "mpg123"); } catch { }
    }
}