using FluentAssertions;
using FormFlow.Data.Models;
using FormFlow.Data.Services;

namespace FormFlow.Data.Tests.Services;

public class SignUpValidatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);

    private static SignUpRequest Valid() => new()
    {
        Name = "Grace Hopper",
        Email = "grace@navy.example",
        Password = "compiler1",
        DateOfBirth = "1980-12-09",
        IntendedUse = "Course feedback surveys.",
        Organization = "Navy Research Lab",
    };

    [Fact]
    public void ACompleteSignUp_IsValid()
    {
        SignUpValidator.Validate(Valid(), Today).Should().BeEmpty();
    }

    [Theory]
    [InlineData("2008-09-29", null)]
    [InlineData("2008-09-30", "You must be at least 18 to sign up.")]
    [InlineData("2026-09-30", "Enter a real date of birth.")]
    [InlineData("1899-12-31", "Enter a real date of birth.")]
    [InlineData("09/12/1980", "Enter your date of birth.")]
    [InlineData("", "Enter your date of birth.")]
    public void DateOfBirth_MustBeAnIsoDate_AtLeast18YearsAgo(string dateOfBirth, string? error)
    {
        var request = Valid();
        request.DateOfBirth = dateOfBirth;

        var errors = SignUpValidator.Validate(request, Today);

        if (error is null)
        {
            errors.Should().BeEmpty();
        }
        else
        {
            errors["dateOfBirth"].Should().Equal(error);
        }
    }

    [Theory]
    [InlineData("", "Enter your email address.")]
    [InlineData("grace", "Enter an email address, like name@example.com.")]
    [InlineData("grace@navy", "Enter an email address, like name@example.com.")]
    public void Email_MustLookLikeAnEmailAddress(string email, string error)
    {
        var request = Valid();
        request.Email = email;

        SignUpValidator.Validate(request, Today)["email"].Should().Equal(error);
    }

    [Theory]
    [InlineData("1234567", "Use at least 8 characters.")]
    [InlineData("", "Use at least 8 characters.")]
    public void Password_NeedsAtLeast8Characters(string password, string error)
    {
        var request = Valid();
        request.Password = password;

        SignUpValidator.Validate(request, Today)["password"].Should().Equal(error);
    }

    [Fact]
    public void TextFields_AreRequired_AndLimitedInLength()
    {
        var request = Valid();
        request.Name = "  ";
        request.IntendedUse = new string('x', SignUpValidator.MaxIntendedUseLength + 1);
        request.Organization = "";

        var errors = SignUpValidator.Validate(request, Today);

        errors["name"].Should().Equal("Enter your name.");
        errors["intendedUse"].Should().Equal($"Use at most {SignUpValidator.MaxIntendedUseLength} characters.");
        errors["organization"].Should().Equal("Enter your university, lab or company.");
    }
}
