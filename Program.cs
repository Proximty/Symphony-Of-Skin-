using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using SpotifyAPI.Web;

class Program
{
    // Vul hier je eigen Client ID in
    private static readonly string clientId = "f135a36b32054ef49aac4c7e27554f85";
    private static SpotifyClient? _spotify;

    // Je playlist instellingen
    private static readonly Dictionary<ConsoleKey, string> GenreMap = new()
    {
        { ConsoleKey.Spacebar, "spotify:playlist:37i9dQZF1DXcBWIGoYBM5M" },
        { ConsoleKey.UpArrow,  "spotify:playlist:37i9dQZF1DX4sW2JPNYs9L" },
        { ConsoleKey.DownArrow, "spotify:playlist:37i9dQZF1DXcZQQ3sJ9Pga" }
    };

    static async Task Main()
    {
        Console.WriteLine("Systeem start op...");
        
        // 1. Inloggen via Device Code Flow
        using var client = new HttpClient();
        var values = new Dictionary<string, string> { { "client_id", clientId }, { "scope", "user-modify-playback-state" } };
        
        var res = await client.PostAsync("https://accounts.spotify.com/api/device-authorization", new FormUrlEncodedContent(values));
        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        
        var root = doc.RootElement;
        string deviceCode = root.GetProperty("device_code").GetString()!;
        string userCode = root.GetProperty("user_code").GetString()!;
        string url = root.GetProperty("verification_uri_complete").GetString()!;

        Console.WriteLine($"\n--- AUTHENTICATIE VEREIST ---");
        Console.WriteLine($"1. Ga naar: {url}");
        Console.WriteLine($"2. Voer deze code in: {userCode}");
        Console.WriteLine("---------------------------\n");

        // 2. Wacht op token
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

        // 3. Initialiseer client
        _spotify = new SpotifyClient(accessToken);
        Console.WriteLine("Succesvol verbonden met Spotify!");

        // 4. Luister naar Makey Makey input
        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            if (GenreMap.ContainsKey(keyInfo.Key))
            {
                await SpeelMuziek(GenreMap[keyInfo.Key]);
            }
        }
    }

    static async Task SpeelMuziek(string playlistUri)
    {
        try 
        {
            await _spotify!.Player.ResumePlayback(new PlayerResumePlaybackRequest { ContextUri = playlistUri });
            Console.WriteLine($"Muziek gestart: {playlistUri}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fout bij afspelen: {ex.Message}");
        }
    }
}