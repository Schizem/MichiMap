using MichiMap.Api.Services;
using Xunit;

namespace MichiMap.Tests;

public class MichiganCountyServiceTests
{
    private readonly MichiganCountyService _svc = new();

    [Theory]
    [InlineData("Wayne")]
    [InlineData("Grand Traverse")]
    [InlineData("Keweenaw")]
    [InlineData("wayne")]          // case-insensitive
    [InlineData("MANISTEE")]
    public void Lookup_KnownCounty_ReturnsInfo(string name)
    {
        var info = _svc.Lookup(name);
        Assert.NotNull(info);
        Assert.StartsWith("26", info.Fips);
        Assert.InRange(info.Lat, 41m, 48m);   // Michigan latitude bounds
        Assert.InRange(info.Lng, -91m, -82m); // Michigan longitude bounds
    }

    [Theory]
    [InlineData("Cook")]       // Illinois county
    [InlineData("")]
    [InlineData("Fake County")]
    public void Lookup_UnknownCounty_ReturnsNull(string name)
    {
        Assert.Null(_svc.Lookup(name));
    }

    [Fact]
    public void AllCountyNames_Returns83Counties()
    {
        Assert.Equal(83, _svc.AllCountyNames.Count());
    }

    [Fact]
    public void IsValidCounty_CaseInsensitive()
    {
        Assert.True(_svc.IsValidCounty("Houghton"));
        Assert.True(_svc.IsValidCounty("houghton"));
        Assert.False(_svc.IsValidCounty("NotACounty"));
    }
}
