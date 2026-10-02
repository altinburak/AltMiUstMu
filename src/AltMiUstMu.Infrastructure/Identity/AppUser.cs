using Microsoft.AspNetCore.Identity;

namespace AltMiUstMu.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = "";

    /// <summary>Folded display name (see TextNormalizer.Fold). Unique; used for lookups and uniqueness.</summary>
    public string DisplayNameKey { get; set; } = "";

    /// <summary>Featured "Amerikan Mutfak" host. Holds picks entered by the admin; can never sign in.</summary>
    public bool IsPundit { get; set; }

    public bool IsDisabled { get; set; }

    public DateTime CreatedAt { get; set; }
}

public static class Roles
{
    public const string Admin = "Admin";
}
