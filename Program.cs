using System;
using System.IO;
using System.Linq;
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
        Console.WriteLine("--- Spotify Paal Controller (Hold to Play + Auto-Device) ---");
        await StartSpotify();
        while (_spotify == null) await Task.Delay(100);

        Console.WriteLine("\nSysteem gereed! Houd een contact vast.");

        bool isPlaying = false;
        int activeKey = 0;

        while (true)
        {
            int pressedKey = GetPressedKey();

            if (pressedKey != 0 && !isPlaying)
            {
                string uri = GetUriForKey(pressedKey);
                if (!string.IsNullOrEmpty(uri))
                {
                    // FIX: Zoek eerst een apparaat voordat we proberen te spelen
                    var deviceId = await GetActiveDeviceId();
                    
                    if (deviceId != null)
                    {
                        try {
                            Console.WriteLine($"\n[AANRAKING] Starten op apparaat...");
                            await _spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest { 
                                ContextUri = uri,
                                DeviceId = deviceId // Dwing Spotify om dit apparaat te gebruiken
                            });
                            isPlaying = true;
                            activeKey = pressedKey;
                        }
                        catch (Exception ex) { Console.WriteLine("Fout bij starten: " + ex.Message); }
                    }
                    else {
                        Console.WriteLine("FOUT: Geen actieve Spotify app gevonden. Zet Spotify aan!");
                        await Task.Delay(2000); // Wacht even voor de volgende check
                    }
                }
            }
            else if (isPlaying && (GetAsyncKeyState(activeKey) >= 0))
            {
                try {
                    Console.WriteLine("[LOSGELATEN] Pauzeren...");
                    await _spotify.Player.PausePlayback();
                } catch { /* Al gepauzeerd of apparaat weg */ }
                
                isPlaying = false;
                activeKey = 0;
                await Task.Delay(200); 
            }

            await Task.Delay(50);
        }
    }

    // Hulpmiddel om het ID van je laptop/telefoon op te halen
    static async Task<string?> GetActiveDeviceId()
    {
        try {
            var devices = await _spotify!.Player.GetAvailableDevices();
            // Pak het eerste apparaat dat beschikbaar is
            return devices.Devices.FirstOrDefault()?.Id;
        } catch { return null; }
    }

    static int GetPressedKey()
    {
        if (GetAsyncKeyState(0x26) < 0) return 0x26; // Up
        if (GetAsyncKeyState(0x28) < 0) return 0x28; // Down
        if (GetAsyncKeyState(0x25) < 0) return 0x25; // Left
        if (GetAsyncKeyState(0x27) < 0) return 0x27; // Right
        if (GetAsyncKeyState(0x20) < 0) return 0x20; // Space
        if (GetAsyncKeyState(0x0D) < 0) return 0x0D; // Enter
        return 0;
    }

    static string GetUriForKey(int vKey)
    {
        return vKey switch
        {
            0x26 => "https://open.spotify.com/playlist/37i9dQZF1EVJSvZp5AOML2?si=28abb051846241fd", 
            0x28 => "spotify:playlist:37i9dQZF1DX0XUsKG7PBeI", 
            0x25 => "spotify:playlist:37i9dQZF1DX4dyzvuaB0nB", 
            0x27 => "spotify:playlist:37i9dQZF1DXcF6BvY9tqeC", 
            0x20 => "spotify:playlist:37i9dQZF1DX1s9vYpYpXqf", 
            0x0D => "spotify:playlist:37i9dQZF1DX4sWvAiTbnO3", 
            _ => ""
        };
    }

    static async Task StartSpotify()
    {
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