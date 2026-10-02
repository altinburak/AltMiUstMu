using Microsoft.AspNetCore.Identity;

namespace AltMiUstMu.Web.Identity;

/// <summary>Turkish versions of every ASP.NET Core Identity error message.</summary>
public sealed class TurkishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(nameof(DefaultError), "Bilinmeyen bir hata oluştu.");
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "Kayıt başka bir işlem tarafından değiştirildi, lütfen tekrar dene.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "Şifre yanlış.");
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "Bağlantı geçersiz veya süresi dolmuş.");
    public override IdentityError RecoveryCodeRedemptionFailed() => Error(nameof(RecoveryCodeRedemptionFailed), "Kurtarma kodu kullanılamadı.");
    public override IdentityError LoginAlreadyAssociated() => Error(nameof(LoginAlreadyAssociated), "Bu giriş yöntemi zaten başka bir hesaba bağlı.");
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), $"'{userName}' geçerli bir kullanıcı adı değil.");
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), $"'{email}' geçerli bir e-posta adresi değil.");
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), "Bu e-posta adresiyle zaten bir hesap var.");
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), "Bu e-posta adresiyle zaten bir hesap var.");
    public override IdentityError InvalidRoleName(string? role) => Error(nameof(InvalidRoleName), $"'{role}' geçerli bir rol adı değil.");
    public override IdentityError DuplicateRoleName(string role) => Error(nameof(DuplicateRoleName), $"'{role}' rolü zaten var.");
    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword), "Bu kullanıcının zaten bir şifresi var.");
    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled), "Bu kullanıcı için hesap kilitleme açık değil.");
    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole), $"Kullanıcı zaten '{role}' rolünde.");
    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole), $"Kullanıcı '{role}' rolünde değil.");
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"Şifre en az {length} karakter olmalı.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"Şifre en az {uniqueChars} farklı karakter içermeli.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "Şifre en az bir özel karakter içermeli.");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "Şifre en az bir rakam ('0'-'9') içermeli.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "Şifre en az bir küçük harf ('a'-'z') içermeli.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "Şifre en az bir büyük harf ('A'-'Z') içermeli.");

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
