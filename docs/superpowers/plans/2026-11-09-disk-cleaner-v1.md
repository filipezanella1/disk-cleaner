# DiskCleaner v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** App WPF para Windows que varre discos, mostra um mapa de espaço (árvore + treemap) e sugere itens para limpar, enviando os marcados para a Lixeira.

**Architecture:** `DiskCleaner.Core` (net8.0, sem WPF) contém varredura, árvore, regras, proteção de caminhos, limpeza e layout do treemap, tudo atrás de interfaces (`IFileSystem`, `IRecycleBin`) para ser testável em Linux. Implementações Windows (P/Invoke) ficam em `Core/Platform/Windows`. `DiskCleaner.App` (net8.0-windows, WPF/MVVM) só orquestra e exibe.

**Tech Stack:** .NET 8, C# 12, WPF, xUnit, CommunityToolkit.Mvvm, GitHub Actions (windows-latest).

**Spec:** `docs/superpowers/specs/2026-11-09-disk-cleaner-design.md`

## Global Constraints

- .NET 8 (`net8.0` para Core/Tests, `net8.0-windows` + `UseWPF` para App). `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- App roda sem administrador (`app.manifest` com `requestedExecutionLevel level="asInvoker"`).
- Entrega: `dotnet publish src/DiskCleaner.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`.
- Textos da interface em português (pt-BR).
- Defaults: arquivo grande ≥ 500 MB (524_288_000 bytes); downloads antigos > 90 dias; temporários > 24 h; duplicados ≥ 1 MB (1_048_576 bytes); hash parcial = primeiros 64 KB + últimos 64 KB.
- Dados do app em `%LOCALAPPDATA%\DiskCleaner\` (`settings.json`, `logs\cleanup-yyyyMMdd-HHmmss.log`, `logs\error.log`).
- Nada é apagado sem passar por `ProtectedPaths` duas vezes (no `RuleEngine` e no `Cleaner`).
- Comparação de caminhos: `StringComparison.OrdinalIgnoreCase` sobre caminhos normalizados por `PathUtil.Normalize`.
- Sem dependências externas no Core além da BCL.
- Ambiente de desenvolvimento é Linux: App compila com `-p:EnableWindowsTargeting=true`; testes Windows-only usam `if (!OperatingSystem.IsWindows()) return;` e rodam no CI.

## Review Focus

1. **Arquivos/pastas que somem durante a varredura** (temp, caches de navegador): o scanner deve ignorar `FileNotFoundException`/`DirectoryNotFoundException`/`IOException` e continuar. → teste `Scan_EntryVanishes_ContinuesScan` (Task 2).
2. **Caminho protegido escrito de outro jeito** (maiúsculas, barra no fim, prefixo `\\?\`, `..`): deve continuar protegido. → teste `IsProtected_EquivalentSpellings_AreProtected` (Task 3).
3. **Mesmo item sugerido por duas regras ou item dentro de outro** (ex.: `node_modules` dentro de `%TEMP%`, duplicado dentro de cache): aparece uma vez só, sem contar o espaço duas vezes. → teste `Run_NestedAndDuplicatePaths_KeepsOuterOnce` (Task 4).
4. **Arquivo em uso ou alterado entre varredura e limpeza**: pulado com motivo, limpeza continua. → testes `Clean_FileChangedSinceScan_Skipped`, `Clean_RecycleInUse_SkippedAndContinues` (Task 9).
5. **Duplicados que não liberam espaço** (arquivos ilegíveis, mesmo tamanho com conteúdo diferente só no meio): não devem ser sugeridos. → testes `Duplicates_DifferOnlyInMiddle_NotDuplicates`, `Duplicates_UnreadableFile_Excluded` (Task 8).

---

## File Structure

```
DiskCleaner.sln
Directory.Build.props                      # Nullable, TreatWarningsAsErrors, LangVersion
src/DiskCleaner.Core/
  Model/SpaceNode.cs, SpaceTree.cs, NodeKind.cs
  IO/IFileSystem.cs, FsEntry.cs, PhysicalFileSystem.cs, PathUtil.cs
  Scanning/Scanner.cs, ScanProgress.cs
  Safety/KnownFolders.cs, ProtectedPaths.cs
  Rules/ICleanupRule.cs, CleanupItem.cs, RuleContext.cs, RuleEngine.cs, CategoryGroup.cs
  Rules/KnownPathRule.cs, KnownPathRules.cs, DumpFilesRule.cs, RecycleBinRule.cs,
        LargeFilesRule.cs, OldDownloadsRule.cs, DevFoldersRule.cs, DuplicatesRule.cs
  Cleaning/IRecycleBin.cs, Cleaner.cs, CleanupResult.cs, ICleanupLog.cs, FileCleanupLog.cs
  Settings/CleanerSettings.cs, SettingsStore.cs
  Layout/TreemapLayout.cs
  Formatting/SizeFormatter.cs
  Platform/Windows/WindowsFileSystem.cs, ShellRecycleBin.cs, NativeMethods.cs
src/DiskCleaner.App/
  App.xaml(.cs), app.manifest, Services/AppServices.cs
  ViewModels/MainViewModel.cs, DriveViewModel.cs, NodeViewModel.cs, SuggestionsViewModel.cs, SuggestionItemViewModel.cs
  Views/MainWindow.xaml, MapView.xaml, TreemapControl.cs, SuggestionsView.xaml, SettingsWindow.xaml
tests/DiskCleaner.Tests/
  Helpers/TestTrees.cs, TestFolders.cs, TempDir.cs, FakeFileSystem.cs, FakeRecycleBin.cs
  (um arquivo *Tests.cs por classe testada)
.github/workflows/ci.yml
README.md
```

---

### Task 1: Scaffold + modelo da árvore

**Files:**
- Create: `DiskCleaner.sln`, `Directory.Build.props`, `src/DiskCleaner.Core/DiskCleaner.Core.csproj`, `src/DiskCleaner.Core/Model/*.cs`, `src/DiskCleaner.Core/Formatting/SizeFormatter.cs`, `tests/DiskCleaner.Tests/DiskCleaner.Tests.csproj`, `tests/DiskCleaner.Tests/Helpers/TestTrees.cs`
- Test: `tests/DiskCleaner.Tests/SpaceTreeTests.cs`, `SizeFormatterTests.cs`

**Interfaces:**
- Produces:
  - `enum NodeKind { File, Directory }`; `[Flags] enum NodeFlags { None = 0, AccessDenied = 1, ReparsePoint = 2 }`
  - `sealed class SpaceNode`: `string Name`, `string FullPath`, `NodeKind Kind`, `long LogicalSize`, `long AllocatedSize` (pastas: soma dos descendentes), `DateTime LastWriteUtc`, `NodeFlags Flags` (settable), `SpaceNode? Parent`, `IReadOnlyList<SpaceNode> GetChildrenSnapshot()`, `void AddChild(SpaceNode child)`, `SpaceNode? GetChild(string name)` (case-insensitive). Construtores: `SpaceNode.CreateDirectory(string fullPath, DateTime lastWriteUtc)`, `SpaceNode.CreateFile(string fullPath, long logicalSize, long allocatedSize, DateTime lastWriteUtc)`.
  - `sealed class SpaceTree`: `SpaceTree(string rootPath)`, `SpaceNode Root`, `bool IsComplete { get; set; }`, `SpaceNode? Find(string path)`, `IEnumerable<SpaceNode> EnumerateFiles()`, `IEnumerable<SpaceNode> EnumerateAll()`.
  - `static class SizeFormatter { string Format(long bytes) }` (pt-BR, base 1024).
  - Test helper `TestTrees.Build(string root, params (string relPath, long size, DateTime lastWriteUtc)[] files) : SpaceTree` — pastas implícitas, `IsComplete = true`; `TestTrees.Root` = `Path.Combine(Path.GetTempPath(), "dc-fake")`.

- [ ] **Step 1: Instalar SDK e criar solução**

Run: `apt-get install -y dotnet-sdk-8.0 || (curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0)`; depois `dotnet new sln`, `dotnet new classlib -o src/DiskCleaner.Core -f net8.0`, `dotnet new xunit -o tests/DiskCleaner.Tests -f net8.0`, adicionar à sln e referência Tests→Core. `Directory.Build.props` com `Nullable=enable`, `TreatWarningsAsErrors=true`, `LangVersion=12`.
Expected: `dotnet build` sem erros.

- [ ] **Step 2: Testes que falham**

```csharp
[Fact] public void AddChild_PropagatesAllocatedSizeToAncestors() {
    var tree = TestTrees.Build(TestTrees.Root, ("a/b/x.bin", 100, T0), ("a/y.bin", 50, T0), ("z.bin", 7, T0));
    Assert.Equal(157, tree.Root.AllocatedSize);
    Assert.Equal(150, tree.Find(Path.Combine(TestTrees.Root, "a"))!.AllocatedSize);
    Assert.Equal(100, tree.Find(Path.Combine(TestTrees.Root, "a", "b"))!.AllocatedSize);
}
[Fact] public void Find_IsCaseInsensitive_AndNullWhenMissing() {
    var tree = TestTrees.Build(TestTrees.Root, ("Dir/File.txt", 1, T0));
    Assert.NotNull(tree.Find(Path.Combine(TestTrees.Root, "dir", "FILE.TXT")));
    Assert.Null(tree.Find(Path.Combine(TestTrees.Root, "nope")));
}
[Fact] public void EnumerateFiles_ReturnsOnlyFiles() { /* 3 arquivos → Count == 3, todos Kind == File */ }
[Theory, InlineData(0, "0 B"), InlineData(1023, "1023 B"), InlineData(1536, "1,5 KB"), InlineData(524_288_000, "500,0 MB"), InlineData(1_610_612_736, "1,5 GB")]
public void Format_UsesPtBrAndBase1024(long bytes, string expected) => Assert.Equal(expected, SizeFormatter.Format(bytes));
```

- [ ] **Step 3: Rodar e ver falhar** — `dotnet test` → FAIL (tipos inexistentes).

- [ ] **Step 4: Implementar.** `AddChild` usa `lock` na lista de filhos e `Interlocked.Add` em `AllocatedSize`/`LogicalSize` de cada ancestral (para o mapa ao vivo durante varredura paralela). `Find` desce pelos segmentos relativos à raiz usando `GetChild`.

- [ ] **Step 5: Rodar** — `dotnet test` → PASS.

- [ ] **Step 6: Commit** — `git commit -m "feat(core): space tree model and size formatter"`

---

### Task 2: Sistema de arquivos + Scanner

**Files:**
- Create: `src/DiskCleaner.Core/IO/{IFileSystem,FsEntry,PhysicalFileSystem,PathUtil}.cs`, `src/DiskCleaner.Core/Scanning/{Scanner,ScanProgress}.cs`, `tests/.../Helpers/{TempDir,FakeFileSystem}.cs`
- Test: `tests/DiskCleaner.Tests/ScannerTests.cs`, `PathUtilTests.cs`

**Interfaces:**
- Consumes: `SpaceTree`, `SpaceNode` (Task 1).
- Produces:
  - `record FsEntry(string Name, string FullPath, bool IsDirectory, long Length, DateTime LastWriteUtc, FileAttributes Attributes)`
  - `interface IFileSystem { IEnumerable<FsEntry> Enumerate(string directory); long GetAllocatedSize(FsEntry file); FsEntry? GetEntry(string path); Stream OpenRead(string path); void DeletePermanently(string path); }`
  - `class PhysicalFileSystem : IFileSystem` — `virtual long GetAllocatedSize` (retorna 0 se `Attributes` tem `Offline` ou `0x00400000` RecallOnDataAccess; senão `Length`). `Enumerate` usa `DirectoryInfo.EnumerateFileSystemInfos` com `EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false }`.
  - `static class PathUtil { string Normalize(string path); bool IsUnderOrEqual(string path, string root); bool IsUnder(string path, string root); StringComparison Comparison => OrdinalIgnoreCase }` — `Normalize`: remove `\\?\`, `Path.GetFullPath`, remove separadores finais exceto na raiz.
  - `record ScanProgress(long Files, long Bytes, string CurrentPath)`
  - `class Scanner(IFileSystem fs) { Task ScanAsync(SpaceTree tree, IProgress<ScanProgress>? progress, CancellationToken ct, int maxParallelism = 0) }` — `0` = `Environment.ProcessorCount`. Ao terminar sem cancelar: `tree.IsComplete = true`. Ao cancelar: retorna normalmente (sem lançar), `IsComplete = false`.

- [ ] **Step 1: Testes que falham**

```csharp
[Fact] public async Task Scan_RealDirectory_SumsSizes() {
    using var d = new TempDir(); d.File("a/x.bin", 1000); d.File("a/b/y.bin", 24); d.File("z.bin", 1);
    var tree = new SpaceTree(d.Path); await new Scanner(new PhysicalFileSystem()).ScanAsync(tree, null, default);
    Assert.True(tree.IsComplete); Assert.Equal(1025, tree.Root.AllocatedSize); Assert.Equal(3, tree.EnumerateFiles().Count());
}
[Fact] public async Task Scan_Symlink_NotFollowed_MarkedReparse() {
    // d.File("real/big.bin", 5000); Directory.CreateSymbolicLink(d.Path+"/link", d.Path+"/real")
    // Root.AllocatedSize == 5000; node "link" tem Flags.HasFlag(ReparsePoint) e AllocatedSize == 0
}
[Fact] public async Task Scan_AccessDenied_MarksNodeAndContinues() {
    // FakeFileSystem: Enumerate("/r/locked") lança UnauthorizedAccessException; "/r/ok/f" 10 bytes
    // tree.IsComplete; Find("/r/locked").Flags.HasFlag(AccessDenied); Root.AllocatedSize == 10
}
[Fact] public async Task Scan_EntryVanishes_ContinuesScan() {
    // FakeFileSystem: Enumerate("/r/gone") lança DirectoryNotFoundException; "/r/ok/f" 10 bytes → IsComplete, total 10
}
[Fact] public async Task Scan_OfflineFile_CountsZeroAllocated() {
    // FakeFileSystem com arquivo Length 1000, Attributes Offline → LogicalSize 1000, AllocatedSize 0
}
[Fact] public async Task Scan_Cancelled_ReturnsPartialIncompleteTree() {
    // CancellationTokenSource já cancelado → não lança, IsComplete == false
}
[Fact] public async Task Scan_ReportsProgress() { /* Progress<ScanProgress> recebe ≥1 relatório com Files > 0 */ }
[Fact] public void Normalize_StripsLongPrefixAndTrailingSeparator() { /* "\\\\?\\" + p + sep → p ; IsUnderOrEqual case-insensitive; IsUnder("/a/bc","/a/b") == false */ }
```

- [ ] **Step 2: Rodar e ver falhar** — `dotnet test --filter "ScannerTests|PathUtilTests"` → FAIL.

- [ ] **Step 3: Implementar.** Scanner: fila de diretórios processada por `maxParallelism` workers (`Channel<SpaceNode>` ou `Parallel.ForEachAsync` recursivo com contador de pendentes). Entrada com `Attributes.HasFlag(ReparsePoint)` vira nó com flag `ReparsePoint`, tamanho 0, sem descer. Exceções: `UnauthorizedAccessException` → `AccessDenied`; `IOException` (inclui NotFound) → ignora entrada. Progresso no máximo a cada 100 ms.

- [ ] **Step 4: Rodar** — PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(core): parallel scanner over IFileSystem"`

---

### Task 3: KnownFolders + ProtectedPaths

**Files:**
- Create: `src/DiskCleaner.Core/Safety/{KnownFolders,ProtectedPaths}.cs`, `tests/.../Helpers/TestFolders.cs`
- Test: `tests/DiskCleaner.Tests/ProtectedPathsTests.cs`

**Interfaces:**
- Consumes: `PathUtil` (Task 2).
- Produces:
  - `record KnownFolders(string UserTemp, string WindowsDir, string WindowsTemp, string ProgramFiles, string ProgramFilesX86, string ProgramData, string LocalAppData, string RoamingAppData, string UserProfile, string Downloads, string AppDirectory) { static KnownFolders FromEnvironment(); }` — Downloads via `SHGetKnownFolderPath(FOLDERID_Downloads)` no Windows, fallback `UserProfile\Downloads`.
  - `TestFolders.Under(string root) : KnownFolders` — `root/Windows`, `root/Windows/Temp`, `root/Program Files`, `root/Program Files (x86)`, `root/ProgramData`, `root/Users/u`, `.../AppData/Local`, `.../AppData/Roaming`, `.../AppData/Local/Temp`, `.../Downloads`, `root/App`.
  - `class ProtectedPaths(KnownFolders folders) { bool IsProtected(string path) }`

- [ ] **Step 1: Testes que falham**

```csharp
var f = TestFolders.Under(TestTrees.Root); var p = new ProtectedPaths(f);
[Fact] DriveRoot_IsProtected: Assert.True(p.IsProtected(Path.GetPathRoot(TestTrees.Root)!));
[Fact] WindowsDir_Protected_ButWindowsTempContentsAllowed:
    True(IsProtected(f.WindowsDir + "/System32/x.dll")); False(IsProtected(f.WindowsTemp + "/a.tmp")); True(IsProtected(f.WindowsTemp)) // a pasta em si não
[Fact] ProgramFilesAndProgramData_Protected (os três, e subcaminhos)
[Fact] SystemFiles_Protected: pagefile.sys, swapfile.sys, hiberfil.sys na raiz de f.WindowsDir's drive root; "System Volume Information/x" e "$Recycle.Bin/x" em qualquer nível
[Fact] AppDirectory_Protected
[Fact] UserFiles_NotProtected: False(IsProtected(f.Downloads + "/setup.exe"))
[Fact] IsProtected_EquivalentSpellings_AreProtected:
    True(IsProtected(f.ProgramFiles.ToUpperInvariant() + "/")); True(IsProtected(@"\\?\" + f.ProgramData)); True(IsProtected(f.Downloads + "/../../../../Program Files/x"))
```
(Em Linux a raiz é `/` e `pagefile.sys` testa como `Path.Combine(Path.GetPathRoot(root)!, "pagefile.sys")`.)

- [ ] **Step 2: Rodar e ver falhar** → FAIL.
- [ ] **Step 3: Implementar** — normalizar com `PathUtil.Normalize` antes de qualquer comparação. `WindowsTemp` em si é protegida (só o conteúdo é limpável).
- [ ] **Step 4: Rodar** → PASS.
- [ ] **Step 5: Commit** — `git commit -m "feat(core): protected path blocklist"`

---

### Task 4: Modelo de regras + RuleEngine + Settings

**Files:**
- Create: `src/DiskCleaner.Core/Rules/{ICleanupRule,CleanupItem,RuleContext,RuleEngine,CategoryGroup}.cs`, `src/DiskCleaner.Core/Settings/{CleanerSettings,SettingsStore}.cs`, `src/DiskCleaner.Core/Cleaning/IRecycleBin.cs`, `tests/.../Helpers/FakeRecycleBin.cs`
- Test: `tests/DiskCleaner.Tests/RuleEngineTests.cs`, `SettingsStoreTests.cs`

**Interfaces:**
- Consumes: `SpaceTree` (1), `IFileSystem` (2), `KnownFolders`, `ProtectedPaths` (3).
- Produces:
  - `enum Risk { Safe, Review }`
  - `enum CleanupCategory { Temp, BrowserCache, AppCache, LogsDumps, RecycleBin, LargeFiles, Duplicates, OldDownloads, DevFolders }` (ordem = ordem de exibição e de prioridade em conflito)
  - `record CleanupItem(string Path, bool IsDirectory, long Size, long LogicalSizeAtScan, DateTime LastWriteUtcAtScan, CleanupCategory Category, Risk Risk, string Reason)` — `Size` = espaço alocado. Item da Lixeira: `Path = CleanupItem.RecycleBinPath` (`"::RecycleBin"`).
  - `static CleanupItem CleanupItem.FromNode(SpaceNode n, CleanupCategory c, Risk r, string reason)`
  - `enum RecycleOutcome { Recycled, InUse, NotFound, Failed }`
  - `interface IRecycleBin { long QuerySize(); bool CanRecycle(string path); RecycleOutcome Recycle(string path); bool Empty(); }`
  - `record CleanerSettings { long LargeFileThresholdBytes = 524_288_000; int OldDownloadDays = 90; long DuplicateMinBytes = 1_048_576; TimeSpan TempMinAge = 24h; HashSet<CleanupCategory> DisabledCategories = [] }`
  - `class SettingsStore(string filePath) { CleanerSettings Load(); void Save(CleanerSettings s); }` — arquivo ausente ou JSON inválido → defaults.
  - `record RuleContext(KnownFolders Folders, CleanerSettings Settings, IFileSystem FileSystem, IRecycleBin RecycleBin, DateTime NowUtc, CancellationToken CancellationToken)`
  - `interface ICleanupRule { CleanupCategory Category { get; } IEnumerable<CleanupItem> Analyze(IReadOnlyList<SpaceTree> trees, RuleContext ctx); }`
  - `record CategoryGroup(CleanupCategory Category, IReadOnlyList<CleanupItem> Items, long TotalSize)`
  - `class RuleEngine(IEnumerable<ICleanupRule> rules, ProtectedPaths protectedPaths) { IReadOnlyList<CategoryGroup> Run(IReadOnlyList<SpaceTree> trees, RuleContext ctx); }` — passa às regras apenas árvores com `IsComplete`; pula regras em `DisabledCategories`; descarta itens protegidos; remove sobreposição; grupos vazios omitidos; itens ordenados por `Size` desc.

- [ ] **Step 1: Testes que falham** (regras falsas `StubRule(category, params CleanupItem[])`)

```csharp
[Fact] Run_DropsProtectedItems: stub devolve item em f.ProgramFiles+"/x" e em f.Downloads+"/y" → só y sobra
[Fact] Run_NestedAndDuplicatePaths_KeepsOuterOnce:
    Temp devolve dir A (100); DevFolders devolve A/node_modules (60) e A (100) → um único item A, categoria Temp, TotalSize 100
[Fact] Run_SkipsIncompleteTrees: árvore IsComplete=false → regra recebe lista vazia
[Fact] Run_SkipsDisabledCategories
[Fact] Run_GroupsByCategoryInEnumOrder_ItemsBySizeDesc
[Fact] SettingsStore_MissingOrCorrupt_ReturnsDefaults; SettingsStore_RoundTrip
```

- [ ] **Step 2: Rodar e ver falhar** → FAIL.
- [ ] **Step 3: Implementar.** Sobreposição: ordenar candidatos por (categoria, comprimento do caminho); aceitar um item só se nenhum aceito for igual ou ancestral (`PathUtil.IsUnderOrEqual`); depois remover aceitos que fiquem sob um aceito posterior de categoria maior (caso pai vindo de regra de prioridade menor). Item da Lixeira nunca conflita.
- [ ] **Step 4: Rodar** → PASS.
- [ ] **Step 5: Commit** — `git commit -m "feat(core): rule engine, cleanup item model, settings"`

---

### Task 5: Regras de caminhos conhecidos (Temp, caches, logs/dumps, Lixeira)

**Files:**
- Create: `src/DiskCleaner.Core/Rules/{KnownPathRule,KnownPathRules,DumpFilesRule,RecycleBinRule}.cs`
- Test: `tests/DiskCleaner.Tests/KnownPathRulesTests.cs`, `RecycleBinRuleTests.cs`

**Interfaces:**
- Consumes: Task 4 types.
- Produces:
  - `class KnownPathRule(CleanupCategory category, Risk risk, string reason, Func<KnownFolders, IEnumerable<string>> patterns, bool childrenOnly = false, Func<RuleContext, TimeSpan>? minAge = null) : ICleanupRule` — padrões usam `*` como um segmento curinga (ex.: `{LocalAppData}/Google/Chrome/User Data/*/Cache`). `childrenOnly`: sugere cada filho direto da pasta encontrada em vez da pasta (usado em Temp). `minAge`: só sugere nós cujo arquivo mais recente da subárvore é mais velho que isso.
  - `static class KnownPathRules { ICleanupRule Temp; ICleanupRule BrowserCache; ICleanupRule AppCache; ICleanupRule LogsDumpsFolders; }`
  - `class DumpFilesRule : ICleanupRule` — `*.dmp` em qualquer lugar das árvores, categoria `LogsDumps`, Safe.
  - `class RecycleBinRule : ICleanupRule` — um item `RecycleBinPath` com `QuerySize()` se > 0.

Padrões (copiar exatamente da spec):
- Temp (`childrenOnly`, `minAge = TempMinAge`): `{UserTemp}`, `{WindowsTemp}`.
- BrowserCache: `{LocalAppData}/Google/Chrome/User Data/*/{Cache,Code Cache,GPUCache}`, `{LocalAppData}/Microsoft/Edge/User Data/*/{…}`, `{LocalAppData}/BraveSoftware/Brave-Browser/User Data/*/{…}`, `{LocalAppData}/Mozilla/Firefox/Profiles/*/cache2`.
- AppCache: `{RoamingAppData}/discord/{Cache,Code Cache,GPUCache}`, `{LocalAppData}/Spotify/Data`, `{RoamingAppData}/Microsoft/Teams/{Cache,Code Cache,GPUCache}`, `{LocalAppData}/Packages/MSTeams_8wekyb3d8bbwe/LocalCache`, `{RoamingAppData}/Code/{Cache,CachedData,Code Cache,GPUCache}`.
- LogsDumpsFolders: `{LocalAppData}/CrashDumps`, `{LocalAppData}/Microsoft/Windows/WER`.

Reasons (pt-BR): Temp "Arquivo temporário com mais de 24h"; BrowserCache "Cache do navegador (recriado automaticamente)"; AppCache "Cache de aplicativo (recriado automaticamente)"; LogsDumps "Log ou despejo de erro"; RecycleBin "Itens na Lixeira".

- [ ] **Step 1: Testes que falham**

```csharp
[Fact] Temp_SuggestsOldChildren_NotRecentOnes_NotTheFolder (arquivo de 2 dias → sugerido; de 1h → não; a pasta Temp em si → não)
[Fact] Temp_OldFolderWithRecentFileInside_NotSuggested
[Fact] BrowserCache_MatchesAnyProfile_NeverLoginDataOrCookies (perfis "Default" e "Profile 1" → 2× Cache; "Default/Login Data" e "Default/Cookies" nunca aparecem)
[Fact] AppCache_FindsDiscordAndVsCode
[Fact] KnownPath_NotInScannedTrees_ReturnsNothing
[Fact] DumpFiles_FindsDmpAnywhere
[Fact] RecycleBin_EmptyBin_NoItem; RecycleBin_NonEmpty_OneSafeItem (FakeRecycleBin.Size = 1234)
```

- [ ] **Step 2: Rodar e ver falhar** → FAIL.
- [ ] **Step 3: Implementar.** Expandir `{a,b}` e `*` contra a árvore com `tree.Find` + `GetChildrenSnapshot`.
- [ ] **Step 4: Rodar** → PASS.
- [ ] **Step 5: Commit** — `git commit -m "feat(core): known-path cleanup rules"`

---

### Task 6: Arquivos grandes + Downloads antigos

**Files:**
- Create: `src/DiskCleaner.Core/Rules/{LargeFilesRule,OldDownloadsRule}.cs`
- Test: `tests/DiskCleaner.Tests/LargeFilesRuleTests.cs`, `OldDownloadsRuleTests.cs`

**Interfaces:**
- Consumes: Task 4 types. Produces: `LargeFilesRule : ICleanupRule` (categoria `LargeFiles`, Review, reason "Arquivo grande"), `OldDownloadsRule : ICleanupRule` (`OldDownloads`, Review, reason "Instalador/arquivo compactado em Downloads há mais de N dias").

- [ ] **Step 1: Testes que falham**

```csharp
[Fact] LargeFiles_AtThreshold_Included_BelowExcluded (524_288_000 → sim; 524_287_999 → não)
[Fact] LargeFiles_RespectsCustomThreshold
[Fact] OldDownloads_MatchesExtensionsCaseInsensitive_OlderThan90Days (".EXE" 100 dias → sim; ".exe" 10 dias → não; ".pdf" 100 dias → não; subpasta de Downloads → sim)
[Fact] OldDownloads_OutsideDownloads_Ignored
```
Extensões: `.exe .msi .iso .zip .rar .7z`.

- [ ] **Step 2–4:** falhar → implementar → passar.
- [ ] **Step 5: Commit** — `git commit -m "feat(core): large files and old downloads rules"`

---

### Task 7: Pastas de desenvolvimento

**Files:**
- Create: `src/DiskCleaner.Core/Rules/DevFoldersRule.cs`
- Test: `tests/DiskCleaner.Tests/DevFoldersRuleTests.cs`

**Interfaces:** Produces `DevFoldersRule : ICleanupRule` (`DevFolders`, Review). Reasons: "Dependências do Node (reinstaláveis com npm install)", "Saída de compilação .NET", "Ambiente virtual Python", "Cache de pacotes".

- [ ] **Step 1: Testes que falham**

```csharp
[Fact] NodeModules_OnlyOutermostSuggested (proj/node_modules e proj/node_modules/x/node_modules → só o externo)
[Fact] BinObj_WithSiblingCsproj_Suggested; BinObj_WithoutProjectFile_NotSuggested; FsprojAlsoCounts
[Fact] Venv_WithPyvenvCfg_Suggested; Venv_WithoutPyvenvCfg_NotSuggested (".venv" e "venv")
[Fact] PackageCaches_Suggested ({LocalAppData}/npm-cache, {LocalAppData}/pip/Cache, {UserProfile}/.nuget/packages, {UserProfile}/.gradle/caches)
```

- [ ] **Step 2–4:** falhar → implementar (uma travessia; não descer em pasta já sugerida) → passar.
- [ ] **Step 5: Commit** — `git commit -m "feat(core): developer folders rule"`

---

### Task 8: Duplicados

**Files:**
- Create: `src/DiskCleaner.Core/Rules/DuplicatesRule.cs`
- Test: `tests/DiskCleaner.Tests/DuplicatesRuleTests.cs` (usa `TempDir` + `PhysicalFileSystem` reais)

**Interfaces:** Produces `DuplicatesRule : ICleanupRule` (`Duplicates`, Review, reason `$"Cópia de {keptPath}"`).

Algoritmo: arquivos com `LogicalSize >= DuplicateMinBytes`, sem `ReparsePoint`, `AllocatedSize > 0` → agrupar por `LogicalSize` → grupos ≥2 por hash parcial (primeiros 64 KB + últimos 64 KB, SHA-256) → grupos ≥2 por SHA-256 completo → em cada grupo manter o de menor `LastWriteUtc` (empate: caminho mais curto, depois ordinal) e sugerir os demais. Erro de leitura → arquivo fora. Checar `ctx.CancellationToken` entre arquivos.

- [ ] **Step 1: Testes que falham**

```csharp
[Fact] Duplicates_IdenticalFiles_KeepsOldest_SuggestsOthers (3 cópias 2 MB, datas distintas → 2 itens, reason contém o caminho da mais antiga)
[Fact] Duplicates_SameSizeDifferentContent_NotDuplicates
[Fact] Duplicates_DifferOnlyInMiddle_NotDuplicates (3 MB, 1 byte diferente no offset 1.5 MB)
[Fact] Duplicates_BelowMinSize_Ignored
[Fact] Duplicates_UnreadableFile_Excluded (FakeFileSystem: OpenRead lança IOException para uma das 3 cópias → 1 item)
[Fact] Duplicates_AcrossTwoTrees_Detected
```

- [ ] **Step 2–4:** falhar → implementar → passar.
- [ ] **Step 5: Commit** — `git commit -m "feat(core): content-hash duplicate detection"`

---

### Task 9: Cleaner + log

**Files:**
- Create: `src/DiskCleaner.Core/Cleaning/{Cleaner,CleanupResult,ICleanupLog,FileCleanupLog}.cs`
- Test: `tests/DiskCleaner.Tests/CleanerTests.cs`, `FileCleanupLogTests.cs`

**Interfaces:**
- Consumes: `IFileSystem` (2), `ProtectedPaths` (3), `CleanupItem`, `IRecycleBin` (4).
- Produces:
  - `record SkippedItem(CleanupItem Item, string Reason)`; `record CleanupResult(IReadOnlyList<CleanupItem> Removed, IReadOnlyList<SkippedItem> Skipped, long BytesFreed)`
  - `interface ICleanupLog { void Write(CleanupResult result); }`; `class FileCleanupLog(string logDirectory, Func<DateTime> nowLocal) : ICleanupLog` — arquivo `cleanup-yyyyMMdd-HHmmss.log`, linhas `REMOVED\t{Size}\t{Path}` e `SKIPPED\t{Reason}\t{Path}`.
  - `class Cleaner(IFileSystem fs, IRecycleBin bin, ProtectedPaths protectedPaths, ICleanupLog log) { CleanupResult Clean(IReadOnlyList<CleanupItem> items, Func<IReadOnlyList<CleanupItem>, bool> confirmPermanentDelete, CancellationToken ct); }`

Regras de skip (motivos pt-BR exatos): protegido → "Caminho protegido"; não existe → "Não encontrado"; arquivo com `Length` ou `LastWriteUtc` diferente do scan → "Alterado desde a análise"; `RecycleOutcome.InUse` → "Em uso"; `Failed` → "Falha ao mover para a Lixeira"; `!CanRecycle` e usuário recusou → "Não cabe na Lixeira". Pastas: só checa existência. Item `RecycleBinPath` → `bin.Empty()`. `confirmPermanentDelete` é chamado **uma vez**, com todos os itens não recicláveis, e só se houver algum. Cancelamento: itens restantes vão para Skipped "Cancelado".

- [ ] **Step 1: Testes que falham** (FakeFileSystem + FakeRecycleBin)

```csharp
[Fact] Clean_RecyclesValidItems_SumsBytesFreed
[Fact] Clean_ProtectedPath_RefusedEvenIfPassedIn (bin.Recycle nunca chamado)
[Fact] Clean_FileChangedSinceScan_Skipped
[Fact] Clean_Missing_Skipped
[Fact] Clean_RecycleInUse_SkippedAndContinues (1º InUse, 2º Recycled → 1 removido, 1 pulado "Em uso")
[Fact] Clean_Unrecyclable_AsksOnce_DeletesPermanentlyIfConfirmed / SkipsIfDeclined
[Fact] Clean_RecycleBinItem_EmptiesBin
[Fact] Clean_WritesLog
[Fact] FileCleanupLog_WritesExpectedFormat
```

- [ ] **Step 2–4:** falhar → implementar → passar.
- [ ] **Step 5: Commit** — `git commit -m "feat(core): cleaner with revalidation and logging"`

---

### Task 10: Layout do treemap

**Files:**
- Create: `src/DiskCleaner.Core/Layout/TreemapLayout.cs`
- Test: `tests/DiskCleaner.Tests/TreemapLayoutTests.cs`

**Interfaces:** Produces `readonly record struct LayoutRect(double X, double Y, double Width, double Height)`, `record TreemapCell<T>(T Item, LayoutRect Bounds)`, `static class TreemapLayout { IReadOnlyList<TreemapCell<T>> Squarify<T>(IReadOnlyList<T> items, Func<T, double> weight, LayoutRect bounds); }` — algoritmo squarified (Bruls et al.), itens ordenados por peso desc, pesos ≤ 0 omitidos.

- [ ] **Step 1: Testes que falham**

```csharp
[Fact] Squarify_AreasProportionalToWeights (pesos 6,6,4,3,2,2,1 em 600×400 → área_i/área_total == peso_i/soma ± 1e-6)
[Fact] Squarify_CellsInsideBounds_NoOverlap
[Fact] Squarify_ZeroWeights_Omitted; Squarify_Empty_ReturnsEmpty; Squarify_SingleItem_FillsBounds
[Fact] Squarify_AspectRatiosReasonable (exemplo acima: maior razão ≤ 3)
```

- [ ] **Step 2–4:** falhar → implementar → passar.
- [ ] **Step 5: Commit** — `git commit -m "feat(core): squarified treemap layout"`

---

### Task 11: Implementações Windows + CI

**Files:**
- Create: `src/DiskCleaner.Core/Platform/Windows/{NativeMethods,WindowsFileSystem,ShellRecycleBin}.cs`, `.github/workflows/ci.yml`
- Test: `tests/DiskCleaner.Tests/WindowsPlatformTests.cs` (retornam cedo fora do Windows)

**Interfaces:**
- Produces (todas `[SupportedOSPlatform("windows")]`):
  - `class WindowsFileSystem : PhysicalFileSystem` — `GetAllocatedSize` via `GetCompressedFileSizeW` (mantém regra de 0 para offline); `Enumerate` aceita caminhos longos prefixando `\\?\` quando > 240 caracteres.
  - `class ShellRecycleBin : IRecycleBin` — `Recycle`: `SHFileOperationW` `FO_DELETE` com `FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT | FOF_WANTNUKEWARNING`; códigos 0x20 (`ERROR_SHARING_VIOLATION`) e 0x05 → `InUse`, 0x02/0x7C → `NotFound`, outros ≠0 → `Failed`. `CanRecycle`: `false` se `DriveInfo.DriveType` não for `Fixed`/`Removable`. `QuerySize`: `SHQueryRecycleBinW(null)`. `Empty`: `SHEmptyRecycleBinW(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND)`.
  - `KnownFolders.FromEnvironment()` usa `SHGetKnownFolderPath` para Downloads.

- [ ] **Step 1: Testes (Windows-only)**

```csharp
[Fact] WindowsFileSystem_AllocatedSize_IsAtLeastLogicalRoundedForNormalFile
[Fact] ShellRecycleBin_Recycle_TempFile_Recycled_AndFileGone
[Fact] ShellRecycleBin_Recycle_LockedFile_InUse (FileStream aberto com FileShare.None)
```

- [ ] **Step 2: CI** — `ci.yml` em `windows-latest`: `actions/setup-dotnet@v4` (8.0.x), `dotnet test -c Release`, `dotnet publish src/DiskCleaner.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish`, `actions/upload-artifact@v4` com `publish/DiskCleaner.App.exe`. Em Linux local: `dotnet build -p:EnableWindowsTargeting=true` → sem erros; `dotnet test` → PASS (testes Windows retornam cedo).
- [ ] **Step 3: Commit** — `git commit -m "feat(core): windows file system and shell recycle bin; CI"`

---

### Task 12: App — shell, discos e varredura

**Files:**
- Create: `src/DiskCleaner.App/DiskCleaner.App.csproj` (`net8.0-windows`, `UseWPF`, `EnableWindowsTargeting`, `ApplicationManifest=app.manifest`, `AssemblyName=DiskCleaner`, ref `CommunityToolkit.Mvvm`), `App.xaml(.cs)`, `app.manifest`, `Services/AppServices.cs`, `ViewModels/{MainViewModel,DriveViewModel}.cs`, `Views/MainWindow.xaml(.cs)`

**Interfaces:**
- Consumes: `Scanner`, `WindowsFileSystem`, `ShellRecycleBin`, `KnownFolders.FromEnvironment`, `ProtectedPaths`, `RuleEngine`, todas as regras, `SettingsStore`, `SizeFormatter`.
- Produces:
  - `AppServices` — cria e expõe as dependências do Core (composição única); lista padrão de regras na ordem do enum.
  - `DriveViewModel`: `Name`, `Label`, `TotalText`, `FreeText`, `UsedFraction`, `IsSelected`, `RootPath`. Lista de `DriveInfo.GetDrives()` com `IsReady` e tipo `Fixed`/`Removable`.
  - `MainViewModel`: `ObservableCollection<DriveViewModel> Drives`, `IAsyncRelayCommand AnalyzeCommand`, `IRelayCommand CancelCommand`, `bool IsScanning`, `string ProgressText`, `IReadOnlyList<SpaceTree> Trees`, `MapViewModel Map` (Task 13), `SuggestionsViewModel Suggestions` (Task 14), `bool ScanWasCancelled`.
- Comportamento: "Analisar" varre os discos selecionados em sequência (`Task.Run`), progresso em `ProgressText` ("123.456 arquivos · 45,2 GB · C:\Users\..."), mapa atualizado a cada 1 s via `DispatcherTimer` durante a varredura; ao fim, se todas as árvores completas, roda `RuleEngine` em `Task.Run`; se cancelado, banner "Análise cancelada — sugestões não geradas". Exceções não tratadas: `DispatcherUnhandledException` → `MessageBox` + `logs\error.log`.

- [ ] **Step 1: Implementar** shell e `MainWindow` (cabeçalho com discos em cartões com checkbox e barra de uso, botão Analisar/Cancelar, barra de progresso, `TabControl` com "Mapa" e "Sugestões").
- [ ] **Step 2: Verificar** — `dotnet build src/DiskCleaner.App -p:EnableWindowsTargeting=true` → 0 erros/avisos.
- [ ] **Step 3: Commit** — `git commit -m "feat(app): main window, drive selection, scan orchestration"`

---

### Task 13: App — aba Mapa (árvore + treemap)

**Files:**
- Create: `src/DiskCleaner.App/ViewModels/{MapViewModel,NodeViewModel}.cs`, `Views/MapView.xaml(.cs)`, `Views/TreemapControl.cs`

**Interfaces:**
- Consumes: `SpaceTree`, `SpaceNode`, `TreemapLayout`, `SizeFormatter`, `ProtectedPaths`, `Cleaner` (Task 9).
- Produces:
  - `NodeViewModel`: `Node`, `Name`, `SizeText`, `PercentOfParent` (0–1), `IsExpanded`, `ObservableCollection<NodeViewModel> Children` (carregadas sob demanda ao expandir, ordenadas por `AllocatedSize` desc; placeholder para mostrar a seta), `bool IsAccessDenied`, `void Refresh()` (reatualiza tamanhos durante a varredura).
  - `MapViewModel`: `ObservableCollection<NodeViewModel> Roots` (uma por disco), `NodeViewModel? Selected`, `NodeViewModel? TreemapFocus`, comandos `OpenInExplorerCommand` (`explorer.exe /select,"path"`), `RecycleCommand` (confirmação `MessageBox` com tamanho; desabilitado se `ProtectedPaths.IsProtected`; usa `Cleaner` com um `CleanupItem.FromNode(..., Risk.Review, "Removido manualmente")`; remove o nó da árvore na interface ao sucesso), `GoUpCommand`.
  - `TreemapControl : FrameworkElement` — `DependencyProperty Node (SpaceNode)`, desenha em `OnRender` os filhos do nó com `TreemapLayout.Squarify` (cores por tipo: pasta / arquivo / extensão agrupada), rótulo se a célula for > 60×18 px, tooltip com caminho e tamanho; clique em pasta → evento `NodeActivated(SpaceNode)`; seleção sincronizada com a árvore.
- Árvore: `TreeView` com `VirtualizingPanel.IsVirtualizing=True` e `VirtualizationMode=Recycling`; coluna de barra proporcional (`PercentOfParent`), tamanho e %.

- [ ] **Step 1: Implementar** VMs, `MapView` (árvore à esquerda, treemap à direita com `GridSplitter`, barra de navegação com caminho atual e "Subir"), menu de contexto (Abrir no Explorer / Mandar para a Lixeira).
- [ ] **Step 2: Verificar** — build App → 0 erros/avisos.
- [ ] **Step 3: Commit** — `git commit -m "feat(app): space map with tree and treemap"`

---

### Task 14: App — aba Sugestões + limpeza + configurações

**Files:**
- Create: `src/DiskCleaner.App/ViewModels/{SuggestionsViewModel,SuggestionItemViewModel,CategoryViewModel}.cs`, `Views/SuggestionsView.xaml(.cs)`, `Views/SettingsWindow.xaml(.cs)`

**Interfaces:**
- Consumes: `CategoryGroup`, `CleanupItem`, `Cleaner`, `CleanupResult`, `SettingsStore`, `CleanerSettings`.
- Produces:
  - `CategoryViewModel`: `DisplayName` (pt-BR: Temporários, Cache de navegadores, Cache de aplicativos, Logs e despejos de erro, Lixeira, Arquivos grandes, Duplicados, Downloads antigos, Pastas de desenvolvimento), `RiskText` ("Seguro"/"Revisar"), `TotalText`, `bool? IsChecked` (tri-state para itens), `ObservableCollection<SuggestionItemViewModel> Items`.
  - `SuggestionItemViewModel`: `Item`, `IsChecked` (default `Risk == Safe`), `PathText`, `SizeText`, `Reason`, `AgeText`.
  - `SuggestionsViewModel`: `Categories`, `SelectedTotalText`, `IAsyncRelayCommand CleanCommand`.
- Fluxo de limpeza: confirmação "Mover N itens (X) para a Lixeira?" → `Cleaner.Clean` em `Task.Run` com `confirmPermanentDelete` abrindo (via Dispatcher) "Estes N itens (X) não cabem na Lixeira e serão excluídos permanentemente. Continuar?" → diálogo de resultado "Liberado: X. Pulados: N" com lista de pulados e motivo → remove itens limpos da lista e da árvore do mapa.
- `SettingsWindow`: limite de arquivo grande (MB), dias de downloads antigos, checkboxes por categoria; salva via `SettingsStore` e pede nova análise.

- [ ] **Step 1: Implementar** VMs e views.
- [ ] **Step 2: Verificar** — build App → 0 erros/avisos; `dotnet test` → PASS.
- [ ] **Step 3: Commit** — `git commit -m "feat(app): suggestions, cleanup flow, settings"`

---

### Task 15: Publicação + README

**Files:**
- Create: `README.md`

- [ ] **Step 1: Publicar** — `dotnet publish src/DiskCleaner.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableWindowsTargeting=true -o publish` → `publish/DiskCleaner.exe` existe.
- [ ] **Step 2: README** em pt-BR: o que faz, o que nunca toca (lista de protegidos), como baixar o `.exe` do artefato do CI, como compilar, onde ficam logs/configurações, roteiro de teste manual no Windows (analisar C:, navegar no mapa, limpar um item Seguro, conferir Lixeira, conferir log).
- [ ] **Step 3: Commit + push** — `git commit -m "docs: README and publish instructions"`; `git push -u origin main`.
