# Mapa do codebase — CouchPad Windows

> Resumo operacional para orquestração. Detalhes em `docs/codebase-map.md`.
> Estado: 1.0.24 · atualizado em 2026-11-23.

## Stack

- C# / .NET 10 (`net10.0`; Notifications usa `net10.0-windows10.0.18362.0`).
- Avalonia 12.1.4 + Fluent, bindings compilados; CommunityToolkit.Mvvm 8.4.0.
- xUnit 2.9.3; Inno Setup 6.7.4 para o instalador.
- SDK local fora do repo: `C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe` (10.0.403).
- Versão única em `Directory.Build.props`; licença MIT; sem CI e sem lint separado.

## Estrutura

| Caminho | Papel |
|---|---|
| `src/CouchPad.App` | Shell Avalonia, Views, ViewModels, tema, i18n, updates, bandeja |
| `src/CouchPad.Core` | Contratos, state machine, journal, settings, simulação (sem Win32/Avalonia) |
| `src/CouchPad.Platform.Windows` | Implementações Windows: CCD, Core Audio, XInput, tasks, rescue |
| `src/CouchPad.Rescue` | Exe de rescue/watchdog; o App copia o artefato |
| `src/CouchPad.Notifications.Windows` | Notificação nativa experimental; fora do payload |
| `tests/CouchPad.Core.Tests` | Testes de Core + Platform.Windows com fakes |
| `tests/CouchPad.App.Tests` | Testes de ViewModels, i18n, updates |
| `tools/` | 15 harnesses console manuais de hardware/processo |
| `docs/` | Decisões, specs, roadmap, validação, screenshots, REA |
| `packaging/` | `Build-Installer.ps1`, `CouchPad.iss` |
| `.opencode/` | Agentes, skills `couchpad-*` e este mapa |

Dependências: `App → Core, Platform.Windows` (+ artefatos Rescue/Notifications);
`Platform.Windows → Core`; `Rescue → Platform.Windows`;
`Core.Tests → Core, Platform.Windows`; `App.Tests → App`; `tools/* → Platform.Windows`.

Modo/escala da TV (1.0.21, D082): `Core/Display/DisplayPreference.cs`;
`Platform.Windows/EdidReader.cs`, `EdidModeParser.cs`, `EdidIdentity.cs`,
`PerMonitorDpi.cs`; UI em `App/ViewModels/MainWindowViewModel.DisplayModes.cs`
e testes `*Preference*`/`Edid*`/`PerMonitorDpiTests`.

## Comandos

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"

& $dotnet build CouchPad.slnx -c Debug
& $dotnet build CouchPad.slnx -c Release
& $dotnet test CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --smoke
```

- **Lint:** não existe `dotnet format`, script de lint nem CI. O gate é build
  Debug **e** Release com 0 avisos/0 erros + testes + smoke. `.editorconfig`
  define estilo, sem enforcement automático.
- Instalador: `packaging\Build-Installer.ps1 -Dotnet $dotnet -Iscc <ISCC.exe> -Version X.Y.Z`.
- Flags: `--smoke`, `--simulate`, `--gallery`, `--windows-display`, `--windows-gamepad`,
  `--experimental-windows-notifications`, `--recovery-succeeded`, `--recovery-failed`.

## Convenções

- Namespaces file-scoped iguais ao caminho; campos `_camelCase`; `sealed` por padrão;
  records para modelos; interfaces no Core + `Simulated*`/fake + implementação Windows.
- MVVM com `[ObservableProperty]`/`[RelayCommand]`; `MainWindowViewModel` dividido por
  assunto (`.Hardware`, `.Profiles`, `.Diagnosis`, `.Updates`). DI manual em `App.axaml.cs`.
- Fronteira: `App` sem P/Invoke/COM; `Core` puro; `Platform.Windows` sem regra de produto.
- i18n obrigatório: `Strings.pt-BR.json` + `Strings.en.json` (232 chaves cada),
  `{loc:Tr chave}`; nenhuma string visível hardcoded.
- UI usa tokens de `Themes/Tokens.axaml`; componentes em `Themes/Components.axaml`.
- Fail-closed: journal `pending` → apply → `active`, rollback e `_recoveryUncertain`.
- Testes puros em `tests/`, sem hardware real; nomes descritivos em inglês.

## Gotchas

- O `dotnet` do PATH não tem SDK; use o caminho absoluto acima.
- Buildar só o App em árvore limpa não gera `CouchPad.Rescue.exe`; use a solução.
- `--smoke` exige sessão gráfica interativa; é isolado em `%TEMP%\couchpad-smoke`.
- Estado real em `%LOCALAPPDATA%\CouchPad`; journal `active` bloqueia autostart/rescue.
- Escritas de display/áudio são opt-in e exigem runbook; falha de rollback = parar.
- Instalador exige árvore limpa, Inno 6.7.4 e `-p:CouchPadInstallerPublish=true`.
- Sem admin por padrão; não encerrar processos de terceiros; sem telemetria.
- Codex está descontinuado (D079); vale `HANDOFF.md` + `docs/decisions.md`.
