using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using Newtonsoft.Json;

class Program
{
    private static readonly string clientId = "f135a36b32054ef49aac4c7e27554f85";
    private static readonly string tokenPath = "spotify_token.json";
    private static SpotifyClient? _spotify;

    private static readonly Dictionary<ConsoleKey, string> GenreMap = new()
    {
        { ConsoleKey.Spacebar, "spotify:playlist:37i9dQZF1DXcBWIGoYBM5M" },
        { ConsoleKey.UpArrow,  "spotify:playlist:37i9dQZF1DX4sW2JPNYs9L" },
        { ConsoleKey.DownArrow, "spotify:playlist:37i9dQZF1DXcZQQ3sJ9Pga" }
    };

    static async Task Main()
    {
        if (File.Exists(tokenPath))
        {
            Console.WriteLine("Opgeslagen sessie gevonden. Bezig met laden...");
            var json = File.ReadAllText(tokenPath);
            var token = JsonConvert.DeserializeObject<PKCETokenResponse>(json);
            
            var authenticator = new PKCEAuthenticator(clientId, token);
            authenticator.TokenRefreshed += (sender, t) => File.WriteAllText(tokenPath, JsonConvert.SerializeObject(t));
            
            _spotify = new SpotifyClient(SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator));
            Console.WriteLine("Sessie geladen! Je kunt je Makey Makey gebruiken.");
        }
        else
        {
            await InitialAuth();
        }

        // Loop voor Makey Makey input
        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);
            if (_spotify != null && GenreMap.ContainsKey(keyInfo.Key))
            {
                await SpeelMuziek(GenreMap[keyInfo.Key]);
            }
            await Task.Delay(100);
        }
    }

    static async Task InitialAuth()
    {
        var verifier = GenerateRandomString();
        var challenge = GenerateCodeChallenge(verifier);

        // Gebruik 'localhost' voor de redirect, gebruik SSH-tunnel op laptop als dit faalt
       // De URI in je code MOET exact overeenkomen met het dashboard
       var server = new EmbedIOAuthServer(new Uri("http://10.17.36.151:8080/callback"), 8080);
        await server.Start();

        var loginRequest = new LoginRequest(server.BaseUri, clientId, LoginRequest.ResponseType.Code)
        {
            CodeChallenge = challenge,
            CodeChallengeMethod = "S256",
            Scope = new List<string> { Scopes.UserModifyPlaybackState, Scopes.UserReadPlaybackState }
        };

        Console.WriteLine("\n--- AUTHENTICATIE NODIG (SLECHTS ÉÉN KEER) ---");
        Console.WriteLine("Open deze link op je laptop (gebruik SSH tunnel indien nodig):");
        Console.WriteLine(loginRequest.ToUri());

        server.AuthorizationCodeReceived += async (sender, response) =>
        {
            await server.Stop();
            var tokenResponse = await new OAuthClient().RequestToken(new PKCETokenRequest(clientId, response.Code, server.BaseUri, verifier));
            
            File.WriteAllText(tokenPath, JsonConvert.SerializeObject(tokenResponse));
            
            var authenticator = new PKCEAuthenticator(clientId, tokenResponse);
            _spotify = new SpotifyClient(SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator));
            
            Console.WriteLine("\nSuccesvol gekoppeld! Token is opgeslagen.");
        };
    }

    // ... (GenerateRandomString, GenerateCodeChallenge en SpeelMuziek blijven hetzelfde)
    static string GenerateRandomString() { /* ... */ return ""; } // Vul je bestaande logica hier in
    static string GenerateCodeChallenge(string verifier) { /* ... */ return ""; }
    static async Task SpeelMuziek(string playlistUri) { /* ... */ }
}