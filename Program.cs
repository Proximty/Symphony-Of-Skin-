using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SpotifyAPI.Web;

class Program
{
    // Vul hier je eigen gegevens in
    private static readonly string clientId = "f135a36b32054ef49aac4c7e27554f85";
    private static readonly string clientSecret = "fd4da5ce69b34becb9dd433c83e8e7dd";
    private static SpotifyClient? _spotify;

    private static readonly Dictionary<ConsoleKey, string> GenreMap = new()
    {
        { ConsoleKey.Spacebar, "spotify:playlist:37i9dQZF1DXcBWIGoYBM5M" }, // Jazz
        { ConsoleKey.UpArrow,  "spotify:playlist:37i9dQZF1DX4sW2JPNYs9L" }, // Rock
        { ConsoleKey.DownArrow, "spotify:playlist:37i9dQZF1DXcZQQ3sJ9Pga" }  // Pop
    };

    static async Task Main()
    {
        Console.WriteLine("Systeem opgestart. Log in bij Spotify...");
        
        // 1. Initialiseer Spotify (Vervang 'JOUW_TOKEN' door een geldig access token)
        // Je kunt een token ophalen via de Spotify Developer Dashboard 'Console'
        var config = SpotifyClientConfig.CreateDefault();
        _spotify = new SpotifyClient(config.WithToken("JOUW_ACCESS_TOKEN"));

        Console.WriteLine("Klaar voor input! Gebruik je Makey Makey.");

        while (true)
        {
            // Luister naar input van toetsenbord (Makey Makey stuurt toetsen)
            var keyInfo = Console.ReadKey(intercept: true);
            
            if (GenreMap.ContainsKey(keyInfo.Key))
            {
                string playlistUri = GenreMap[keyInfo.Key];
                Console.WriteLine($"Input ontvangen: {keyInfo.Key}. Genre playlist starten...");
                
                await SpeelMuziek(playlistUri);
            }
        }
    }

    static async Task SpeelMuziek(string playlistUri)
    {
        if (_spotify == null) return;

        try 
        {
            var resumeRequest = new PlayerResumePlaybackRequest { ContextUri = playlistUri };
            await _spotify.Player.ResumePlayback(resumeRequest);
            Console.WriteLine("Muziek gestart via Spotify Connect!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fout: {ex.Message}. Check of Spotify actief is op je Pi.");
        }
    }
}