using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using HandsLiftedApp.Core.Services.GoogleSignIn;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HandsLiftedApp.Tests.Services;

[TestClass]
public class CapturingCodeReceiverTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed class FakeReceiver : ICodeReceiver
    {
        public readonly TaskCompletionSource<AuthorizationCodeResponseUrl> Result =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AuthorizationCodeRequestUrl? ReceivedUrl;
        public string RedirectUri => "http://localhost:1/authorize/";

        public Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl url,
            CancellationToken taskCancellationToken)
        {
            ReceivedUrl = url;
            return Result.Task;
        }
    }

    private static AuthorizationCodeRequestUrl NewUrl() =>
        new(new Uri("https://accounts.example/auth"))
        {
            ClientId = "id",
            RedirectUri = "http://localhost:1/authorize/",
        };

    [TestMethod]
    public async Task ForwardsRedirectUri_AndReportsTheAuthUrlOnce()
    {
        var inner = new FakeReceiver();
        var captured = new System.Collections.Generic.List<string>();
        var receiver = new CapturingCodeReceiver(inner, captured.Add);
        var url = NewUrl();

        Assert.AreEqual("http://localhost:1/authorize/", receiver.RedirectUri);

        var pending = receiver.ReceiveCodeAsync(url, CancellationToken.None);
        var response = new AuthorizationCodeResponseUrl { Code = "abc" };
        inner.Result.SetResult(response);
        var result = await pending.WaitAsync(Timeout);

        Assert.AreEqual(1, captured.Count);
        Assert.AreEqual(url.Build().AbsoluteUri, captured[0]);
        Assert.AreSame(url, inner.ReceivedUrl);
        Assert.AreSame(response, result);
    }

    [TestMethod]
    public async Task Cancel_ReturnsPromptly_EvenIfInnerReceiverIgnoresCancellation()
    {
        var inner = new FakeReceiver(); // never completes
        var receiver = new CapturingCodeReceiver(inner, _ => { });
        using var cts = new CancellationTokenSource();

        var pending = receiver.ReceiveCodeAsync(NewUrl(), cts.Token);
        cts.Cancel();

        try
        {
            await pending.WaitAsync(Timeout);
            Assert.Fail("Expected the call to be cancelled");
        }
        catch (OperationCanceledException)
        {
            // expected (TaskCanceledException derives from it)
        }
    }
}
