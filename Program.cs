using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

class PaalMuziek
{
    // Voor Windows: checkt of een toets nú is ingedrukt
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    // Mappen per toets (Virtual Key Codes)
    static Dictionary<int, string> paalmappen = new()
    {
        { 38, "/muziek/paal1" }, // Up
        { 40, "/muziek/paal2" }, // Down
        { 37, "/muziek/paal3" }, // Left
        { 39, "/muziek/paal4" }, // Right
        { 32, "/muziek/paal5" }, // Space
        { 87, "/muziek/paal6" }, // W
    };

    // Speciale mappen voor combinaties
    static Dictionary<string, string> comboMappen = new()
    {
        { "37,39",    "/muziek/secret1" }, // Left + Right
        { "38,40",    "/muziek/secret2" }, // Up + Down
        { "32,38", "/muziek/secret3" }  // Space + Up 
    };

    static Dictionary<string, string[]> playlists = new();
    static Dictionary<string, int> trackIndex = new();
    static Process? huidigAfspelen = null;

    static async Task Main()
    {
        Console.WriteLine("--- Lokale Paal Controller (Hold & Combo Mode) ---");

        // 1. Laad normale mappen
        foreach (var entry in paalmappen) LaadMap(entry.Key.ToString(), entry.Value);

        // 2. Laad combo mappen
        foreach (var entry in comboMappen) LaadMap(entry.Key, entry.Value);

        Console.WriteLine("\nWachten op input (Hold to play)...");

        bool isPlaying = false;
        string huidigeActieveCombo = "";

        while (true)
        {
            var pressedKeys = GetPressedKeys();
            string comboId = string.Join(",", pressedKeys);

            // A. Er wordt iets ingedrukt
            if (pressedKeys.Count > 0)
            {
                // Als dit een nieuwe combo is, of we speelden nog niets
                if (comboId != huidigeActieveCombo)
                {
                    if (playlists.ContainsKey(comboId))
                    {
                        Console.WriteLine($"\n[ACTIE] Combo: {comboId}");
                        SpeelVolgendeTrack(comboId);
                        huidigeActieveCombo = comboId;
                        isPlaying = true;
                    }
                }
            }
            // B. Alles is losgelaten
            else if (isPlaying)
            {
                Console.WriteLine("[LOSGELATEN] Muziek stopt.");
                StopMuziek();
                isPlaying = false;
                huidigeActieveCombo = "";
            }

            await Task.Delay(50); // Scan snelheid
        }
    }

    static void LaadMap(string id, string pad)
    {
        if (Directory.Exists(pad))
        {
            var tracks = Directory.GetFiles(pad, "*.mp3");
            Array.Sort(tracks);
            playlists[id] = tracks;
            Console.WriteLine($"ID {id}: {tracks.Length} tracks geladen uit {pad}");
        }
    }

    static void SpeelVolgendeTrack(string id)
    {
        if (!playlists.ContainsKey(id) || playlists[id].Length == 0) return;

        trackIndex.TryAdd(id, 0);
        int index = trackIndex[id];
        string trackPad = playlists[id][index];
        
        // Update index voor de volgende keer dat deze combo wordt aangeraakt
        trackIndex[id] = (index + 1) % playlists[id].Length;

        SpeelAf(trackPad);
    }

    static void SpeelAf(string pad)
    {
        StopMuziek();
        try
        {
            huidigAfspelen = Process.Start(new ProcessStartInfo
            {
                FileName = "mpg123",
                Arguments = $"\"{pad}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            Console.WriteLine($"Speelt af: {Path.GetFileName(pad)}");
        }
        catch (Exception ex) { Console.WriteLine("Fout bij starten mpg123: " + ex.Message); }
    }

    static void StopMuziek()
    {
        if (huidigAfspelen != null && !huidigAfspelen.HasExited)
        {
            try { huidigAfspelen.Kill(); } catch { }
        }
    }

    static List<int> GetPressedKeys()
    {
        int[] keysToCheck = { 38, 40, 37, 39, 32, 87 }; // Up, Down, Left, Right, Space, W
        var pressed = new List<int>();
        foreach (var key in keysToCheck)
        {
            if (GetAsyncKeyState(key) < 0) pressed.Add(key);
        }
        pressed.Sort(); // Sorteer voor consistente combo IDs
        return pressed;
    }
}