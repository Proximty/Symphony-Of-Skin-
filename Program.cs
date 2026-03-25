using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using Newtonsoft.Json;

class Program
{
    private static string clientId = "f135a36b32054ef49aac4c7e27554f85"; 
    private static string clientSecret = "fd4da5ce69b34becb9dd433c83e8e7dd"; 
    private static string credentialsPath = "credentials.json";
    private static SpotifyClient? _spotify;

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    static async Task Main()
    {
        Console.WriteLine("--- Spotify Paal Controller (Makey Makey Click Mode) ---");
        await StartSpotify();
        while (_spotify == null) await Task.Delay(100);

        Console.WriteLine("\nSysteem gereed! Gebruik de pijltjes of de CLICK op de Makey Makey.");

        bool isPlaying = false;
        string currentUri = "";

        while (true)
        {
            var pressedKeys = GetPressedKeys();

            if (pressedKeys.Count > 0)
            {
                string targetUri = GetPlaylistForCombo(pressedKeys);

                if (!string.IsNullOrEmpty(targetUri) && currentUri != targetUri)
                {
                    // Toon "Click" in de console als key 1 wordt ingedrukt
                    var displayKeys = pressedKeys.Select(k => k == 1 ? "Click" : k.ToString());
                    Console.WriteLine($"\n[MODUS] Combinatie herkend: {string.Join(" + ", displayKeys)}");
                    
                    await Play(targetUri);
                    isPlaying = true;
                    currentUri = targetUri;
                }
            }
            else if (isPlaying)
            {
                Console.WriteLine("[LOSGELATEN] Pauzeren...");
                try { await _spotify.Player.PausePlayback(); } catch { }
                isPlaying = false;
                currentUri = "";
                await Task.Delay(200); 
            }

            await Task.Delay(100);
        }
    }

    static string GetPlaylistForCombo(List<int> keys)
    {
        keys.Sort();
        string comboId = string.Join(",", keys);

        return comboId switch
        {
            // --- GEHEIME COMBO'S ---
            "38,40" => "https://open.spotify.com/playlist/37i9dQZF1EIgG2NEOhqsD7?si=bd6c466a80bc4155", 
            "37,39" => "https://open.spotify.com/playlist/37i9dQZF1EQqedj0y9Uwvu?si=c6ac4c4f9d6b422a", 
            "1,32"  => "https://open.spotify.com/playlist/37i9dQZF1EIghjZV03OkEv?si=19f8d6ffd07743c3", // Click + Space
            
            // --- NORMALE TOETSEN ---
            "1"  => "https://open.spotify.com/playlist/37i9dQZF1E3517hW8wnMUj?si=19ab49be08b442d9", // Linkermuisklik (Makey Makey Click)
            "38" => "https://open.spotify.com/playlist/37i9dQZF1EVJSvZp5AOML2?si=7d685ee88d9c42dc", // Up
            "40" => "https://open.spotify.com/playlist/2ibgJKkjNvFac0zfIhftDw?si=fcf91eb297694e36", // Down
            "37" => "https://open.spotify.com/playlist/37i9dQZEVXcP53YF7Dzbvj?si=a4ff4f4160504681",    // Left
            "39" => "https://open.spotify.com/playlist/61jNo7WKLOIQkahju8i0hw?si=cddf24329e11472a",    // Right
            "32" => "https://open.spotify.com/playlist/37i9dQZF1E3ajQz6d6ih8u?si=73fea0cefe6d4620",    // Space
            
            _ => "" 
        };
    }

    static List<int> GetPressedKeys()
    {
        // 1 is de code voor de 'Click' actie van de Makey Makey
        int[] keysToCheck = { 1, 38, 40, 37, 39, 32 }; 
        var pressed = new List<int>();
        foreach (var key in keysToCheck) { if (GetAsyncKeyState(key) < 0) pressed.Add(key); }
        return pressed;
    }

    static async Task Play(string uri)
    {
        var deviceId = await GetActiveDeviceId();
        if (deviceId != null) {
            try { await _spotify!.Player.ResumePlayback(new PlayerResumePlaybackRequest { ContextUri = uri, DeviceId = deviceId }); }
            catch (Exception ex) { Console.WriteLine("Fout: " + ex.Message); }
        }
    }

    static async Task<string?> GetActiveDeviceId() {
        try { var devices = await _spotify!.Player.GetAvailableDevices(); return devices.Devices.FirstOrDefault()?.Id; } catch { return null; }
    }

    static async Task StartSpotify() {
        if (File.Exists(credentialsPath)) {
            try {
                var json = await File.ReadAllTextAsync(credentialsPath);
                var token = JsonConvert.DeserializeObject<AuthorizationCodeTokenResponse>(json);
                _spotify = new SpotifyClient(SpotifyClientConfig.CreateDefault().WithAuthenticator(new AuthorizationCodeAuthenticator(clientId, clientSecret, token!)));
                return;
            } catch { }
        }
        var server = new EmbedIOAuthServer(new Uri("http://127.0.0.1:5005/callback"), 5005);
        await server.Start();
        server.AuthorizationCodeReceived += async (sender, response) => {
            var tokenResponse = await new OAuthClient().RequestToken(new AuthorizationCodeTokenRequest(clientId, clientSecret, response.Code, server.BaseUri));
            await File.WriteAllTextAsync(credentialsPath, JsonConvert.SerializeObject(tokenResponse));
            _spotify = new SpotifyClient(SpotifyClientConfig.CreateDefault().WithAuthenticator(new AuthorizationCodeAuthenticator(clientId, clientSecret, tokenResponse)));
            await server.Stop();
        };
        BrowserUtil.Open(new LoginRequest(server.BaseUri, clientId, LoginRequest.ResponseType.Code) { Scope = new[] { Scopes.UserReadPlaybackState, Scopes.UserModifyPlaybackState } }.ToUri());
    }
}