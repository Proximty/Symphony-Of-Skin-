using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using SpotifyAPI.Web;

class Program
{
    private static readonly string clientId = "f135a36b32054ef49aac4c7e27554f85";
    private static SpotifyClient? _spotify;

    private static readonly Dictionary<ConsoleKey, string> GenreMap = new()
    {
        { ConsoleKey.Spacebar, "https://open.spotify.com/playlist/37i9dQZF1EIf9owOnDezxl?si=b3ee18ecfe7842d9" },
        { ConsoleKey.UpArrow,  "https://open.spotify.com/playlist/37i9dQZF1DXbYM3nMM0oPk?si=511ce4e204e1450a" },
        { ConsoleKey.DownArrow, "spotify:playlist:37i9dQZF1DXcZQQ3sJ9Pga" }
    };

    static async Task Main()
    {
        Console.WriteLine("Systeem start op...");
        using var client = new HttpClient();

        // 1. Officiële Spotify Device Code aanvraag
        var values = new Dictionary<string, string> { 
            { "client_id", clientId }, 
            { "scope", "user-modify-playback-state" } 
        };
        
        // DEZE URLS ZIJN DE OFFICIELE SPOTIFY ENDPOINTS
        var res = await client.PostAsync("https://accounts.spotify.com/api/device-authorization", new FormUrlEncodedContent(values));
        var json = await res.Content.ReadAsStringAsync();
        
        using var doc = JsonDocument.Parse(json);
        string deviceCode = doc.RootElement.GetProperty("device_code").GetString()!;
        string userCode = doc.RootElement.GetProperty("user_code").GetString()!;
        string url = doc.RootElement.GetProperty("verification_uri_complete").GetString()!;

        Console.WriteLine($"\nGa naar: {url}");
        Console.WriteLine($"Voer code in: {userCode}\n");

        // 2. Pollen tot de gebruiker geautoriseerd heeft
        string accessToken = "";
        while (string.IsNullOrEmpty(accessToken))
        {
            await Task.Delay(5000);
            var tokenValues = new Dictionary<string, string> {
                { "grant_type", "urn:ietf:params:oauth:grant-type:device_code" },
                { "device_code", deviceCode },
                { "client_id", clientId }
            };
            
            var tokenRes = await client.PostAsync("https://accounts.spotify.com/api/token", new FormUrlEncodedContent(tokenValues));
            var tokenJson = await tokenRes.Content.ReadAsStringAsync();
            
            if (tokenJson.Contains("access_token")) {
                using var tokenDoc = JsonDocument.Parse(tokenJson);
                accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString()!;
            }
        }

        _spotify = new SpotifyClient(accessToken);
        Console.WriteLine("Succesvol verbonden!");

        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            if (GenreMap.ContainsKey(keyInfo.Key))
            {
                await _spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest { ContextUri = GenreMap[keyInfo.Key] });
            }
        }
    }
}