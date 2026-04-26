using AyvalikBankHA.Api.Domain.Service;
using FluentAssertions;
using Xunit;

namespace AyvalikBankHA.Tests;

public class PasswordValidationServiceTests
{
    private readonly PasswordValidationService _s = new();

    [Fact] public void Accepts_valid() { var act = () => _s.Validate("Valid@123"); act.Should().NotThrow(); }
    [Fact] public void Rejects_short() { var act = () => _s.Validate("Short1!"); act.Should().Throw<ArgumentException>(); }
    [Fact] public void Rejects_no_upper() { var act = () => _s.Validate("nouppercase1!"); act.Should().Throw<ArgumentException>().WithMessage("*uppercase*"); }
    [Fact] public void Rejects_no_digit() { var act = () => _s.Validate("NoDigitHere!"); act.Should().Throw<ArgumentException>().WithMessage("*digit*"); }
    [Fact] public void Rejects_no_special() { var act = () => _s.Validate("NoSpecial123"); act.Should().Throw<ArgumentException>().WithMessage("*special*"); }
}
