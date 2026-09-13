using AegisVault.Core.Services;

namespace AegisVault.Core.Tests;

internal sealed class FakeKeyProtector : IKeyProtector
{
    public string Id => "fake";

    public bool IsAvailable { get; set; } = true;

    public byte[] Protect(ReadOnlySpan<byte> data) => data.ToArray();

    public byte[] Unprotect(ReadOnlySpan<byte> data) => data.ToArray();
}
