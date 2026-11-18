using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using HandsLiftedApp.Importer.GoogleSlides;

namespace HandsLiftedApp.Core.Services.GoogleSignIn;

/// <summary>
/// Runs the real Google OAuth flow: loopback listener plus the system browser.
/// Uses a throw-away in-memory store while authorizing so the browser always opens (a valid or revoked
/// token on disk is never silently reused) and a cancelled sign-in can never destroy an existing token.
/// The new token is written to token.json only after the sign-in succeeds.
/// </summary>
public class GoogleSignInRunner : IGoogleSignInRunner
{
    public async Task SignInAsync(string clientId, string clientSecret, Action<string> onAuthUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                Scopes = Main.Scopes,
                DataStore = new NullDataStore(),
            });

            var receiver = new CapturingCodeReceiver(new LocalServerCodeReceiver(), onAuthUrl);
            var credential = await new AuthorizationCodeInstalledApp(flow, receiver)
                .AuthorizeAsync("user", cancellationToken);

            await new FileDataStore("token.json", true)
                .StoreAsync<TokenResponse>("user", credential.Token);
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested
                                   && ex is not OperationCanceledException)
        {
            // A torn-down listener can surface as ObjectDisposedException / HttpListenerException after the
            // user cancelled; a user cancel must never look like a failure.
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
