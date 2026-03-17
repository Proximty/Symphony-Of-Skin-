using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using Newtonsoft.Json;

class Program
{
    // --- SPOTIFY INSTELLINGEN ---
    private static string clientId = "f135a36b32054ef49aac4c7e27554f85"; 
    private static string clientSecret = "fd4da5ce69b34becb9dd433c83e8e7dd"; 
    private static string credentialsPath = "credentials.json";
    private static SpotifyClient? _spotify;

    static async Task Main()
    {
        Console.WriteLine("--- Spotify Paal Controller (Makey Makey + API) ---");
        
        // Start de authenticatie flow
        _ = Task.Run(() => StartSpotify());

        // Wachten tot de client klaar is
        while (_spotify == null) 
        {
            await Task.Delay(100);
        }

        Console.WriteLine("\nSysteem gereed! Raak een paal aan op de Makey Makey.");
        Console.WriteLine("Toetsenbord mappings: Pijltjes, Spatie, Enter (Click).");

        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                
                // HIER KOPPEL JE DE PLAYLISTS AAN DE PALEN
                switch (key)
                {
                    case ConsoleKey.UpArrow:    // Paal 1
                        await SpeelPlaylist("https://open.spotify.com/playlist/37i9dQZF1DX4o1oenSJRJd?si=e3c312f2421d4b76");
                        break;
                    case ConsoleKey.DownArrow:  // Paal 2
                        await SpeelPlaylist("https://open.spotify.com/playlist/37i9dQZF1DWVJyzEwVacEu?si=d882e56fb4544368");
                        break;
                    case ConsoleKey.LeftArrow:  // Paal 3
                        await SpeelPlaylist("https://open.spotify.com/playlist/2ibgJKkjNvFac0zfIhftDw?si=660276ab3f6647df");
                        break;
                    case ConsoleKey.RightArrow: // Paal 4
                        await SpeelPlaylist("https://open.spotify.com/playlist/61jNo7WKLOIQkahju8i0hw?si=8a9cc0d7237941cb");
                        break;
                    case ConsoleKey.Spacebar:   // Paal 5
                        await SpeelPlaylist("https://open.spotify.com/playlist/5PWLyQD0eqfTW2hhRulHIX?si=5cd44ca9b7534982");
                        break;
                    case ConsoleKey.Enter:      // Paal 6 (De 'Click' aansluiting)
                        await SpeelPlaylist("https://open.spotify.com/playlist/0GtaEQ91LENQitjz9IHj4g?si=bf839554a3e1424b");
                        break;
                }
            }
            await Task.Delay(50); 
        }
    }

    static async Task SpeelPlaylist(string playlistUri)
    {
        if (_spotify == null) return;

        try 
        {
            // Zoek eerst naar beschikbare apparaten (zoals je laptop of speaker)
            var devicesResponse = await _spotify.Player.GetAvailableDevices();
            var devices = devicesResponse.Devices;

            if (devices.Count == 0)
            {
                Console.WriteLine("WAARSCHUWING: Geen actief apparaat gevonden. Zet Spotify aan!");
                return;
            }

            // Start de playlist op het eerste beschikbare apparaat
            var request = new PlayerResumePlaybackRequest 
            { 
                ContextUri = playlistUri,
                DeviceId = devices[0].Id 
            };
            
            await _spotify.Player.ResumePlayback(request);
            Console.WriteLine($"Succes! Playlist gestart op {devices[0].Name}");
        }
        catch (Exception ex) 
        { 
            Console.WriteLine("Fout bij afspelen via API: " + ex.Message); 
        }
    }

    static async Task StartSpotify()
    {
        // 1. Probeer bestaande credentials te laden
        if (File.Exists(credentialsPath))
        {
            try 
            {
                var json = await File.ReadAllTextAsync(credentialsPath);
                var token = JsonConvert.DeserializeObject<AuthorizationCodeTokenResponse>(json);
                var authenticator = new AuthorizationCodeAuthenticator(clientId, clientSecret, token!);
                
                var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
                _spotify = new SpotifyClient(config);
                Console.WriteLine("Ingelogd via opgeslagen gegevens.");
                return;
            }
            catch { /* Token verlopen, start nieuwe login */ }
        }

        // 2. Browser Login Flow
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
            Console.WriteLine("Spotify succesvol gekoppeld!");
        };

        var request = new LoginRequest(server.BaseUri, clientId, LoginRequest.ResponseType.Code)
        {
            Scope = new[] { 
                Scopes.UserReadPlaybackState, 
                Scopes.UserModifyPlaybackState, 
                Scopes.PlaylistReadPrivate 
            }
        };
        BrowserUtil.Open(request.ToUri());
    }
}