using AegisVault.Platform;
using Xunit;

namespace AegisVault.Platform.Tests;

public sealed class HotKeyServiceTests
{
    [Fact]
    public void RegisterAfterDisposeIsRejected()
    {
        var service = new HotKeyService();
        service.Dispose();

        Assert.False(service.TryRegister(HotKeyService.ModControl | HotKeyService.ModShift, 0x20));
        Assert.False(service.IsRegistered);
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        var service = new HotKeyService();
        service.Dispose();
        service.Dispose();
    }
}
