using System.Security.Claims;
using AltMiUstMu.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AltMiUstMu.Web.Identity;

/// <summary>Adds the public display name to the auth cookie so the layout never needs a DB lookup.</summary>
public sealed class AppClaimsFactory(UserManager<AppUser> users, RoleManager<IdentityRole> roles, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser, IdentityRole>(users, roles, options)
{
    public const string DisplayNameClaim = "altmiustmu:display_name";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(DisplayNameClaim, user.DisplayName));
        return identity;
    }
}

public static class ClaimsExtensions
{
    public static string DisplayName(this ClaimsPrincipal user) =>
        user.FindFirst(AppClaimsFactory.DisplayNameClaim)?.Value ?? user.Identity?.Name ?? "";
}
