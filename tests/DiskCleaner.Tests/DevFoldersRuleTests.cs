using DiskCleaner.Core.Rules;
using DiskCleaner.Tests.Helpers;
using static DiskCleaner.Tests.Helpers.TestTrees;

namespace DiskCleaner.Tests;

public class DevFoldersRuleTests : RuleTestBase
{
    private static List<CleanupItem> Run(params (string rel, long size, DateTime lw)[] files) =>
        new DevFoldersRule().Analyze(new[] { Build(Root, files) }, Ctx()).ToList();

    [Fact]
    public void NodeModules_OnlyOutermostSuggested()
    {
        var items = Run(("proj/node_modules/a/index.js", 10, T0), ("proj/node_modules/x/node_modules/b.js", 5, T0), ("proj/src/a.js", 1, T0));
        var i = Assert.Single(items);
        Assert.Equal(P("proj", "node_modules"), i.Path);
        Assert.Equal(15, i.Size);
        Assert.Equal("Dependências do Node (reinstaláveis com npm install)", i.Reason);
        Assert.Equal(Risk.Review, i.Risk);
    }

    [Fact]
    public void BinObj_WithSiblingCsproj_Suggested()
    {
        var items = Run(("app/App.csproj", 1, T0), ("app/bin/Debug/app.dll", 10, T0), ("app/obj/x", 3, T0));
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal("Saída de compilação .NET", i.Reason));
    }

    [Fact]
    public void BinObj_WithoutProjectFile_NotSuggested() => Assert.Empty(Run(("tools/bin/tool.exe", 10, T0), ("tools/obj/x", 1, T0)));

    [Fact]
    public void FsprojAlsoCounts() => Assert.Single(Run(("lib/Lib.fsproj", 1, T0), ("lib/bin/x.dll", 10, T0)));

    [Fact]
    public void Venv_WithPyvenvCfg_Suggested()
    {
        var items = Run(("py/.venv/pyvenv.cfg", 1, T0), ("py/.venv/lib/x.py", 10, T0), ("py2/venv/pyvenv.cfg", 1, T0));
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal("Ambiente virtual Python", i.Reason));
    }

    [Fact]
    public void Venv_WithoutPyvenvCfg_NotSuggested() => Assert.Empty(Run(("py/venv/lib/x.py", 10, T0), ("py/.venv/x", 1, T0)));

    [Fact]
    public void PackageCaches_Suggested()
    {
        var items = Run(
            (Rel(J(F.LocalAppData, "npm-cache", "x")), 1, T0),
            (Rel(J(F.LocalAppData, "pip", "Cache", "x")), 1, T0),
            (Rel(J(F.UserProfile, ".nuget", "packages", "x")), 1, T0),
            (Rel(J(F.UserProfile, ".gradle", "caches", "x")), 1, T0));
        Assert.Equal(4, items.Count);
        Assert.All(items, i => Assert.Equal("Cache de pacotes", i.Reason));
    }
}
