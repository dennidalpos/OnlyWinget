using OnlyWinget.Application.Winget;

namespace OnlyWinget.Tests;

public sealed class WingetInputValidatorTests
{
    [Theory]
    [InlineData("valid-source_1.2")]
    [InlineData("My Source")]
    [InlineData("Microsoft.VisualStudioCode")]
    [InlineData("Community-repo.123")]
    public void IsValidReturnsTrueForSafeInputs(string value)
    {
        Assert.True(WingetInputValidator.IsValid(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("evil\" & calc")]
    [InlineData("name; rm -rf /")]
    [InlineData("name|pipe")]
    [InlineData("name`backtick`")]
    [InlineData("name'quote'")]
    [InlineData("name<redirect>")]
    [InlineData("name>redirect")]
    public void IsValidReturnsFalseForInvalidInputs(string? value)
    {
        Assert.False(WingetInputValidator.IsValid(value));
    }

    [Theory]
    [InlineData("valid-source_1.2")]
    [InlineData("My Source")]
    public void ValidateAcceptsSafeCharacters(string value)
    {
        WingetInputValidator.Validate(value, nameof(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("evil\" & calc")]
    [InlineData("name; rm -rf /")]
    [InlineData("name|pipe")]
    [InlineData("name`backtick`")]
    [InlineData("name'quote'")]
    [InlineData("name<redirect>")]
    public void ValidateRejectsInvalidInputs(string? value)
    {
        Assert.ThrowsAny<ArgumentException>(() => WingetInputValidator.Validate(value!, nameof(value)));
    }
}
