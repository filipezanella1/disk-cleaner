using DiskCleaner.Core.Rules;
using DiskCleaner.Tests.Helpers;

namespace DiskCleaner.Tests;

public class RecycleBinRuleTests : RuleTestBase
{
    [Fact]
    public void EmptyBin_NoItem() =>
        Assert.Empty(new RecycleBinRule().Analyze(Array.Empty<SpaceTree>(), Ctx(bin: new FakeRecycleBin { Size = 0 })));

    [Fact]
    public void NonEmpty_OneSafeItem()
    {
        var i = Assert.Single(new RecycleBinRule().Analyze(Array.Empty<SpaceTree>(), Ctx(bin: new FakeRecycleBin { Size = 1234 })));
        Assert.True(i.IsRecycleBin);
        Assert.Equal(1234, i.Size);
        Assert.Equal(Risk.Safe, i.Risk);
        Assert.Equal("Itens na Lixeira", i.Reason);
    }
}
