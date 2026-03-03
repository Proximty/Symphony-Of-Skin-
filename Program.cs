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
                        await SpeelPlaylist("https://open.spotify.com/playlist/37i9dQZF1DX1kCIzMYtzum?si=9fbae2c2e8b54ea7");
                        break;
                    case ConsoleKey.DownArrow:  // Paal 2
                        await SpeelPlaylist("https://open.spotify.com/playlist/37i9dQZF1DWVJyzEwVacEu?si=eae73badc6424225");
                        break;
                    case ConsoleKey.LeftArrow:  // Paal 3
                        await SpeelPlaylist("https://open.spotify.com/playlist/37i9dQZEVXbNG2KDcFcKOF?si=3f2e946c0daa4ef4");
                        break;
                    case ConsoleKey.RightArrow: // Paal 4
                        await SpeelPlaylist("spotify:playlist:JOUW_URI_4");
                        break;
                    case ConsoleKey.Spacebar:   // Paal 5
                        await SpeelPlaylist("spotify:playlist:JOUW_URI_5");
                        break;
                    case ConsoleKey.Enter:      // Paal 6 (De 'Click' aansluiting)
                        await SpeelPlaylist("spotify:playlist:JOUW_URI_6");
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
        var devicesResponse = await _spotify.Player.GetAvailableDevices();
        // Zoek specifiek naar de Raspberry Pi in de lijst met apparaten
        var piDevice = devicesResponse.Devices.Find(d => d.Name.ToLower().Contains("raspotify") || d.Name.ToLower().Contains("raspberry"));

        if (piDevice == null)
        {
            Console.WriteLine("WAARSCHUWING: Raspberry Pi (Raspotify) niet gevonden in het netwerk!");
            return;
        }

        // Activeer de Pi en speel de playlist af
        var request = new PlayerResumePlaybackRequest 
        { 
            ContextUri = playlistUri,
            DeviceId = piDevice.Id 
        };
        
        await _spotify.Player.ResumePlayback(request);
        Console.WriteLine($"Muziek gestart op de Raspberry Pi: {piDevice.Name}");
    }
    catch (Exception ex) 
    { 
        Console.WriteLine("Fout: " + ex.Message); 
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