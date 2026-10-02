namespace AltMiUstMu.Web.Infrastructure;

/// <summary>
/// REQUIRE_EMAIL_CONFIRMATION=true turns on email verification and email-based password reset (needs a working
/// RESEND_API_KEY). Off by default: sign-up logs the user in immediately and no email is sent.
/// </summary>
public sealed record AccountSettings(bool RequireEmailConfirmation);
