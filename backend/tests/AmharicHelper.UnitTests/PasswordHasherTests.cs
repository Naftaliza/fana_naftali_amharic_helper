using AmharicHelper.Infrastructure.Security;
using Xunit;

namespace AmharicHelper.UnitTests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_then_Verify_succeeds_for_correct_password()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("Password1!");
        Assert.True(hasher.Verify("Password1!", hash));
    }

    [Fact]
    public void Verify_fails_for_wrong_password()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("Password1!");
        Assert.False(hasher.Verify("wrong", hash));
    }
}
