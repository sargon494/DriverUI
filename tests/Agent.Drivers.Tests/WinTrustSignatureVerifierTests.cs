using Agent.Drivers.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Agent.Drivers.Tests;

public sealed class WinTrustSignatureVerifierTests
{
    private readonly WinTrustSignatureVerifier _verifier =
        new(NullLogger<WinTrustSignatureVerifier>.Instance);

    [Fact]
    public async Task VerifyAsync_Should_Return_Invalid_When_File_Does_Not_Exist()
    {
        var result = await _verifier.VerifyAsync(@"C:\NonExistent_Path_12345\nonexistent.dll");

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Contain("no existe");
    }

    [Fact]
    public async Task VerifyAsync_Should_Throw_When_FilePath_Is_Null_Or_Empty()
    {
        var act1 = () => _verifier.VerifyAsync(null!);
        var act2 = () => _verifier.VerifyAsync("   ");

        await act1.Should().ThrowAsync<ArgumentException>();
        await act2.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task VerifyAsync_Should_Return_Invalid_For_Unsigned_File()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "not a valid pe binary with signature");

            var result = await _verifier.VerifyAsync(tempFile);

            result.IsValid.Should().BeFalse();
            result.FailureReason.Should().NotBeNullOrEmpty();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task VerifyAsync_Should_Return_Valid_For_Signed_Windows_System_Binary()
    {
        var systemBinary = Path.Combine(Environment.SystemDirectory, "kernel32.dll");

        if (!File.Exists(systemBinary))
        {
            return;
        }

        var result = await _verifier.VerifyAsync(systemBinary);

        result.IsValid.Should().BeTrue();
        result.SignerName.Should().NotBeNullOrEmpty();
        result.IsWhql.Should().BeTrue();
        result.FailureReason.Should().BeNull();
    }
}
