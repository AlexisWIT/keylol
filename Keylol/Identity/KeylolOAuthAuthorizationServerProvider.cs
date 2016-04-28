﻿using System;
using System.Threading.Tasks;
using Keylol.Models;
using Keylol.Models.DAL;
using Keylol.Provider;
using Keylol.Utilities;
using Microsoft.Owin.Security.OAuth;

namespace Keylol.Identity
{
    /// <summary>
    ///     OAuth Authorzation Server 实现
    /// </summary>
    public class KeylolOAuthAuthorizationServerProvider : OAuthAuthorizationServerProvider
    {
        private async Task GrantPasswordCaptcha(OAuthGrantCustomExtensionContext context)
        {
            var geetest = Startup.Container.GetInstance<GeetestProvider>();
            if (!await geetest.ValidateAsync(context.Parameters["geetest_challenge"],
                context.Parameters["geetest_seccode"],
                context.Parameters["geetest_validate"]))
            {
                context.SetError(Errors.InvalidCaptcha);
                return;
            }

            var userManager = Startup.Container.GetInstance<KeylolUserManager>();

            var idCode = context.Parameters["id_code"];
            var email = context.Parameters["email"];
            var userName = context.Parameters["user_name"];
            var password = context.Parameters["password"];
            KeylolUser user;
            if (!string.IsNullOrWhiteSpace(idCode))
            {
                // 识别码登录
                user = await userManager.FindByIdCodeAsync(idCode);
            }
            else if (!string.IsNullOrWhiteSpace(email))
            {
                // 邮箱登录
                user = await userManager.FindByEmailAsync(email);
            }
            else if (!string.IsNullOrWhiteSpace(userName))
            {
                // 用户名登录
                user = await userManager.FindByNameAsync(userName);
            }
            else
            {
                context.SetError(Errors.InvalidIdField);
                return;
            }

            if (user == null)
            {
                context.SetError(Errors.UserNonExistent);
                return;
            }

            if (await userManager.IsLockedOutAsync(user.Id))
            {
                context.SetError(Errors.AccountLockedOut);
                return;
            }
            if (!await userManager.CheckPasswordAsync(user, password))
            {
                await userManager.AccessFailedAsync(user.Id);
                context.SetError(Errors.InvalidPassword);
                return;
            }
            await userManager.ResetAccessFailedCountAsync(user.Id);

            var dbContext = Startup.Container.GetInstance<KeylolDbContext>();
            dbContext.LoginLogs.Add(new LoginLog
            {
                Ip = context.Request.RemoteIpAddress,
                UserId = user.Id
            });
            await dbContext.SaveChangesAsync();
            context.Validated(await userManager.CreateIdentityAsync(user, OAuthDefaults.AuthenticationType));
        }

        private async Task GrantSteamLoginToken(OAuthGrantCustomExtensionContext context)
        {
            var dbContext = Startup.Container.GetInstance<KeylolDbContext>();

            var steamLoginTokenId = context.Parameters["token_id"];
            var token = await dbContext.SteamLoginTokens.FindAsync(steamLoginTokenId);

            if (token?.SteamId == null)
            {
                context.SetError("invalid_token");
                return;
            }

            var userManager = Startup.Container.GetInstance<KeylolUserManager>();
            var user = await userManager.FindBySteamIdAsync(token.SteamId);
            if (user == null)
            {
                context.Rejected();
                return;
            }

            var loginLog = new LoginLog
            {
                Ip = context.Request.RemoteIpAddress,
                UserId = user.Id
            };
            dbContext.LoginLogs.Add(loginLog);
            await dbContext.SaveChangesAsync();
            context.Validated(await userManager.CreateIdentityAsync(user, OAuthDefaults.AuthenticationType));
        }

        private async Task GrantOneTimeToken(OAuthGrantCustomExtensionContext context)
        {
            var tokenProvider = Startup.Container.GetInstance<OneTimeTokenProvider>();
            var token = context.Parameters["token"];
            string userId;
            try
            {
                userId = await tokenProvider.Consume<string>(token);
            }
            catch (Exception)
            {
                context.SetError(Errors.InvalidToken);
                return;
            }
            var userManager = Startup.Container.GetInstance<KeylolUserManager>();
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                context.SetError(Errors.UserNonExistent);
                return;
            }
            var loginLog = new LoginLog
            {
                Ip = context.Request.RemoteIpAddress,
                UserId = user.Id
            };
            var dbContext = Startup.Container.GetInstance<KeylolDbContext>();
            dbContext.LoginLogs.Add(loginLog);
            await dbContext.SaveChangesAsync();
            context.Validated(await userManager.CreateIdentityAsync(user, OAuthDefaults.AuthenticationType));
        }

        /// <summary>
        ///     Called when a request to the Token endpoint arrives with a "grant_type" of any other value. If the application
        ///     supports custom grant types
        ///     it is entirely responsible for determining if the request should result in an access_token. If context.Validated is
        ///     called with ticket
        ///     information the response body is produced in the same way as the other standard grant types. If additional response
        ///     parameters must be
        ///     included they may be added in the final TokenEndpoint call.
        ///     See also http://tools.ietf.org/html/rfc6749#section-4.5
        /// </summary>
        /// <param name="context">The context of the event carries information in and results out.</param>
        /// <returns>
        ///     Task to enable asynchronous execution
        /// </returns>
        public override async Task GrantCustomExtension(OAuthGrantCustomExtensionContext context)
        {
            if (context.GrantType.Equals("password_captcha", StringComparison.OrdinalIgnoreCase))
            {
                await GrantPasswordCaptcha(context);
            }
            else if (context.GrantType.Equals("steam_login_token", StringComparison.OrdinalIgnoreCase))
            {
                await GrantSteamLoginToken(context);
            }
            else if (context.GrantType.Equals("one_time_token", StringComparison.OrdinalIgnoreCase))
            {
                await GrantOneTimeToken(context);
            }
        }

        /// <summary>
        ///     Called to validate that the origin of the request is a registered "client_id", and that the correct credentials for
        ///     that client are
        ///     present on the request. If the web application accepts Basic authentication credentials,
        ///     context.TryGetBasicCredentials(out clientId, out clientSecret) may be called to acquire those values if present in
        ///     the request header. If the web
        ///     application accepts "client_id" and "client_secret" as form encoded POST parameters,
        ///     context.TryGetFormCredentials(out clientId, out clientSecret) may be called to acquire those values if present in
        ///     the request body.
        ///     If context.Validated is not called the request will not proceed further.
        /// </summary>
        /// <param name="context">The context of the event carries information in and results out.</param>
        /// <returns>
        ///     Task to enable asynchronous execution
        /// </returns>
        public override Task ValidateClientAuthentication(OAuthValidateClientAuthenticationContext context)
        {
            string clientId, clientSecret;
            if (context.TryGetBasicCredentials(out clientId, out clientSecret) ||
                context.TryGetFormCredentials(out clientId, out clientSecret))
            {
                // 暂时不限制 Client ID
                context.Validated(clientId);
            }
            return Task.FromResult(0);
        }
    }
}