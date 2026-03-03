using System;
using System.IO;
using System.Runtime.InteropServices;
using CSCore.CoreAudioAPI;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System.Threading.Tasks;
using Newtonsoft.Json;

class Program
{
    // --- SPOTIFY SETTINGS ---
    private static string clientId = "f135a36b32054ef49aac4c7e27554f85"; 
    private static string clientSecret = "fd4da5ce69b34becb9dd433c83e8e7dd"; 
    private static string credentialsPath = "credentials.json";
    private static SpotifyClient? _spotify;

    // --- MEDIA KEYS ---
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
    const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    const byte VK_MEDIA_PREV_TRACK = 0xB1;
    const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    const uint KEYEVENTF_KEYUP = 0x0002;

    static async Task Main()
    {
        Console.WriteLine("Systeem opstarten...");

        // Start Spotify authenticatie
        _ = Task.Run(() => StartSpotify());

        // Audio setup (zonder visuals)
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        using var meter = AudioMeterInformation.FromDevice(device);

        Console.WriteLine("Systeem actief. Druk op ESC om te stoppen.");
        Console.WriteLine("Console luistert nu naar audio levels...");

        // Simpele console loop in plaats van een window
        while (true)
        {
            float peak = meter.PeakValue;
            
            // Voorbeeld van output zonder graphics:
            if (peak > 0.5f) 
            {
                Console.WriteLine($"[BEAT DETECTED] Level: {peak:P0}");
            }

            // Omdat we geen Raylib.IsKeyPressed meer hebben, kun je Console.KeyAvailable gebruiken
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Escape) break;
                if (key == ConsoleKey.Spacebar) SendMediaKey(VK_MEDIA_PLAY_PAUSE);
                if (key == ConsoleKey.UpArrow) SendMediaKey(VK_MEDIA_NEXT_TRACK);
                if (key == ConsoleKey.DownArrow) SendMediaKey(VK_MEDIA_PREV_TRACK);
            }

            await Task.Delay(10); // Voorkom 100% CPU gebruik
        }
    }

    static async Task StartSpotify()
    {
        if (File.Exists(credentialsPath))
        {
            try 
            {
                var json = await File.ReadAllTextAsync(credentialsPath);
                var token = JsonConvert.DeserializeObject<AuthorizationCodeTokenResponse>(json);
                var authenticator = new AuthorizationCodeAuthenticator(clientId, clientSecret, token!);
                
                var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
                _spotify = new SpotifyClient(config);

                authenticator.TokenRefreshed += (sender, newToken) => 
                    File.WriteAllText(credentialsPath, JsonConvert.SerializeObject(newToken));

                await RunPollingLoop();
                return;
            }
            catch { /* Token corrupt of verlopen */ }
        }

        var server = new EmbedIOAuthServer(new Uri("http://127.0.0.1:5000/callback"), 5000);
        await server.Start();

        server.AuthorizationCodeReceived += async (sender, response) =>
        {
            var tokenResponse = await new OAuthClient().RequestToken(
                new AuthorizationCodeTokenRequest(clientId, clientSecret, response.Code, server.BaseUri)
            );

            await File.WriteAllTextAsync(credentialsPath, JsonConvert.SerializeObject(tokenResponse));
            var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(new AuthorizationCodeAuthenticator(clientId, clientSecret, tokenResponse));
            _spotify = new SpotifyClient(config);
            
            await server.Stop();
            Console.WriteLine("Spotify verbonden!");
            await RunPollingLoop();
        };

        var request = new LoginRequest(server.BaseUri, clientId, LoginRequest.ResponseType.Code)
        {
            Scope = new[] { Scopes.UserReadCurrentlyPlaying, Scopes.UserReadPlaybackState }
        };
        // Handmatig openen van browser voor login
        Console.WriteLine("Open je browser om in te loggen bij Spotify...");
        // In een echte headless omgeving zou je de URL printen: Console.WriteLine(request.ToUri());
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(request.ToUri().ToString()) { UseShellExecute = true });
    }

    static async Task RunPollingLoop()
    {
        while (true)
        {
            if (_spotify != null)
            {
                try {
                    var playback = await _spotify.Player.GetCurrentPlayback();
                    if (playback?.Item is FullTrack track) {
                        Console.Title = $"Nu speelt: {track.Name} - {track.Artists[0].Name}";
                    }
                } catch { }
            }
            await Task.Delay(3000);
        }
    }

    static void SendMediaKey(byte key) 
    { 
        keybd_event(key, 0, 0, 0); 
        keybd_event(key, 0, KEYEVENTF_KEYUP, 0); 
    }
}