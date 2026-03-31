using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class PaalMuziek
{
    // Het basispad naar je muziekmap
    static string basisPad = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "muziek");

    // Koppeling tussen knoppen en hoofdcategorieën
    static Dictionary<ConsoleKey, string> mappen = new() {
        { ConsoleKey.UpArrow,    "2000s" },
        { ConsoleKey.DownArrow,  "2010s" },
        { ConsoleKey.LeftArrow,  "Anime" },
        { ConsoleKey.RightArrow, "EDM" },
        { ConsoleKey.Spacebar,   "Game" },
        { ConsoleKey.W,          "Pop" }
    };

    // Speciale combinaties
    static Dictionary<string, string> comboMappen = new() {
        { "37,39", "secret1" }, 
        { "38,40", "Secret" }, 
        { "32,38", "secret3" }  
    };

    // Lijst om alle 5 de actieve geluidsprocessen bij te houden
    static List<Process> actieveSpelers = new();
    static string huidigeActieveCombo = "";
    static HashSet<int> ingedrukteToetsen = new();

    static async Task Main()
    {
        Console.WriteLine("--- Interactieve Muziekpaal Gestart ---");
        Console.WriteLine($"Basispad: {basisPad}");

        while (true)
        {
            // 1. Check welke toetsen worden ingedrukt (Makey Makey simuleert toetsenbord)
            while (Console.KeyAvailable) 
            {
                int code = (int)Console.ReadKey(true).Key;
                if (mappen.ContainsKey((ConsoleKey)code)) ingedrukteToetsen.Add(code);
            }

            var lijst = ingedrukteToetsen.OrderBy(x => x).ToList();
            string comboId = string.Join(",", lijst);

            if (lijst.Count > 0)
            {
                if (comboId != huidigeActieveCombo)
                {
                    string? categorieMap = null;

                    // Check of het een combo is of een enkele toets
                    if (comboMappen.ContainsKey(comboId)) categorieMap = comboMappen[comboId];
                    else if (lijst.Count == 1) categorieMap = mappen[(ConsoleKey)lijst[0]];

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
                // Geen toetsen meer ingedrukt? Stop alle lagen
                StopAlleMuziek();
                huidigeActieveCombo = "";
            }

            // Korte pauze voor de CPU en om de Makey Makey tijd te geven
            await Task.Delay(100); 
            ingedrukteToetsen.Clear();
        }
    }

    static void SpeelLiedjeMetLagen(string categoriePad)
    {
        if (!Directory.Exists(categoriePad))
        {
            Console.WriteLine($"Categorie niet gevonden: {categoriePad}");
            return;
        }

        // 1. Zoek alle mappen (bijv. "Alan Walker - Faded") in de categorie
        var liedjeMappen = Directory.GetDirectories(categoriePad);
        
        if (liedjeMappen.Length == 0)
        {
            Console.WriteLine($"Geen liedje-mappen gevonden in: {categoriePad}");
            return;
        }

        // 2. Kies een willekeurige map (een liedje)
        string gekozenLiedjeMap = liedjeMappen[new Random().Next(liedjeMappen.Length)];
        
        // 3. Stop eerst wat er nu speelt
        StopAlleMuziek();

        // 4. Zoek de 5 MP3's in deze specifieke map
        var fragmenten = Directory.GetFiles(gekozenLiedjeMap, "*.mp3");

        Console.WriteLine($"Speelt nu: {Path.GetFileName(gekozenLiedjeMap)} ({fragmenten.Length} lagen)");

        foreach (var track in fragmenten)
        {
            try {
                // Start mpg123 voor elk bestand. Ze draaien nu parallel.
                // -q is quiet mode, zodat je console schoon blijft.
                var p = Process.Start("mpg123", $"-q \"{track}\"");
                if (p != null) actieveSpelers.Add(p);
            } catch (Exception ex) {
                Console.WriteLine($"Fout bij starten fragment {track}: {ex.Message}");
            }
        }
    }

    static void StopAlleMuziek()
    {
        foreach (var p in actieveSpelers)
        {
            try { 
                if (!p.HasExited)
                {
                    p.Kill(); 
                    p.WaitForExit();
                }
            } catch { }
        }
        actieveSpelers.Clear();
    }
}