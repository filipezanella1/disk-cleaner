using DiskCleaner.Core.Rules;
using DiskCleaner.Core.Safety;
using DiskCleaner.Core.Settings;

namespace DiskCleaner.Tests.Helpers;

public abstract class RuleTestBase
{
    protected static readonly KnownFolders F = TestFolders.Under(TestTrees.Root);
    protected static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    protected static RuleContext Ctx(CleanerSettings? s = null, FakeFileSystem? fs = null, FakeRecycleBin? bin = null) =>
        new(F, s ?? new CleanerSettings(), fs ?? new FakeFileSystem(), bin ?? new FakeRecycleBin(), Now, CancellationToken.None);

    /// <summary>Relative path under the fake root, built from a KnownFolders path + segments.</summary>
    protected static string Rel(string absolute) => Path.GetRelativePath(TestTrees.Root, absolute).Replace('\\', '/');

    protected static string J(string a, params string[] b) => Path.Combine([a, .. b]);
}
