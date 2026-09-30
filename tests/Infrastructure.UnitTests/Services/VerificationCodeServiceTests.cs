using Infrastructure.Services;
using LibraryApi.Domain.Constants;

namespace Infrastructure.UnitTests.Services;

public class VerificationCodeServiceTests
{
    private readonly VerificationCodeService _codes = new();

    [Fact]
    public void Generates_a_code_of_the_configured_length()
    {
        Assert.Equal(PasswordResetRules.CodeLength, _codes.Generate().Length);
    }

    [Fact]
    public void Generates_only_characters_from_the_alphabet()
    {
        for (var i = 0; i < 200; i++)
        {
            Assert.All(
                _codes.Generate(),
                c => Assert.Contains(c, PasswordResetRules.CodeAlphabet));
        }
    }

    // Not a randomness test, which a unit test cannot do. It catches the
    // failure that matters: a generator that returns a constant, or one seeded
    // per call so every code in the same tick is identical.
    [Fact]
    public void Does_not_hand_out_the_same_code_twice_in_a_row()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => _codes.Generate()).ToList();

        Assert.True(codes.Distinct().Count() > 490);
    }

    [Fact]
    public void Verifies_a_code_against_its_own_hash()
    {
        var code = _codes.Generate();

        Assert.True(_codes.Verify(code, _codes.Hash(code)));
    }

    [Fact]
    public void Refuses_a_different_code()
    {
        Assert.False(_codes.Verify("AAAAAAA", _codes.Hash("BBBBBBB")));
    }

    // A phone keyboard capitalising the first letter, or a user typing it in
    // caps, must not cost them one of five attempts.
    [Theory]
    [InlineData("VK35oeQ")]
    [InlineData("vk35oeq")]
    [InlineData("VK35OEQ")]
    [InlineData("  VK35oeQ  ")]
    public void Ignores_case_and_surrounding_space(string typed)
    {
        Assert.True(_codes.Verify(typed, _codes.Hash("VK35oeQ")));
    }

    [Fact]
    public void Hash_does_not_contain_the_code()
    {
        var hash = _codes.Hash("VK35oeQ");

        Assert.DoesNotContain("VK35oeQ", hash);
        Assert.DoesNotContain("VK35OEQ", hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuses_an_empty_code(string code)
    {
        Assert.False(_codes.Verify(code, _codes.Hash("VK35oeQ")));
    }

    [Fact]
    public void Refuses_an_empty_hash()
    {
        Assert.False(_codes.Verify("VK35oeQ", ""));
    }
}
