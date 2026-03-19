using System;
using System.IO;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using Newtonsoft.Json;

class Program
{
    private static string clientId = "f135a36b32054ef49aac4c7e27554f85"; 
    private static string clientSecret = "fd4da5ce69b34becb9dd433c83e8e7dd"; 
    private static string credentialsPath = "credentials.json";
    private static SpotifyClient? _spotify;

    static async Task Main()
    {
        Console.WriteLine("--- Spotify Paal Controller (The Final Fix) ---");
        _ = Task.Run(() => StartSpotify());

        while (_spotify == null) await Task.Delay(100);

        Console.WriteLine("\nSysteem gereed! Gebruik de Makey Makey.");

        bool _isProcessing = false; 

        while (true)
        {
            if (Console.KeyAvailable && !_isProcessing)
            {
                var key = Console.ReadKey(true).Key;
                
                // VERVANG DEZE LINKS DOOR JOUW EIGEN SPOTIFY URI'S!
                string playlistUri = key switch
                {
                    ConsoleKey.UpArrow    => "https://open.spotify.com/playlist/37i9dQZF1DX4o1oenSJRJd?si=1fa34d2732844f33", 
                    ConsoleKey.DownArrow  => "https://open.spotify.com/playlist/2ibgJKkjNvFac0zfIhftDw?si=78e85b0b10334068", 
                    ConsoleKey.LeftArrow  => "https://open.spotify.com/playlist/37i9dQZF1DWVJyzEwVacEu?si=16d4dc503e4e4445", 
                    ConsoleKey.RightArrow => "https://open.spotify.com/playlist/37i9dQZF1E8LCKAL524VnW?si=04acda6b0e1e4a1e", 
                    ConsoleKey.Spacebar   => "https://open.spotify.com/playlist/37i9dQZF1E8L17wPopyB8V?si=849bb536ec694ec0", 
                    ConsoleKey.Enter      => "https://open.spotify.com/playlist/37i9dQZF1E4t3XGxrTxUnP?si=2c682136d6c74499", 
                    _ => ""
                };

                if (!string.IsNullOrEmpty(playlistUri))
                {
                    _isProcessing = true; // LOCK: Negeer andere inputs
                    await SpeelPlaylist(playlistUri);
                    
                    // Wacht 3 seconden zodat de Makey Makey niet blijft triggeren
                    await Task.Delay(3000); 
                    _isProcessing = false; // UNLOCK
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
            var devices = devicesResponse.Devices;

            if (devices.Count == 0)
            {
                Console.WriteLine("FOUT: Geen actief apparaat. Zet Spotify aan op je laptop!");
                return;
            }

            // DIT IS DE OPDRACHT DIE ECHT OP 'PLAY' DRUKT
            var request = new PlayerResumePlaybackRequest { 
                ContextUri = playlistUri,
                DeviceId = devices[0].Id 
            };
            
            await _spotify.Player.ResumePlayback(request);
            Console.WriteLine($"Aan het afspelen op: {devices[0].Name}");
        }
        catch (Exception ex) { Console.WriteLine("API Fout: " + ex.Message); }
    }

    // --- AUTHENTICATIE LOGICA (Laat dit ongewijzigd) ---
    static async Task StartSpotify()
    {
        if (File.Exists(credentialsPath)) {
            try {
                var json = await File.ReadAllTextAsync(credentialsPath);
                var token = JsonConvert.DeserializeObject<AuthorizationCodeTokenResponse>(json);
                var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(new AuthorizationCodeAuthenticator(clientId, clientSecret, token!));
                _spotify = new SpotifyClient(config);
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
        var loginRequest = new LoginRequest(server.BaseUri, clientId, LoginRequest.ResponseType.Code) { Scope = new[] { Scopes.UserReadPlaybackState, Scopes.UserModifyPlaybackState } };
        BrowserUtil.Open(loginRequest.ToUri());
    }
}