using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;

namespace HandsLiftedApp.Core.Services.GoogleSignIn;

/// <summary>
/// Wraps another <see cref="ICodeReceiver"/> so the Google authorization URL can be shown to the user
/// (the inner receiver opens the browser itself), and so cancellation is honoured promptly even when
/// the inner receiver's listener ignores the token.
/// </summary>
public class CapturingCodeReceiver : ICodeReceiver
{
    private readonly ICodeReceiver _inner;
    private readonly Action<string> _onAuthUrl;

    public CapturingCodeReceiver(ICodeReceiver inner, Action<string> onAuthUrl)
    {
        _inner = inner;
        _onAuthUrl = onAuthUrl;
    }

    public string RedirectUri => _inner.RedirectUri;

    public Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl url,
        CancellationToken taskCancellationToken)
    {
        _onAuthUrl(url.Build().AbsoluteUri);

        // WaitAsync guarantees we return promptly on cancel even if the library's listener lingers.
        return _inner.ReceiveCodeAsync(url, taskCancellationToken).WaitAsync(taskCancellationToken);
    }
}
