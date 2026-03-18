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
        { ConsoleKey.Spacebar, "spotify:playlist:37i9dQZF1DXcBWIGoYBM5M" },
        { ConsoleKey.UpArrow,  "spotify:playlist:37i9dQZF1DX4sW2JPNYs9L" },
        { ConsoleKey.DownArrow, "spotify:playlist:37i9dQZF1DXcZQQ3sJ9Pga" }
    };

    static async Task Main()
    {
        Console.WriteLine("Systeem start op...");
        using var client = new HttpClient();

        // 1. Request Device Code
        var values = new Dictionary<string, string> 
        { 
            { "client_id", clientId }, 
            { "scope", "user-modify-playback-state user-read-playback-state" } 
        };
        
        var res = await client.PostAsync("https://accounts.spotify.com/api/device/authorization", new FormUrlEncodedContent(values));
        var jsonResponse = await res.Content.ReadAsStringAsync();
        
        // Safety check to prevent the '<' crash
        if (!jsonResponse.Trim().StartsWith("{")) 
        {
            Console.WriteLine("\n--- ERROR ---");
            Console.WriteLine("Spotify sent back an error page instead of data.");
            Console.WriteLine("Response content: " + jsonResponse);
            return;
        }

        using var doc = JsonDocument.Parse(jsonResponse);
        string deviceCode = doc.RootElement.GetProperty("device_code").GetString()!;
        string userCode = doc.RootElement.GetProperty("user_code").GetString()!;
        string url = doc.RootElement.GetProperty("verification_uri").GetString()!;

        Console.WriteLine($"\nGa naar: {url}");
        Console.WriteLine($"Code: {userCode}\n");

        // 2. Poll for Token
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
        Console.WriteLine("Verbonden! Gebruik de Makey Makey.");

        // 3. Input Loop
        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            if (GenreMap.ContainsKey(keyInfo.Key) && _spotify is not null)
            {
                try {
                    await _spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest { ContextUri = GenreMap[keyInfo.Key] });
                    Console.WriteLine($"Afspelen: {GenreMap[keyInfo.Key]}");
                }
                catch (Exception ex) { Console.WriteLine($"Fout: {ex.Message}"); }
            }
        }
    }
}