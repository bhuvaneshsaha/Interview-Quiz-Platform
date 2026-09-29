using InterviewQuiz.Access.Authentication;

namespace InterviewQuiz.Access.UnitTests;

public sealed class RefreshTokenHasherTests
{
    [Fact]
    public void Hash_is_stable_and_does_not_equal_raw_token()
    {
        var raw = RefreshTokenHasher.CreateToken();
        var hash = RefreshTokenHasher.Hash(raw);

        Assert.NotEqual(raw, hash);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, RefreshTokenHasher.Hash(raw));
    }

    [Fact]
    public void CreateToken_is_unique()
    {
        var first = RefreshTokenHasher.CreateToken();
        var second = RefreshTokenHasher.CreateToken();
        Assert.NotEqual(first, second);
    }
}
