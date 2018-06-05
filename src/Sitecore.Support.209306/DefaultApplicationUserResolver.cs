// © 2017 Sitecore Corporation A/S. All rights reserved. Sitecore® is a registered trademark of Sitecore Corporation A/S.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Sitecore.Diagnostics;
using Sitecore.Owin.Authentication.Collections;
using Sitecore.Owin.Authentication.Configuration;
using Sitecore.Owin.Authentication.Extensions;
using Sitecore.Owin.Authentication.Identity;
using Sitecore.Security.Accounts;
using Sitecore.Sites;
using Sitecore.Owin.Authentication.Services;
using Sitecore.Data;

namespace Sitecore.Support.Owin.Authentication.Services
{
    public class DefaultApplicationUserResolver : Sitecore.Owin.Authentication.Services.ApplicationUserResolver
    {
        private User _user;

        public DefaultApplicationUserResolver(FederatedAuthenticationConfiguration federatedAuthenticationConfiguration, UserAttachResolver userAttachResolver)
        {
            Assert.ArgumentNotNull(federatedAuthenticationConfiguration, nameof(federatedAuthenticationConfiguration));

            this.FederatedAuthenticationConfiguration = federatedAuthenticationConfiguration;
            this.UserAttachResolver = userAttachResolver;
        }

        internal User User
        {
            get { return _user ?? User.Current; }
            set { _user = value; }
        }

        protected FederatedAuthenticationConfiguration FederatedAuthenticationConfiguration { get; }

        protected UserAttachResolver UserAttachResolver { get; }

        protected virtual SiteContext SiteContext => Context.Site;

        [SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "login")]
        public override async Task<ApplicationUserResolverResult> ResolveApplicationUserAsync(UserManager<ApplicationUser> userManager, ExternalLoginInfo loginInfo,
            IOwinContext owinContext)
        {
            ApplicationUser user = userManager.Find(loginInfo.Login);
            IdentityResult identityResult;

            if (user != null)
            {
                identityResult = IdentityResult.Success;
                return new ApplicationUserResolverResult(user, identityResult);
            }

            User currentUser = this.User;

            var buildNewUser = true;

            identityResult = IdentityResult.Success;

            if (currentUser.IsAuthenticated)
            {
                var request = owinContext.Request;

                var userAttachContext = new UserAttachContext
                {
                    OwinContext = owinContext,
                    ReturnUrl = request.PathBase + request.Path + request.QueryString
                };

                var result = this.UserAttachResolver.Resolve(userAttachContext);

                if (result.Status == UserAttachResolverResultStatus.DelayedResolve)
                {
                    return ApplicationUserResolverResult.DelayedResolveResult;
                }

                if (result.Status == UserAttachResolverResultStatus.Attach)
                {
                    user = currentUser.ToApplicationUser();
                    buildNewUser = false;
                }
            }

            if (!currentUser.IsAuthenticated || buildNewUser)
            {
                IdentityProviderDictionary identityProvidersPerSite;
                ExternalUserBuilder externalUserBuilder = null;
                if (this.FederatedAuthenticationConfiguration.IdentityProvidersPerSite.TryGetValue(this.SiteContext.Name, out identityProvidersPerSite))
                {
                    externalUserBuilder = identityProvidersPerSite.ExternalUserBuilder;
                }

                if (externalUserBuilder == null)
                {
                    throw new InvalidOperationException("Cannot find user builder");
                }

                user = externalUserBuilder.BuildUser(userManager, loginInfo);

                identityResult = await userManager.CreateAsync(user);
                if (identityResult.Succeeded)
                {
                    string domainDefaultProfileItemID = user.InnerUser.Domain.DefaultProfileItemID;
                    user.InnerUser.Profile.ProfileItemId = !string.IsNullOrEmpty(domainDefaultProfileItemID) ? domainDefaultProfileItemID : new ID("{AE4C4969-5B7E-4B4E-9042-B2D8701CE214}").ToString();
                    user.InnerUser.Profile.Save();
                }
            }

            if (identityResult.Succeeded)
            {
                identityResult = await userManager.AddLoginAsync(user.Id, loginInfo.Login);
            }

            return new ApplicationUserResolverResult(user, identityResult);
        }
    }
}
