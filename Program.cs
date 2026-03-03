using System;
using System.IO;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System.Threading.Tasks;
using Newtonsoft.Json;

class Program
{
    private static string clientId = "f135a36b32054ef49aac4c7e27554f85"; 
    private static string clientSecret = "fd4da5ce69b34becb9dd433c83e8e7dd"; 
    private static string credentialsPath = "credentials.json";
    private static SpotifyClient? _spotify;

    static async Task Main()
    {
        Console.WriteLine("--- Spotify Paal Controller Actief ---");
        _ = Task.Run(() => StartSpotify());

        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                
                // Elke toets koppelen aan een Paal/Playlist
                switch (key)
                {
                    case ConsoleKey.UpArrow:    // Paal 1
                        await Play("spotify:playlist:URI_HIER_1");
                        break;
                    case ConsoleKey.DownArrow:  // Paal 2
                        await Play("spotify:playlist:URI_HIER_2");
                        break;
                    case ConsoleKey.LeftArrow:  // Paal 3
                        await Play("spotify:playlist:URI_HIER_3");
                        break;
                    case ConsoleKey.RightArrow: // Paal 4
                        await Play("spotify:playlist:URI_HIER_4");
                        break;
                    case ConsoleKey.Spacebar:   // Paal 5
                        await Play("spotify:playlist:URI_HIER_5");
                        break;
                    case ConsoleKey.Enter:      // Paal 6 (Vaak de 'Click' actie)
                        await Play("spotify:playlist:URI_HIER_6");
                        break;
                }
            }
            await Task.Delay(50); 
        }
    }

    static async Task Play(string uri)
    {
        if (_spotify == null) return;
        try 
        {
            // Start de playlist op het actieve apparaat
            await _spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest { ContextUri = uri });
            Console.WriteLine($"Spelen: {uri}");
        }
        catch (Exception ex) { Console.WriteLine("Zorg dat Spotify ergens aan staat! Fout: " + ex.Message); }
    }

    static async Task StartSpotify()
    {
        // ... (Dezelfde StartSpotify code als voorheen, MAAR voeg Scopes toe!)
        var request = new LoginRequest(new Uri("http://127.0.0.1:5000/callback"), clientId, LoginRequest.ResponseType.Code)
        {
            Scope = new[] { 
                Scopes.UserReadPlaybackState, 
                Scopes.UserModifyPlaybackState // Essentieel om van playlist te wisselen!
            }
        };
        // Logica voor token laden/server starten (zie je eerdere code)
    }
}