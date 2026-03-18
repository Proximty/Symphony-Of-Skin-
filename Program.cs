using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

class PaalMuziek
{
    static Dictionary<ConsoleKey, string> paalmappen = new()
    {
        { ConsoleKey.UpArrow,    "/muziek/paal1" },
        { ConsoleKey.DownArrow,  "/muziek/paal2" },
        { ConsoleKey.LeftArrow,  "/muziek/paal3" },
        { ConsoleKey.RightArrow, "/muziek/paal4" },
        { ConsoleKey.Spacebar,   "/muziek/paal5" },
        { ConsoleKey.W,          "/muziek/paal6" },
    };

    static Dictionary<ConsoleKey, string[]> playlists = new();
    static Dictionary<ConsoleKey, int> trackIndex = new();
    static Process? huidigAfspelen = null;

    static void Main()
    {
        // Laad alle tracks per paal automatisch vanuit de map
        foreach (var (toets, map) in paalmappen)
        {
            if (Directory.Exists(map))
            {
                var tracks = Directory.GetFiles(map, "*.mp3");
                Array.Sort(tracks); // Vaste volgorde op bestandsnaam
                playlists[toets] = tracks;
                Console.WriteLine($"Paal {toets}: {tracks.Length} tracks geladen uit {map}");
            }
            else
            {
                Console.WriteLine($"WAARSCHUWING: Map niet gevonden: {map}");
            }
        }

        Console.WriteLine("\nWachten op input...");

        while (true)
        {
            var toets = Console.ReadKey(intercept: true).Key;

            if (playlists.TryGetValue(toets, out var tracks) && tracks.Length > 0)
            {
                trackIndex.TryAdd(toets, 0);
                int index = trackIndex[toets];
                string track = tracks[index];
                trackIndex[toets] = (index + 1) % tracks.Length;

                SpeelAf(track);
            }
        }
    }

    static void SpeelAf(string pad)
    {
        huidigAfspelen?.Kill();

        huidigAfspelen = Process.Start(new ProcessStartInfo
        {
            FileName = "mpg123",
            Arguments = $"\"{pad}\"",
            UseShellExecute = false
        });

        Console.WriteLine($"Speelt af: {pad}");
    }
}