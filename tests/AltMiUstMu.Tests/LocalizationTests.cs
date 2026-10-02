using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using AltMiUstMu.Core.Entities;
using AltMiUstMu.Core.Text;
using AltMiUstMu.Web.Helpers;
using AltMiUstMu.Web.Identity;
using AltMiUstMu.Web.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AltMiUstMu.Tests;

/// <summary>Runs the body with the request culture set to Turkish or English, then restores the previous one.</summary>
internal static class InLanguage
{
    public static T Run<T>(string code, Func<T> body)
    {
        var (culture, ui) = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = Lang.Culture(code);
            return body();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (culture, ui);
        }
    }
}

public class LanguageTests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    [InlineData(" en ", "en")]
    [InlineData("tr", "tr")]
    [InlineData("de", "tr")]
    [InlineData("", "tr")]
    [InlineData(null, "tr")]
    public void Unknown_languages_fall_back_to_turkish(string? input, string expected) =>
        Lang.Normalize(input).Should().Be(expected);

    [Fact]
    public void T_picks_the_text_for_the_current_ui_culture()
    {
        InLanguage.Run("tr", () => Lang.T("Giriş yap", "Sign in")).Should().Be("Giriş yap");
        InLanguage.Run("en", () => Lang.T("Giriş yap", "Sign in")).Should().Be("Sign in");
    }

    [Fact]
    public void Service_messages_follow_the_current_language()
    {
        var error = NameRules.ValidateDisplayName("ab")[0];

        InLanguage.Run("tr", error.ToString).Should().Be("Kullanıcı adı 3-20 karakter olmalı.");
        InLanguage.Run("en", error.ToString).Should().Be("Username must be 3-20 characters.");
    }
}

public class CultureProviderTests
{
    private static async Task<string?> Resolve(string? claim, string? cookie)
    {
        var ctx = new DefaultHttpContext();
        if (claim is not null)
        {
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AppClaimsFactory.LanguageClaim, claim)], "test"));
        }

        if (cookie is not null)
        {
            ctx.Request.Headers.Cookie = $"{Lang.CookieName}={cookie}";
        }

        var result = await new UserLanguageCultureProvider().DetermineProviderCultureResult(ctx);
        return result?.UICultures.Single().Value;
    }

    [Fact]
    public async Task Nothing_set_means_the_default_turkish_applies()
    {
        (await Resolve(claim: null, cookie: null)).Should().BeNull();
    }

    [Fact]
    public async Task Anonymous_visitors_use_the_language_cookie()
    {
        (await Resolve(claim: null, cookie: "en")).Should().Be("en-US");
        (await Resolve(claim: null, cookie: "tr")).Should().Be("tr-TR");
    }

    [Fact]
    public async Task The_signed_in_users_saved_language_wins_over_the_cookie()
    {
        (await Resolve(claim: "tr", cookie: "en")).Should().Be("tr-TR");
        (await Resolve(claim: "en", cookie: "tr")).Should().Be("en-US");
    }

    [Fact]
    public async Task Garbage_cookie_values_fall_back_to_turkish()
    {
        (await Resolve(claim: null, cookie: "xx")).Should().Be("tr-TR");
    }
}

public class DisplayLocalizationTests
{
    [Fact]
    public void Numbers_use_a_decimal_comma_in_turkish_and_a_point_in_english()
    {
        InLanguage.Run("tr", () => Display.Line(47.5m)).Should().Be("47,5");
        InLanguage.Run("en", () => Display.Line(47.5m)).Should().Be("47.5");
    }

    [Fact]
    public void Percent_sign_goes_before_the_number_in_turkish_and_after_it_in_english()
    {
        InLanguage.Run("tr", () => Display.Percent(0.64)).Should().Be("%64");
        InLanguage.Run("en", () => Display.Percent(0.64)).Should().Be("64%");
    }

    [Fact]
    public void Sides_are_alt_ust_in_turkish_and_under_over_in_english()
    {
        InLanguage.Run("tr", () => (Display.SideLabel(PickSide.Under), Display.SideLabel(PickSide.Over))).Should().Be(("ALT", "ÜST"));
        InLanguage.Run("en", () => (Display.SideLabel(PickSide.Under), Display.SideLabel(PickSide.Over))).Should().Be(("UNDER", "OVER"));
    }

    [Fact]
    public void Css_values_stay_invariant_in_both_languages()
    {
        InLanguage.Run("tr", () => Display.Css(45.5)).Should().Be("45.5");
        InLanguage.Run("en", () => Display.Css(45.5)).Should().Be("45.5");
    }

    [Fact]
    public void Every_stored_division_name_has_an_english_name()
    {
        var divisions = AltMiUstMu.Core.Catalog.TeamCatalog.Teams.Select(t => t.Division).Distinct().ToList();

        divisions.Should().HaveCount(6);
        InLanguage.Run("en", () => divisions.Select(Display.Division).ToList()).Should().NotIntersectWith(divisions);
    }
}

public class AttributeTextTests
{
    /// <summary>
    /// DataAnnotations strings (ErrorMessage, [Display(Name)]) on the public pages must have an English entry.
    /// Admin pages are Turkish-only and are skipped.
    /// </summary>
    [Fact]
    public void Every_attribute_string_on_public_pages_has_an_english_translation()
    {
        var pageTypes = typeof(Lang).Assembly.GetTypes()
            .Where(t => typeof(PageModel).IsAssignableFrom(t) && !t.Namespace!.EndsWith(".Admin", StringComparison.Ordinal))
            .ToList();
        var types = pageTypes.Concat(pageTypes.SelectMany(t => t.GetNestedTypes())).ToList();

        var strings = types
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(p => p.GetCustomAttributes<ValidationAttribute>().Select(a => a.ErrorMessage)
                .Append(p.GetCustomAttribute<DisplayAttribute>()?.Name))
            .OfType<string>()
            .Distinct()
            .ToList();

        strings.Should().NotBeEmpty();
        strings.Where(s => !AttributeText.English.ContainsKey(s)).Should().BeEmpty("every one needs an entry in AttributeText.English");
    }

    [Fact]
    public void Localizer_returns_turkish_unchanged_and_english_translated()
    {
        var localizer = new AttributeTextLocalizer();

        InLanguage.Run("tr", () => localizer["Şifre zorunlu."].Value).Should().Be("Şifre zorunlu.");
        InLanguage.Run("en", () => localizer["Şifre zorunlu."].Value).Should().Be("Password is required.");
        InLanguage.Run("en", () => localizer["Bilinmeyen metin"].Value).Should().Be("Bilinmeyen metin");
    }
}
