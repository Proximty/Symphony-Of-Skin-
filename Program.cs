using System;
using System.IO;
using System.Runtime.InteropServices;
using Raylib_cs;
using CSCore.CoreAudioAPI;
using System.Numerics;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System.Threading.Tasks;
using Newtonsoft.Json;

class Program
{
    // --- SPOTIFY SETTINGS ---
    private static string clientId = "f135a36b32054ef49aac4c7e27554f85"; 
    private static string clientSecret = "fd4da5ce69b34becb9dd433c83e8e7dd"; // Let op: Houd deze geheim!
    private static string credentialsPath = "credentials.json";
    private static string currentTrackInfo = "Connecting to Spotify...";
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
        // Start Spotify op de achtergrond (Standalone mode)
        _ = Task.Run(() => StartSpotify());

        // Kiosk mode optie: Fullscreen zonder muis
        // Raylib.SetConfigFlags(ConfigFlags.FullscreenMode); 
        Raylib.InitWindow(1400, 900, "Spotify Sync - Standalone High Impact");
        Raylib.SetTargetFPS(60);

        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        using var meter = AudioMeterInformation.FromDevice(device);

        float smoothVolume = 0f;
        float waveTime = 0f;
        float smoothHue = 240f; 
        float volumeThreshold = 0f;
        float autoGain = 1.0f; 

        while (!Raylib.WindowShouldClose())
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Space)) SendMediaKey(VK_MEDIA_PLAY_PAUSE);
            if (Raylib.IsKeyPressed(KeyboardKey.Up)) SendMediaKey(VK_MEDIA_NEXT_TRACK);
            if (Raylib.IsKeyPressed(KeyboardKey.Down)) SendMediaKey(VK_MEDIA_PREV_TRACK);

            float peak = meter.PeakValue; 

            // --- VOLUME STABILISATIE ---
            if (peak > 0.001f) 
            {
                float targetGain = 0.50f / (peak + 0.01f); 
                autoGain += (targetGain - autoGain) * 0.015f;
            }
            autoGain = MathF.Max(1.0f, MathF.Min(autoGain, 15.0f));
            float boostedPeak = MathF.Min(1.3f, peak * autoGain);
            
            float targetIntensity = (boostedPeak * 220f) + 15f; 
            smoothVolume += (targetIntensity - smoothVolume) * 0.20f; 
            waveTime += 0.04f + (boostedPeak * 0.20f); 

            if (peak > volumeThreshold) volumeThreshold = peak;
            else volumeThreshold -= 0.005f;

            float impact = MathF.Max(0f, peak - (volumeThreshold * 0.82f));
            float colorTrigger = MathF.Min(1.0f, impact / 0.05f);

            float targetHue = 240f - (colorTrigger * 240f); 
            float lerpSpeed = (targetHue < smoothHue) ? 0.4f : 0.02f;
            smoothHue += (targetHue - smoothHue) * lerpSpeed; 

            Color beatColor = Raylib.ColorFromHSV(smoothHue, 0.95f, 1.0f);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(5, 5, 12, 255)); 

            DrawGrid(Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), beatColor, boostedPeak);

            Raylib.BeginBlendMode(BlendMode.Additive);
            
            float mainThickness = 5.0f + (boostedPeak * 10.0f);
            Color neon1 = new Color(beatColor.R, beatColor.G, beatColor.B, (byte)160);
            DrawBassWave(Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), waveTime, smoothVolume, 0.012f, neon1, mainThickness, false);
            DrawBassWave(Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), waveTime, smoothVolume * 0.95f, 0.012f, new Color(255, 255, 255, 220), 2.5f, false);

            float secondaryThickness = 3.0f + (boostedPeak * 6.0f);
            Color neon2 = new Color(beatColor.R, beatColor.G, beatColor.B, (byte)100);
            DrawBassWave(Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), waveTime * 1.05f, smoothVolume * 0.70f, 0.014f, neon2, secondaryThickness, true); 
            DrawBassWave(Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), waveTime * 1.05f, smoothVolume * 0.60f, 0.014f, new Color(220, 220, 255, 130), 1.5f, true);

            Raylib.EndBlendMode();

            // --- UI ---
            Raylib.DrawRectangle(25, 25, 450, 70, new Color(0, 0, 0, 150));
            Raylib.DrawText("HIGH IMPACT MODE", 35, 35, 18, beatColor);
            Raylib.DrawText(currentTrackInfo, 35, 60, 22, Color.White);
            
            Raylib.EndDrawing();
        }
        Raylib.CloseWindow();
    }

    static async Task StartSpotify()
    {
        // 1. Probeer bestaande inloggegevens te laden
        if (File.Exists(credentialsPath))
        {
            try 
            {
                var json = await File.ReadAllTextAsync(credentialsPath);
                var token = JsonConvert.DeserializeObject<AuthorizationCodeTokenResponse>(json);
                var authenticator = new AuthorizationCodeAuthenticator(clientId, clientSecret, token!);
                
                var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
                _spotify = new SpotifyClient(config);

                // Update token in bestand als deze ververst wordt
                authenticator.TokenRefreshed += (sender, newToken) => 
                    File.WriteAllText(credentialsPath, JsonConvert.SerializeObject(newToken));

                await RunPollingLoop();
                return;
            }
            catch { /* Token corrupt of verlopen, doe opnieuw login */ }
        }

        // 2. Geen geldige credentials? Start browser login
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
            await RunPollingLoop();
        };

        var request = new LoginRequest(server.BaseUri, clientId, LoginRequest.ResponseType.Code)
        {
            Scope = new[] { Scopes.UserReadCurrentlyPlaying, Scopes.UserReadPlaybackState }
        };
        BrowserUtil.Open(request.ToUri());
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
                        currentTrackInfo = $"{track.Name} - {track.Artists[0].Name}";
                    }
                } catch { }
            }
            await Task.Delay(3000);
        }
    }

    static void SendMediaKey(byte key) { keybd_event(key, 0, 0, 0); keybd_event(key, 0, KEYEVENTF_KEYUP, 0); }

    static void DrawGrid(int w, int h, Color accent, float peak)
    {
        byte alpha = (byte)(15 + (peak * 60));
        Color gridColor = new Color(accent.R, accent.G, accent.B, alpha);
        for (int i = 0; i <= w; i += 85) Raylib.DrawLine(i, 0, i, h, gridColor);
        for (int i = 0; i <= h; i += 85) Raylib.DrawLine(0, i, w, i, gridColor);
    }

    static void DrawBassWave(int w, int h, float time, float intensity, float frequency, Color color, float thickness, bool isOpposite)
    {
        Vector2 previousPoint = new Vector2(0, (float)h / 2);
        for (int x = 0; x <= w; x += 4)
        {
            float multiplier = isOpposite ? -1.0f : 1.0f;
            float y = (float)Math.Sin(x * frequency + time) * intensity * multiplier;
            Vector2 currentPoint = new Vector2(x, (float)h / 2 + y);
            if (x > 0) Raylib.DrawLineEx(previousPoint, currentPoint, thickness, color);
            previousPoint = currentPoint;
        }
    }
}