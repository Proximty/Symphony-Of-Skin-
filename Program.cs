using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using Newtonsoft.Json;
using System.Net.Http; // Nodig voor de API-versie

class Program
{
    private static string clientId = "f135a36b32054ef49aac4c7e27554f85";
    private static string clientSecret = "fd4da5ce69b34becb9dd433c83e8e7dd";
    private static SpotifyClient? _spotify;

    private static readonly Dictionary<ConsoleKey, string> GenreMap = new()
    {
        { ConsoleKey.Spacebar, "Jazz" },
        { ConsoleKey.UpArrow,    "Rock" },
        { ConsoleKey.DownArrow,  "Pop" },
        { ConsoleKey.LeftArrow,  "HipHop" },
        { ConsoleKey.RightArrow, "Classical" },
        { ConsoleKey.Enter,      "Electronic" }
    };

   static async Task Main()
{
    Console.WriteLine("Systeem opgestart. Klaar voor input!");

    // Zorg dat de rest van je code hieronder staat, 
    // maar zonder enige verwijzing naar CSCore!

    while (true)
    {
        // Je input logic
        await Task.Delay(100);
    }
}

    static async Task SendGenreToApi(string genre)
    {
        // Hier komt jouw API logic
        Console.WriteLine($"Genre '{genre}' gedetecteerd. Hier stuur je de API call.");
    }
}