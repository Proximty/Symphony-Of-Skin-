using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography; // Voor PKCE beveiliging
using System.Text;                  // Voor de string omzetting
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;

class Program
{
    private static readonly string clientId = "f135a36b32054ef49aac4c7e27554f85";
    private static SpotifyClient? _spotify;

    private static readonly Dictionary<ConsoleKey, string> GenreMap = new()
    {
        { ConsoleKey.Spacebar, "spotify:playlist:37i9dQZF1DXcBWIGoYBM5M" }, // Jazz
        { ConsoleKey.UpArrow,  "spotify:playlist:37i9dQZF1DX4sW2JPNYs9L" }, // Rock
        { ConsoleKey.DownArrow, "spotify:playlist:37i9dQZF1DXcZQQ3sJ9Pga" }  // Pop
    };

    static async Task Main()
    {
        Console.WriteLine("Systeem start op... Authenticatie in browser vereist.");

        // Handmatige PKCE setup (Onafhankelijk van library-errors)
        var verifier = GenerateRandomString();
        var challenge = GenerateCodeChallenge(verifier);

        var server = new EmbedIOAuthServer(new Uri("http://localhost:8080/callback"), 8080);
        await server.Start();

        var loginRequest = new LoginRequest(server.BaseUri, clientId, LoginRequest.ResponseType.Code)
        {
            CodeChallenge = challenge,
            CodeChallengeMethod = "S256",
            Scope = new List<string> { Scopes.UserModifyPlaybackState, Scopes.UserReadPlaybackState }
        };

        BrowserUtil.Open(loginRequest.ToUri());

        server.AuthorizationCodeReceived += async (sender, response) =>
        {
            await server.Stop();
            var tokenResponse = await new OAuthClient().RequestToken(new PKCETokenRequest(clientId, response.Code, server.BaseUri, verifier));
            _spotify = new SpotifyClient(tokenResponse.AccessToken);
            Console.WriteLine("\nIngelogd! Je kunt nu je Makey Makey gebruiken.");
        };

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

    // Handmatige generatoren om PKCE errors te voorkomen
    static string GenerateRandomString()
    {
        byte[] randomBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(randomBytes); }
        return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    static string GenerateCodeChallenge(string verifier)
    {
        using var sha256 = SHA256.Create();
        byte[] challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(verifier));
        return Convert.ToBase64String(challengeBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    static async Task SpeelMuziek(string playlistUri)
    {
        try 
        {
            var resumeRequest = new PlayerResumePlaybackRequest { ContextUri = playlistUri };
            await _spotify.Player.ResumePlayback(resumeRequest);
            Console.WriteLine($"Muziek gestart: {playlistUri}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fout: {ex.Message}. Check of Spotify actief is op je Pi.");
        }
    }
}