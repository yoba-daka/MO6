using System.ComponentModel.DataAnnotations;
using MO6.ViewModels;
using MyProject12.ViewModels;
using Xunit;

public class SubscriptionValidationTests
{
    [Theory]
    [InlineData("12345")]
    [InlineData("050000000")]
    [InlineData("05000000000")]
    [InlineData("abc")]
    public void PhoneNumber_InvalidIsraeliMobileNumber_FailsValidation(string phoneNumber)
    {
        var model = BuildSubscription(phoneNumber);

        var errors = Validate(model);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(Subscription.PhoneNumber)));
    }

    [Theory]
    [InlineData("0500000000")]
    [InlineData("0543040260")]
    [InlineData("020000000")]
    public void PhoneNumber_ValidIsraeliPhoneNumber_PassesValidation(string phoneNumber)
    {
        var model = BuildSubscription(phoneNumber);

        var errors = Validate(model);

        Assert.DoesNotContain(errors, error => error.MemberNames.Contains(nameof(Subscription.PhoneNumber)));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("050000000")]
    [InlineData("05000000000")]
    [InlineData("abc")]
    public void RegisterAndSubscribePhoneNumber_InvalidIsraeliPhoneNumber_FailsValidation(string phoneNumber)
    {
        var model = BuildRegisterAndSubscribe(phoneNumber);

        var errors = Validate(model);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(RegisterAndSubscribeViewModel.PhoneNumber)));
    }

    [Theory]
    [InlineData("0500000000")]
    [InlineData("0543040260")]
    [InlineData("020000000")]
    public void RegisterAndSubscribePhoneNumber_ValidIsraeliPhoneNumber_PassesValidation(string phoneNumber)
    {
        var model = BuildRegisterAndSubscribe(phoneNumber);

        var errors = Validate(model);

        Assert.DoesNotContain(errors, error => error.MemberNames.Contains(nameof(RegisterAndSubscribeViewModel.PhoneNumber)));
    }

    private static Subscription BuildSubscription(string phoneNumber) =>
        new()
        {
            FullName = "Test User",
            Email = "test@example.com",
            PhoneNumber = phoneNumber,
            Monthly = true
        };

    private static RegisterAndSubscribeViewModel BuildRegisterAndSubscribe(string phoneNumber) =>
        new()
        {
            Name = "Test User",
            Email = "test@example.com",
            EmailConfirmation = "test@example.com",
            Password = "1234567890",
            ConfirmPassword = "1234567890",
            PhoneNumber = phoneNumber,
            AcceptTerms = true,
            Monthly = true
        };

    private static List<ValidationResult> Validate(object model)
    {
        var errors = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), errors, true);
        return errors;
    }
}
