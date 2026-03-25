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
        Console.WriteLine("--- Spotify Paal Controller (Super Combo Mode) ---");
        await StartSpotify();
        while (_spotify == null) await Task.Delay(100);

        Console.WriteLine("\nSysteem gereed! Probeer verschillende combinaties.");

        bool isPlaying = false;
        string currentUri = "";

        while (true)
        {
            var pressedKeys = GetPressedKeys();

            if (pressedKeys.Count > 0)
            {
                // Bepaal de playlist op basis van de specifieke combinatie
                string targetUri = GetPlaylistForCombo(pressedKeys);

                if (!string.IsNullOrEmpty(targetUri) && currentUri != targetUri)
                {
                    Console.WriteLine($"\n[MODUS] Combinatie herkend: {string.Join(" + ", pressedKeys)}");
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

    // HIER VOEG JE JOUW COMBO'S TOE
    static string GetPlaylistForCombo(List<int> keys)
    {
        // Sorteer de lijst zodat de volgorde van indrukken niet uitmaakt
        keys.Sort();
        string comboId = string.Join(",", keys);

        return comboId switch
        {
            // --- GEHEIME COMBO'S (2 of meer toetsen) ---
            "38,40"       => "https://open.spotify.com/playlist/37i9dQZF1EIgG2NEOhqsD7?si=bd6c466a80bc4155", // Up + Down
            "37,39"       => "spotify:playlist:PLAYLIST_ID_VOOR_LEFT_EN_RIGHT", // Left + Right
            "32,38"    => "spotify:playlist:PLAYLIST_ID_VOOR_SPACE_UP_DOWN", // Space + Up 
            
            // --- NORMALE TOETSEN (1 toets) ---
            "38" => "https://open.spotify.com/playlist/37i9dQZF1EVJSvZp5AOML2?si=7d685ee88d9c42dc", // Up
            "40" => "https://open.spotify.com/playlist/37i9dQZF1DX4o1oenSJRJd?si=24a6f50d36d64e54", // Down
            "37" => "spotify:playlist:37i9dQZF1DX4dyzvuaB0nB", // Left
            "39" => "spotify:playlist:37i9dQZF1DXcF6BvY9tqeC", // Right
            "32" => "spotify:playlist:37i9dQZF1DX1s9vYpYpXqf", // Space
            "13" => "spotify:playlist:37i9dQZF1DX4sWvAiTbnO3", // Enter
            
            _ => "" // Geen match? Doe niets.
        };
    }

    static List<int> GetPressedKeys()
    {
        int[] keysToCheck = { 38, 40, 37, 39, 32, 13 }; // Up, Down, Left, Right, Space, Enter
        var pressed = new List<int>();
        foreach (var key in keysToCheck) { if (GetAsyncKeyState(key) < 0) pressed.Add(key); }
        return pressed;
    }

    // --- STANDAARD HELPER FUNCTIES (Device & Auth) ---
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