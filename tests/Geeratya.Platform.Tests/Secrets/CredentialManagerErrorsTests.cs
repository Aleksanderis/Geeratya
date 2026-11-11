using Geeratya.Platform.Secrets;

namespace Geeratya.Platform.Tests.Secrets;

[Trait("Os", "Independent")]
public class CredentialManagerErrorsTests
{
    [Fact]
    public void GivenErrorNotFound_WhenMapping_ThenItIsNotAFailure()
    {
        CredentialManagerErrors.ThrowUnlessNotFound(CredentialManagerErrors.ErrorNotFound, "read");
    }

    [Theory]
    [InlineData(5, "access denied")]
    [InlineData(1312, "no logon session")]
    [InlineData(1, "unexpected failure")]
    public void GivenAnyOtherError_WhenMapping_ThenItThrowsAMessageWithTheActionAndCode(int error, string reason)
    {
        var exception = Assert.Throws<SecretStoreException>(
            () => CredentialManagerErrors.ThrowUnlessNotFound(error, "read"));

        Assert.Contains("could not read the token", exception.Message, StringComparison.Ordinal);
        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
        Assert.Contains($"error {error}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAWriteFailure_WhenThrowing_ThenItThrowsEvenForNotFound()
    {
        Assert.Throws<SecretStoreException>(
            () => CredentialManagerErrors.Throw(CredentialManagerErrors.ErrorNotFound, "store"));
    }
}
