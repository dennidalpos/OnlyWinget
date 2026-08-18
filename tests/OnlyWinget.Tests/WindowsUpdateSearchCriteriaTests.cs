using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Infrastructure.WindowsUpdate;

namespace OnlyWinget.Tests;

public sealed class WindowsUpdateSearchCriteriaTests
{
    // Regression test: the native COM scan/install path used to hardcode "IsInstalled=0" (optionally
    // adding Type='Software') and ignored IncludeSoftware/IsHidden entirely, unlike the PowerShell
    // fallback. WindowsUpdateSearchCriteria.Build must now match PowerShellWindowsUpdateService's
    // ApplyOptions logic exactly so both execution paths behave the same way.
    [Fact]
    public void BuildIncludesBothTypesWhenSoftwareAndDriversSelected()
    {
        var criteria = WindowsUpdateSearchCriteria.Build(new WindowsUpdateOptions(IncludeSoftware: true, IncludeDrivers: true));

        Assert.Equal("IsInstalled=0 and IsHidden=0", criteria);
    }

    [Fact]
    public void BuildFiltersToSoftwareOnly()
    {
        var criteria = WindowsUpdateSearchCriteria.Build(new WindowsUpdateOptions(IncludeSoftware: true, IncludeDrivers: false));

        Assert.Equal("IsInstalled=0 and IsHidden=0 and Type='Software'", criteria);
    }

    [Fact]
    public void BuildFiltersToDriversOnly()
    {
        var criteria = WindowsUpdateSearchCriteria.Build(new WindowsUpdateOptions(IncludeSoftware: false, IncludeDrivers: true));

        Assert.Equal("IsInstalled=0 and IsHidden=0 and Type='Driver'", criteria);
    }

    [Fact]
    public void BuildThrowsWhenNeitherTypeSelected()
    {
        Assert.Throws<ArgumentException>(() =>
            WindowsUpdateSearchCriteria.Build(new WindowsUpdateOptions(IncludeSoftware: false, IncludeDrivers: false)));
    }
}
