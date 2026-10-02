using Microsoft.AspNetCore.Identity;
using static AltMiUstMu.Web.Localization.Lang;

namespace AltMiUstMu.Web.Identity;

/// <summary>Turkish and English versions of every ASP.NET Core Identity error message (picked per request).</summary>
public sealed class LocalizedIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(nameof(DefaultError), T("Bilinmeyen bir hata oluştu.", "An unknown error occurred."));
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), T("Kayıt başka bir işlem tarafından değiştirildi, lütfen tekrar dene.", "This record was changed by another operation, please try again."));
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), T("Şifre yanlış.", "Incorrect password."));
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), T("Bağlantı geçersiz veya süresi dolmuş.", "This link is invalid or has expired."));
    public override IdentityError RecoveryCodeRedemptionFailed() => Error(nameof(RecoveryCodeRedemptionFailed), T("Kurtarma kodu kullanılamadı.", "The recovery code could not be used."));
    public override IdentityError LoginAlreadyAssociated() => Error(nameof(LoginAlreadyAssociated), T("Bu giriş yöntemi zaten başka bir hesaba bağlı.", "This login is already linked to another account."));
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), T($"'{userName}' geçerli bir kullanıcı adı değil.", $"'{userName}' is not a valid username."));
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), T($"'{email}' geçerli bir e-posta adresi değil.", $"'{email}' is not a valid email address."));
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), T("Bu e-posta adresiyle zaten bir hesap var.", "An account with this email address already exists."));
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), T("Bu e-posta adresiyle zaten bir hesap var.", "An account with this email address already exists."));
    public override IdentityError InvalidRoleName(string? role) => Error(nameof(InvalidRoleName), T($"'{role}' geçerli bir rol adı değil.", $"'{role}' is not a valid role name."));
    public override IdentityError DuplicateRoleName(string role) => Error(nameof(DuplicateRoleName), T($"'{role}' rolü zaten var.", $"The role '{role}' already exists."));
    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword), T("Bu kullanıcının zaten bir şifresi var.", "This user already has a password."));
    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled), T("Bu kullanıcı için hesap kilitleme açık değil.", "Lockout is not enabled for this user."));
    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole), T($"Kullanıcı zaten '{role}' rolünde.", $"The user is already in the '{role}' role."));
    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole), T($"Kullanıcı '{role}' rolünde değil.", $"The user is not in the '{role}' role."));
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), T($"Şifre en az {length} karakter olmalı.", $"Password must be at least {length} characters."));
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), T($"Şifre en az {uniqueChars} farklı karakter içermeli.", $"Password must contain at least {uniqueChars} different characters."));
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), T("Şifre en az bir özel karakter içermeli.", "Password must contain at least one special character."));
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), T("Şifre en az bir rakam ('0'-'9') içermeli.", "Password must contain at least one digit ('0'-'9')."));
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), T("Şifre en az bir küçük harf ('a'-'z') içermeli.", "Password must contain at least one lowercase letter ('a'-'z')."));
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), T("Şifre en az bir büyük harf ('A'-'Z') içermeli.", "Password must contain at least one uppercase letter ('A'-'Z')."));

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
