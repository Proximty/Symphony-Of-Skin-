using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class PaalMuziek
{
    // Mappen per toets (gebruik ConsoleKey voor Makey Makey op de Pi)
    static Dictionary<ConsoleKey, string> mappen = new() {
        { ConsoleKey.UpArrow,    "/muziek/paal1" },
        { ConsoleKey.DownArrow,  "/muziek/paal2" },
        { ConsoleKey.LeftArrow,  "/muziek/paal3" },
        { ConsoleKey.RightArrow, "/muziek/paal4" },
        { ConsoleKey.Spacebar,   "/muziek/paal5" },
        { ConsoleKey.W,          "/muziek/paal6" }
    };

    // Jouw combo structuur (getallen zijn de (int)ConsoleKey waarden)
    static Dictionary<string, string> comboMappen = new() {
        { "37,39", "/muziek/secret1" }, // Left + Right
        { "38,40", "/muziek/secret2" }, // Up + Down
        { "32,38", "/muziek/secret3" }  // Space + Up 
    };

    static Process? speler = null;
    static string huidigeActieveCombo = "";
    static HashSet<int> ingedrukteToetsen = new();

    static async Task Main()
    {
        Console.WriteLine("--- Paal Gestart (Makey Makey + Combo Mode) ---");

        while (true)
        {
            // 1. Verzamel alle toetsen die op dit moment "vuren"
            while (Console.KeyAvailable) 
            {
                int code = (int)Console.ReadKey(true).Key;
                if (mappen.ContainsKey((ConsoleKey)code)) ingedrukteToetsen.Add(code);
            }

            // 2. Maak de combo-ID (bijv "37,39")
            var lijst = ingedrukteToetsen.OrderBy(x => x).ToList();
            string comboId = string.Join(",", lijst);

            // 3. Check wat we moeten doen
            if (lijst.Count > 0)
            {
                if (comboId != huidigeActieveCombo)
                {
                    string? pad = null;

                    if (comboMappen.ContainsKey(comboId)) pad = comboMappen[comboId];
                    else if (lijst.Count == 1) pad = mappen[(ConsoleKey)lijst[0]];

                    if (pad != null) SpeelMap(pad);
                    huidigeActieveCombo = comboId;
                }
            }
            else if (huidigeActieveCombo != "")
            {
                StopMuziek();
                huidigeActieveCombo = "";
            }

            // 4. Reset voor de volgende scan (Makey Makey herhaalt de toetsen zelf)
            await Task.Delay(100); 
            ingedrukteToetsen.Clear();
        }
    }

    static void SpeelMap(string pad)
    {
        if (!Directory.Exists(pad)) return;
        var bestanden = Directory.GetFiles(pad, "*.mp3");
        if (bestanden.Length == 0) return;

        string track = bestanden[new Random().Next(bestanden.Length)];
        StopMuziek();
        try {
            speler = Process.Start("mpg123", $"-q \"{track}\"");
            Console.WriteLine($"Speelt nu: {Path.GetFileName(track)}");
        } catch { }
    }

    static void StopMuziek()
    {
        try { speler?.Kill(); } catch { }
    }
}