using DiskCleaner.Core.Safety;
using DiskCleaner.Tests.Helpers;

namespace DiskCleaner.Tests;

public class ProtectedPathsTests
{
    private static readonly KnownFolders F = TestFolders.Under(TestTrees.Root);
    private static readonly ProtectedPaths P = new(F);
    private static string J(string a, params string[] b) => Path.Combine([a, .. b]);
    private static readonly string DriveRoot = Path.GetPathRoot(TestTrees.Root)!;

    [Fact] public void DriveRoot_IsProtected() => Assert.True(P.IsProtected(DriveRoot));

    [Fact]
    public void WindowsDir_Protected_ButWindowsTempContentsAllowed()
    {
        Assert.True(P.IsProtected(F.WindowsDir));
        Assert.True(P.IsProtected(J(F.WindowsDir, "System32", "x.dll")));
        Assert.True(P.IsProtected(F.WindowsTemp));
        Assert.False(P.IsProtected(J(F.WindowsTemp, "a.tmp")));
        Assert.False(P.IsProtected(J(F.WindowsTemp, "sub", "b.tmp")));
    }

    [Fact]
    public void ProgramFilesAndProgramData_Protected()
    {
        foreach (var d in new[] { F.ProgramFiles, F.ProgramFilesX86, F.ProgramData })
        {
            Assert.True(P.IsProtected(d));
            Assert.True(P.IsProtected(J(d, "Vendor", "app.exe")));
        }
    }

    [Fact]
    public void SystemFiles_Protected()
    {
        foreach (var f in new[] { "pagefile.sys", "swapfile.sys", "hiberfil.sys" })
            Assert.True(P.IsProtected(J(DriveRoot, f)));
        Assert.True(P.IsProtected(J(DriveRoot, "System Volume Information", "x")));
        Assert.True(P.IsProtected(J(TestTrees.Root, "$Recycle.Bin", "S-1", "x")));
        Assert.True(P.IsProtected(J(TestTrees.Root, "System Volume Information")));
    }

    [Fact] public void AppDirectory_Protected() => Assert.True(P.IsProtected(J(F.AppDirectory, "DiskCleaner.exe")));

    [Fact]
    public void UserFiles_NotProtected()
    {
        Assert.False(P.IsProtected(J(F.Downloads, "setup.exe")));
        Assert.False(P.IsProtected(J(F.LocalAppData, "Google", "Chrome", "User Data", "Default", "Cache")));
        Assert.False(P.IsProtected(J(TestTrees.Root, "Program Files Backup", "x"))); // prefix but different segment
    }

    [Fact]
    public void IsProtected_EquivalentSpellings_AreProtected()
    {
        Assert.True(P.IsProtected(F.ProgramFiles.ToUpperInvariant() + Path.DirectorySeparatorChar));
        Assert.True(P.IsProtected(@"\\?\" + F.ProgramData));
        Assert.True(P.IsProtected(J(F.Downloads, "..", "..", "..", "Program Files", "x")));
        Assert.True(P.IsProtected(J(F.WindowsTemp, "..", "System32")));
    }

    [Fact]
    public void UnderAProtectedRoot_EvenInAnotherTree()
    {
        // the root of a scanned non-system drive is also protected
        Assert.True(P.IsProtected(Path.GetPathRoot(F.Downloads)!));
    }
}
