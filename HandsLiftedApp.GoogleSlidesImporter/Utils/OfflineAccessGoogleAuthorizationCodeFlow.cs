using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandsLiftedApp.Importer.GoogleSlides.Utils {
    public class OfflineAccessGoogleAuthorizationCodeFlow : GoogleAuthorizationCodeFlow {
        public OfflineAccessGoogleAuthorizationCodeFlow(GoogleAuthorizationCodeFlow.Initializer initializer) : base(initializer) { }

        public override AuthorizationCodeRequestUrl CreateAuthorizationCodeRequest(string redirectUri) {
            return new GoogleAuthorizationCodeRequestUrl(new Uri(AuthorizationServerUrl)) {
                ClientId = ClientSecrets.ClientId,
                Scope = string.Join(" ", Scopes),
                RedirectUri = redirectUri,
                AccessType = "offline",
                ApprovalPrompt = "force"
            };
        }
    };
}
