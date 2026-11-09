# DiskCleaner

App para Windows que varre seus discos, mostra **para onde está indo o espaço** e sugere **o que pode ser removido**. Você revisa, marca e confirma, e os itens vão para a **Lixeira**, de onde ainda dá para recuperar.

## O que ele faz

- **Mapa de espaço**: uma árvore de pastas ordenada por tamanho, com barra e porcentagem, e ao lado um treemap clicável (cada retângulo é proporcional ao tamanho; clicar numa pasta entra nela).
- **Sugestões de limpeza**, por categoria:

  | Categoria | O que encontra | Padrão |
  |---|---|---|
  | Temporários | `%TEMP%` e `C:\Windows\Temp`, só itens com mais de 24h | marcado |
  | Cache de navegadores | Chrome, Edge, Brave (`Cache`, `Code Cache`, `GPUCache`) e Firefox (`cache2`). Senhas, cookies, histórico e favoritos ficam de fora | marcado |
  | Cache de aplicativos | Discord, Spotify, Teams, VS Code | marcado |
  | Logs e despejos de erro | `CrashDumps`, `WER`, arquivos `*.dmp` | marcado |
  | Lixeira | o que já está na Lixeira | marcado |
  | Arquivos grandes | arquivos a partir de 500 MB | desmarcado |
  | Duplicados | conteúdo idêntico (SHA-256), a partir de 1 MB; a cópia mais antiga é mantida | desmarcado |
  | Downloads antigos | `.exe .msi .iso .zip .rar .7z` em Downloads com mais de 90 dias | desmarcado |
  | Pastas de desenvolvimento | `node_modules`, `bin`/`obj` (só quando há `.csproj`/`.fsproj` na mesma pasta), `.venv`/`venv` (só com `pyvenv.cfg`), caches do npm, pip, NuGet e Gradle | desmarcado |

  Os limites (500 MB e 90 dias) e as regras ativas podem ser mudados em **Configurações**.

## O que ele nunca toca

- a raiz de qualquer disco
- `C:\Windows` e tudo dentro dele, exceto o conteúdo de `C:\Windows\Temp`
- `Program Files`, `Program Files (x86)`, `ProgramData`
- `pagefile.sys`, `swapfile.sys`, `hiberfil.sys`, `System Volume Information`, `$Recycle.Bin`
- a própria pasta do DiskCleaner

Essa lista é checada duas vezes: quando as sugestões são geradas e de novo na hora de limpar. Antes de remover, cada arquivo é conferido outra vez. Se mudou de tamanho ou de data desde a análise, ou se está em uso, ele é pulado.

Se algum item não puder ir para a Lixeira (disco de rede ou caminho muito longo), o app avisa e pede uma confirmação extra antes de excluir de forma permanente.

O app roda **sem administrador**. A limpeza do Windows Update, do WinSxS e do `hiberfil.sys` fica para uma versão futura.

## Baixar

Em **Actions**, abra a execução mais recente do workflow **CI** e baixe o artefato `DiskCleaner-win-x64`, que contém `DiskCleaner.exe`. Não é preciso instalar o .NET.

O Windows SmartScreen pode avisar que o app é de um "editor desconhecido", porque o executável não é assinado. Para abrir, clique em **Mais informações → Executar assim mesmo**.

## Onde ficam os dados

- Configurações: `%LOCALAPPDATA%\DiskCleaner\settings.json`
- Log de cada limpeza: `%LOCALAPPDATA%\DiskCleaner\logs\cleanup-AAAAMMDD-HHMMSS.log`
- Erros inesperados: `%LOCALAPPDATA%\DiskCleaner\logs\error.log`

## Compilar

Requer o .NET 8 SDK.

```bash
dotnet test
```

```bash
dotnet publish src/DiskCleaner.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

O resultado fica em `publish/DiskCleaner.exe`. A compilação também funciona em Linux, mas exige o SDK oficial da Microsoft: o SDK do Ubuntu não traz o suporte a WPF.

## Estrutura

- `src/DiskCleaner.Core`: varredura, árvore de espaço, regras, caminhos protegidos, limpeza e layout do treemap. Não depende de WPF e é coberto por testes.
- `src/DiskCleaner.Core/Platform/Windows`: chamadas específicas do Windows (Lixeira, tamanho real em disco, pasta Downloads).
- `src/DiskCleaner.App`: interface WPF.
- `tests/DiskCleaner.Tests`: testes xUnit.
- `docs/superpowers/`: especificação e plano de implementação.

## Roteiro de teste manual (primeira execução)

1. Abra o `DiskCleaner.exe`, deixe só o `C:` marcado e clique em **Analisar**. A barra de progresso e o mapa devem ir se preenchendo.
2. Na aba **Mapa de espaço**, expanda pastas, clique em retângulos do treemap e use **↑ Subir**.
3. Clique com o botão direito numa pasta e escolha **Abrir no Explorer**.
4. Na aba **Sugestões de limpeza**, confira as categorias e desmarque o que quiser manter.
5. Limpe uma categoria **Segura** pequena (por exemplo, Temporários) e confira:
   - se os itens aparecem na Lixeira do Windows;
   - se o log foi criado em `%LOCALAPPDATA%\DiskCleaner\logs`.
6. Tente **Mandar para a Lixeira** algo dentro de `C:\Program Files`. O app deve recusar.
