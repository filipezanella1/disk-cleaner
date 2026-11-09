# DiskCleaner — Design (v1)

Data: 2026-11-09

## Objetivo

App para Windows, de uso pessoal, que varre os discos do computador, mostra
para onde está indo o espaço (mapa de espaço) e sugere o que pode ser removido.
O usuário revisa, marca e confirma; os itens vão para a Lixeira.

**Sucesso na v1:** em uma única execução o usuário vê o mapa de espaço de cada
disco, uma lista de sugestões com o total recuperável por categoria, e consegue
limpar os itens marcados sem jamais tocar em arquivos de sistema protegidos.

## Fora de escopo (v1)

- Limpeza que exige administrador: Windows Update/WinSxS, `hiberfil.sys`,
  pontos de restauração, `Windows.old` (fica para a v2).
- Limpeza automática/agendada.
- Exclusão permanente como padrão (só com confirmação extra, ver Segurança).

## Tecnologia

- C# / .NET 8, interface WPF.
- Entrega: `.exe` autocontido para Windows x64 (`dotnet publish -r win-x64
  --self-contained -p:PublishSingleFile=true`).
- Roda sem administrador (`asInvoker`).

## Arquitetura

Solução `DiskCleaner.sln` com três projetos:

### `DiskCleaner.Core` (biblioteca, `net8.0`)

Sem dependência de WPF; toda a lógica testável.

- **`IFileSystem`**: abstração mínima sobre enumeração de diretórios, atributos
  e tamanho em disco. Implementação real usa `System.IO` + `GetCompressedFileSizeW`
  (Windows) para tamanho alocado; em testes, roda sobre diretórios temporários.
- **`Scanner`**: varre uma raiz em paralelo e constrói a árvore.
  - Não segue reparse points (junctions/symlinks); registra-os com tamanho 0.
  - Arquivos "somente online" (atributo `RecallOnDataAccess`/`Offline`) contam
    tamanho alocado (≈0), não o lógico.
  - Acesso negado: o nó é marcado `AccessDenied` e a varredura continua.
  - Suporta caminhos longos (`\\?\`).
  - Reporta progresso (`IProgress<ScanProgress>`: arquivos, bytes, caminho atual)
    e aceita `CancellationToken`.
- **`SpaceTree` / `SpaceNode`**: nó com nome, caminho, tipo (arquivo/pasta),
  tamanho lógico, tamanho alocado, data de modificação, filhos, flags
  (`AccessDenied`, `ReparsePoint`). Tamanhos de pastas são somados de baixo
  para cima ao fim da varredura (e parcialmente durante, para o mapa ao vivo).
- **Regras (`ICleanupRule`)**: `Analyze(SpaceTree, RuleContext) ->
  IEnumerable<CleanupItem>`. `CleanupItem`: caminho, tamanho, categoria,
  risco (`Safe`/`Review`), motivo, data de modificação na varredura.
- **`ProtectedPaths`**: lista de bloqueio. Qualquer `CleanupItem` cujo caminho
  esteja protegido é descartado pelo `RuleEngine` antes de chegar à interface,
  e o `Cleaner` verifica de novo antes de agir.
- **`RuleEngine`**: executa todas as regras, aplica `ProtectedPaths`, remove
  sobreposições (item contido em outro item já sugerido) e agrupa por categoria.
- **`Cleaner`**: recebe itens marcados e envia para a Lixeira
  (`SHFileOperation`/`IFileOperation` com `FOF_ALLOWUNDO`). Revalida cada item
  (existe, mesmo tamanho/data); itens alterados ou em uso são pulados. Retorna
  `CleanupResult` (removidos, pulados com motivo, bytes liberados). Grava log.

### `DiskCleaner.App` (WPF, `net8.0-windows`)

Só interface (MVVM), chama o Core.

- **Tela inicial**: lista de discos fixos/removíveis (`DriveInfo`) com
  capacidade/livre, caixas de seleção, botão "Analisar".
- **Durante a varredura**: barra de progresso, contador, botão "Cancelar";
  o mapa já vai sendo preenchido.
- **Aba Mapa**:
  - Árvore expansível (virtualizada) ordenada por tamanho decrescente: nome,
    tamanho, % do pai, barra proporcional.
  - Treemap (squarified) ao lado, sincronizado com a seleção; clicar num
    retângulo entra na pasta; navegação "voltar/subir".
  - Menu de contexto: "Abrir no Explorer", "Mandar para a Lixeira" (com
    confirmação; bloqueado para caminhos protegidos).
- **Aba Sugestões**: grupos por categoria com total; itens com caixa de
  seleção (Seguros marcados por padrão, Revisar desmarcados), caminho, tamanho,
  motivo. Botão "Limpar selecionados" → diálogo de confirmação com total e
  quantidade → resultado (liberado, pulados).
- **Configurações** (simples, JSON em `%LOCALAPPDATA%\DiskCleaner\settings.json`):
  limite de "arquivo grande" (padrão 500 MB), idade de "downloads antigos"
  (padrão 90 dias), regras habilitadas.

### `DiskCleaner.Tests` (xUnit, `net8.0`)

Testes do Core sobre árvores de arquivos falsas criadas em diretório temporário
e/ou `SpaceTree` montadas em memória.

## Regras da v1

| Regra | Encontra | Risco |
|---|---|---|
| Temporários | Conteúdo de `%TEMP%` e `C:\Windows\Temp` (o que for legível), apenas arquivos modificados há mais de 24h | Safe |
| Caches de navegador | `Cache`, `Code Cache`, `GPUCache` dos perfis de Chrome, Edge, Brave; `cache2` do Firefox. Nunca `Login Data`, `Cookies`, `Bookmarks`, `History` | Safe |
| Caches de apps | Discord, Spotify, Teams, VS Code (`Cache`, `CachedData`, `GPUCache`) | Safe |
| Logs e dumps | `%LOCALAPPDATA%\CrashDumps`, `*.dmp`, `%LOCALAPPDATA%\Microsoft\Windows\WER` | Safe |
| Lixeira | Tamanho atual (`SHQueryRecycleBin`), ação "Esvaziar" (`SHEmptyRecycleBin`) | Safe |
| Arquivos grandes | Arquivos ≥ limite configurado (padrão 500 MB) | Review |
| Duplicados | Agrupa por tamanho (≥ 1 MB) → hash dos primeiros/últimos 64 KB → SHA-256 completo. Sugere todas as cópias menos a mais antiga | Review |
| Downloads antigos | `.exe .msi .iso .zip .rar .7z` em `Downloads` com mais de 90 dias | Review |
| Pastas de dev | `node_modules`; `bin`/`obj` somente com `.csproj`/`.fsproj` irmão; `.venv`/`venv` com `pyvenv.cfg`; caches `%LOCALAPPDATA%\npm-cache`, `pip\Cache`, `%USERPROFILE%\.nuget\packages`, `.gradle\caches` | Review |

## Segurança

- **Caminhos protegidos** (nunca sugeridos nem removidos): raiz de qualquer
  disco; `C:\Windows` e subpastas (exceto `C:\Windows\Temp`); `Program Files`,
  `Program Files (x86)`, `ProgramData`; `pagefile.sys`, `swapfile.sys`,
  `hiberfil.sys`; `System Volume Information`, `$Recycle.Bin` (gerida só pela
  regra Lixeira); a própria pasta do app. Comparação case-insensitive em
  caminhos normalizados.
- **Sempre Lixeira.** Se o item não couber na Lixeira (disco sem Lixeira ou
  item maior que o limite), o app avisa e exige confirmação extra explícita
  para exclusão permanente.
- **Confirmação** antes de qualquer limpeza (total + quantidade).
- **Revalidação** no momento da limpeza; itens alterados são pulados.
- **Log** de cada item removido/pulado em
  `%LOCALAPPDATA%\DiskCleaner\logs\cleanup-AAAAMMDD-HHMMSS.log`.

## Tratamento de erros

- Acesso negado / arquivo em uso / caminho sumiu: registrado no nó ou no
  resultado, nunca interrompe a varredura ou a limpeza.
- Cancelamento: a árvore parcial continua navegável; sugestões não são geradas
  a partir de varredura incompleta (aviso na interface).
- Exceção inesperada: mensagem na interface + log em
  `%LOCALAPPDATA%\DiskCleaner\logs\error.log`.

## Testes

- Scanner: soma de tamanhos, junction não seguida, acesso negado não aborta,
  cancelamento.
- Cada regra sobre árvores falsas (positivos e negativos, ex.: `bin` sem
  `.csproj` não é sugerido).
- Duplicados: mesmo tamanho com conteúdo diferente não é duplicado; mantém uma
  cópia.
- `ProtectedPaths`: teste dedicado garantindo que nenhum caminho protegido sai
  do `RuleEngine`, e que o `Cleaner` recusa caminhos protegidos.
- `RuleEngine`: remoção de sobreposição.
- Interface: compilada no CI/Linux (`EnableWindowsTargeting`); teste manual no
  Windows pelo usuário.
