using OnlyWinget.Infrastructure.WindowsUpdate;

namespace OnlyWinget.Tests;

public sealed class WindowsUpdateErrorCodesTests
{
    [Fact]
    public void DescribeReturnsNullForSuccess() =>
        Assert.Null(WindowsUpdateErrorCodes.Describe(0));

    [Fact]
    public void DescribeReturnsFriendlyTextForKnownCode()
    {
        // WU_E_ALL_UPDATES_FAILED
        var message = WindowsUpdateErrorCodes.Describe(unchecked((int)0x80240022));

        Assert.Equal("The operation failed for all the updates. (0x80240022)", message);
    }

    [Fact]
    public void DescribeFallsBackToRawHexForUnknownCode()
    {
        var message = WindowsUpdateErrorCodes.Describe(unchecked((int)0x80240FFF));

        Assert.Equal("HRESULT 0x80240FFF", message);
    }
}
