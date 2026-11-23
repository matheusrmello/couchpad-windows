# Mapa do codebase — CouchPad Windows

> Documento de orientação para um agente novo. Todo o conteúdo abaixo deriva de leitura direta dos arquivos do repositório em `C:\Users\Usuario\Documents\couchpad windows` (estado da versão 1.0.21). Nada aqui descreve código que não exista no checkout.

## 1. Visão geral

O CouchPad Windows recria, de forma clean-room e 100% gratuita, o comportamento útil do ShiftPad 2.2.0: ao conectar um gamepad, muda a tela para a TV, roteia o áudio para o HDMI e, opcionalmente, abre um launcher; ao desconectar, restaura tela, áudio e launcher. Inclui journal de recuperação e rescue separado com watchdog. O escopo declara HDR como adiado (`README.md`, `docs/roadmap.md`).

| Item | Valor | Evidência |
|---|---|---|
| UI | Avalonia 12.1.4 (`Avalonia.Desktop`, `Avalonia.Themes.Fluent`) | `src/CouchPad.App/CouchPad.App.csproj` |
| Runtime | .NET 10, `net10.0` (Notifications usa `net10.0-windows10.0.18362.0`) | todos os `.csproj` |
| MVVM | CommunityToolkit.Mvvm 8.4.0 | `src/CouchPad.App/CouchPad.App.csproj` |
| Versão | `1.0.21` | `Directory.Build.props` |
| Identidade | Company/Product `CouchPad`, autor Matheus Mello, licença MIT | `Directory.Build.props`, `LICENSE` |
| Build | `Nullable`, `ImplicitUsings`, `Deterministic`, `LangVersion=latest`, `NeutralLanguage=en` | `Directory.Build.props` |
| Testes | xUnit 2.9.3, `Microsoft.NET.Test.Sdk` 17.14.1 | `tests/*/*.csproj` |
| Instalador | Inno Setup (`packaging/CouchPad.iss`, compilado por `packaging/Build-Installer.ps1`) | script + docs |

## 2. Árvore de pastas

```text
src/           5 projetos de produção (App, Core, Platform.Windows, Notifications.Windows, Rescue)
tests/         2 projetos xUnit (Core.Tests, App.Tests)
docs/          decisões, specs, roadmap, validações, capturas, REA
packaging/     Build-Installer.ps1, Copy-InstallerToDownloads.ps1, CouchPad.iss
tools/         15 harnesses de hardware/processo (console, fora da solução)
.opencode/     agents/ (6) e skills/ (5 pacotes couchpad-*)
analysis/      diretório reservado à REA (`analysis/shiftpad-2.2.0`, vazio no checkout)
.build/        não existe neste repositório; AGENTS.md aponta o SDK local em `C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe`
artifacts/     local e ignorado pelo Git (logs e instaladores por versão)
```

- `docs/agents/codex/` e `docs/handoff-*codex*.md` são históricos (D079).
- `bin/`, `obj/`, `artifacts/`, `.artifacts/` não fazem parte do mapa.

## 3. Projetos da solução `CouchPad.slnx`

| Caminho | Tipo | O que faz | Arquivos/classes principais |
|---|---|---|---|
| `src/CouchPad.App/CouchPad.App.csproj` | WinExe Avalonia (`AssemblyName=CouchPad`) | Shell, tema, i18n, ViewModels, updates, bandeja | `App.axaml(.cs)`, `Program.cs`, `Views/MainWindow.axaml(.cs)`, `ViewModels/MainWindowViewModel*.cs`, `Localization/LocalizationService.cs` |
| `src/CouchPad.Core/CouchPad.Core.csproj` | Biblioteca `net10.0` sem pacotes | Contratos, state machine, modelos, journal, settings, simulação | `Automation/AutomationCoordinator.cs`, `Display/DisplayService.cs`, `Audio/AudioService.cs`, `Settings/AppSettings.cs`, `Storage/AppPaths.cs` |
| `src/CouchPad.Platform.Windows/CouchPad.Platform.Windows.csproj` | Biblioteca `net10.0` | Implementações Windows: CCD, Core Audio, XInput, Task Scheduler, Win32, watchdog | `WindowsDisplayService.cs`, `WindowsAudioService.cs`, `WindowsRescueGuard.cs`, `DisplayTransaction.cs`, `README.md` |
| `src/CouchPad.Notifications.Windows/CouchPad.Notifications.Windows.csproj` | Exe WinAppSDK experimental (`net10.0-windows10.0.18362.0`, `win-x64`) | Contagem regressiva em notificação nativa; opcional e excluída do payload do instalador | `Program.cs` |
| `src/CouchPad.Rescue/CouchPad.Rescue.csproj` | Exe `net10.0` | Processo de rescue separado: startup recovery, watchdog, tarefas de logon, recuperação assistida | `Program.cs` |
| `tests/CouchPad.Core.Tests/CouchPad.Core.Tests.csproj` | xUnit | Testes de Core + Platform.Windows, fakes e processos com backend falso | `AutomationCoordinatorTests.cs` (44 casos), `DisplayTopologySerializationTests.cs` (29), `DisplayPreferenceTests.cs`, `EdidModeParserTests.cs`, `EdidIdentityTests.cs`, `PerMonitorDpiTests.cs`, `UiLocalizationTests.cs` |
| `tests/CouchPad.App.Tests/CouchPad.App.Tests.csproj` | xUnit | Testes de ViewModels, i18n, updates e composição | `MainWindowViewModelDisplayTests.cs` (18), `WindowsDisplayCompositionTests.cs` (8) |

Os `tools/*` são executáveis independentes (`AudioProbe`, `DisplayProbe`, `RescueDryRun`, `RescueProcessTests`, `StartupRecoveryProcessTests`, `DisplayFailureHardware`, `AudioSwitchHardware`, entre outros), todos referenciando `Platform.Windows` e executados manualmente.

## 4. Dependências e fronteira de camadas

```text
App ──► Core
 │       ▲
 ├──► Platform.Windows ──► Core
 ├──► Rescue ──► Platform.Windows        (artefato copiado; ReferenceOutputAssembly=false)
 └──► Notifications.Windows             (artefato copiado; ReferenceOutputAssembly=false)

Core.Tests ──► Core, Platform.Windows
App.Tests  ──► App
tools/*    ──► Platform.Windows
```

| Camada | Pode | Não pode |
|---|---|---|
| `src/CouchPad.App` | Views, ViewModels, tema, i18n, navegação, composição raiz | P/Invoke, COM, `SetDisplayConfig`, Core Audio, Task Scheduler |
| `src/CouchPad.Core` | Modelos, interfaces, simulação, state machine, journal, testes puros | Depender de Avalonia, Win32 ou hardware |
| `src/CouchPad.Platform.Windows` | Implementar interfaces do Core com APIs do Windows | Regra de produto, texto de UI, decisão de fluxo |
| `tests/*` | Testes puros, fakes, journal, state machine | Hardware real, tela, áudio |

Verificação factual: todos os `DllImport` estão em `Platform.Windows` (`WindowsDisplayService` com 4, `WindowsLauncherService` com 9, `WindowsGamepadWatcher` com 3, `WindowsAudioProbe`, `WindowsAudioTestTone`, `DisplayTopologyValidator`); `App` e `Core` não contêm `DllImport`, `LibraryImport` nem `Marshal.`. Exceção conhecida: o csproj do App tem `BuiltInComInteropSupport` (necessário ao Avalonia), sem código COM.

## 5. Módulos por assunto

| Assunto | Arquivos-chave | Notas |
|---|---|---|
| Automação / state machine | `Core/Automation/AutomationCoordinator.cs`, `AutomationState.cs`, `AutomationSnapshot.cs`, `AutomationSettings.cs`, `DelayOptions.cs`, `DelayScheduler.cs` | Estados `Idle`, `WaitingForGamepad`, `Pending`, `Applying`, `Active`, `Error`, `RescuePending`; `SemaphoreSlim` + CTS cancelável |
| Display / CCD | `Core/Display/DisplayService.cs` (`IDisplayService`, `IDisplayWriteControl`, `SimulatedDisplayService`); `Platform.Windows/WindowsDisplayService.cs`, `DisplayTopologyPlanner.cs`, `DisplayTopologyValidator.cs`, `DisplaySnapshotComparer.cs`, `DisplaySnapshotRehydrator.cs`, `DisplayTransaction.cs`, `DisplayMutationLock.cs`, `DisplaySessionOwner.cs` | Modos `TvOnly`, `Extend`, `InternalOnly`; `Mirror` rejeitado (D043); topologia opaca em `DisplayState.Topology` |
| Modo/escala da TV (1.0.21, D082) | `Core/Display/DisplayPreference.cs` (`DisplayPreference`, modo alvo); `Platform.Windows/EdidReader.cs`, `EdidModeParser.cs`, `EdidIdentity.cs`, `PerMonitorDpi.cs`; `App/ViewModels/MainWindowViewModel.DisplayModes.cs`, `DisplayModeOptions.cs` | Modos/refresh lidos do EDID; apply por `ChangeDisplaySettingsEx` com validação e rollback; escala por monitor via `PerMonitorSettings\DpiValue`; leitura nunca escreve |
| Áudio / Core Audio | `Core/Audio/AudioService.cs` (`IAudioService`, `IAudioBaselineService`, `AudioBaseline`); `Platform.Windows/WindowsAudioService.cs`, `WindowsAudioProbe.cs`, `WindowsAudioTestTone.cs`, `AudioRecoveryTransaction.cs`, `GuardedAudioRecovery.cs`, `WindowsAudioRecoveryBackend.cs`, `WindowsAudioRescueGuard.cs` | Baseline versionado dos três papéis (console/multimídia/comunicações); escrita exige transação armada |
| Gamepad / XInput | `Core/Devices/GamepadDevice.cs`, `GamepadButtons.cs`, `GamepadChordDetector.cs`; `Platform.Windows/WindowsGamepadWatcher.cs`, `WindowsGamepadButtonSource.cs` | Polling read-only dos 4 slots; `GamepadChordPresets` (LB+RB+Y padrão, 800 ms) |
| Perfis de ambiente | `Core/Settings/EnvironmentProfile.cs`; `App/ViewModels/MainWindowViewModel.Profiles.cs`, `EnvironmentProfileOption.cs` | `ActiveProfileId` espelhado nos campos de hardware (D076) |
| Journal / rescue | `Core/Automation/SessionJournalEntry.cs`, `JsonSessionJournalStore.cs`, `RescueGuard.cs`; `Platform.Windows/RescueTransactionContext.cs`, `RescueDecisionStore.cs`, `RescueWatchdogLoop.cs`, `RescueLogonTask.cs`, `StartupRecovery.cs`, `StartupRecoveryAttempts.cs`, `StartupLaunchSequence.cs`, `RescueFailureLog.cs`, `WindowsAssistedRecovery.cs`; `src/CouchPad.Rescue/Program.cs` | Journal `pending` antes do apply e `active` depois; `IRescueLease` com `commit/abort/abandon` e `FailureToken` |
| Settings / persistência | `Core/Settings/AppSettings.cs`, `JsonAppSettingsStore.cs`, `Storage/AppPaths.cs` | JSON camelCase, escrita atômica `.tmp` + move, `.bak` de arquivo corrompido, `JsonExtensionData` preserva chaves desconhecidas |
| Localização | `Core/Localization/AppLanguage.cs`; `App/Localization/LocalizationService.cs`, `TrExtension.cs`, `Strings.pt-BR.json`, `Strings.en.json` | 232 chaves planas em cada catálogo |
| Tema / componentes | `App/Themes/Tokens.axaml` (56 chaves `CouchPad.*`), `Components.axaml` (6 `ControlTheme`), `Controls.axaml` (50 `Style`); `App/Controls/Card.cs`, `ActionButton.cs`, `StatusPill.cs`, `StatusRow.cs`, `KeyChip.cs`, `PathPicker.cs` | `ActionButton` com `Variant` e `State` e pseudo-classes |
| Updates | `App/Updates/GitHubUpdateCheck.cs`, `App/ViewModels/MainWindowViewModel.Updates.cs` | Consulta releases públicas do GitHub, cache de 1 dia em `update-cache.json`, `TimeProvider` injetado |
| Launchers | `Core/Launchers/LauncherService.cs`; `Platform.Windows/WindowsLauncherService.cs` | `Steam` e `Hydra`; só fecha processo que ele próprio iniciou |
| Bandeja / autostart | `App/App.axaml.cs` (TrayIcon, menu, minimizar); `Platform.Windows/WindowsStartupRegistration.cs`, `WindowsScheduledStartup.cs`, `WindowsAppInstance.cs`, `WindowsCountdownNotifications.cs`; `App/ViewModels/CountdownNotificationPresenter.cs` | Instância única por perfil; startup só para o próprio executável |

## 6. Padrões de código observados

- **MVVM com source generators**: `MainWindowViewModel` é `partial : ObservableObject` dividido em `MainWindowViewModel.cs`, `.Hardware.cs`, `.Profiles.cs`, `.Diagnosis.cs`, `.Updates.cs`. Há 32 `[ObservableProperty]`, 13 `[RelayCommand]` e 26 `partial void OnXChanged` nos ViewModels (ex.: `MainWindowViewModel.OnUseWindowsGamepadChanged`).
- **Composição/DI manual**: `App.OnFrameworkInitializationCompleted` (`src/CouchPad.App/App.axaml.cs`) escolhe produção x simulação por flags (`--smoke`, `--simulate`, `--gallery`), cria stores, serviços e o `MainWindowViewModel` por construtor com delegates (`discoverTargets`, `configureStartup`, `productionMode`). Não há container de DI.
- **Interfaces no Core + implementação Windows + fake/simulação**: `IDisplayService`/`SimulatedDisplayService`, `IAudioService`/`SimulatedAudioService`, `IGamepadWatcher`/`SimulatedGamepadWatcher`, `IRescueGuard`/`NoopRescueGuard`, `IGamepadButtonSource`/`NoopGamepadButtonSource`.
- **Fail-closed**: o coordenador grava journal `pending` e aborta se a gravação falhar (D037); após qualquer mutação tentada executa `TryRestoreAsync`; perda do watchdog marca `_recoveryUncertain` e bloqueia novas transições (D034/D038); `DisplayTransaction` faz rollback obrigatório; `DisplaySessionOwner` garante posse exclusiva do journal.
- **Tempo e cancelamento**: `TimeProvider` injetável em `AutomationCoordinator` e `GitHubUpdateCheck`; `IDelayScheduler`/`SystemDelayScheduler` para atrasos testáveis; todo método de hardware recebe `CancellationToken` e há `CreateLinkedTokenSource` com o `FailureToken` do lease.
- **Records para modelos**: `AppSettings`, `EnvironmentProfile`, `AutomationSnapshot`, `DisplayState`, `SessionJournalEntry`, `GamepadChord`, `AudioBaseline`.
- **Convenções**: file-scoped namespaces `CouchPad.Core.*`, `CouchPad.App.*`, `CouchPad.Platform.Windows`; campos privados `_camelCase`; `sealed` por padrão; XML docs em português nos contratos (`DisplayService.cs`, `AutomationCoordinator.cs`); erros de plataforma traduzidos para `InvalidOperationException`/`NotSupportedException` com mensagens em inglês.

## 7. Padrões de UI

- XAML declarativo com `x:DataType` e `AvaloniaUseCompiledBindingsByDefault=true`; todo o texto visível vem de `{loc:Tr chave}` (213 ocorrências em `Views/*.axaml`).
- Tokens centralizados em `Themes/Tokens.axaml` (`CouchPad.Background`, `CouchPad.Accent`, `CouchPad.SurfaceRaised`, tipografia, espaçamento), consumidos por `{DynamicResource ...}`.
- Componentes como `TemplatedControl`/`ContentControl` com `ControlTheme` em `Themes/Components.axaml`; variações por classe em `Themes/Controls.axaml` (`TextBlock.cardTitle`, `Button.loading`, `Button.updateNotice`).
- Breakpoints e layout em `Views/MainWindow.axaml.cs`: `WideBreakpoint = 1000`, `MediumBreakpoint = 720`; `ApplyLayout` recria colunas/linhas do `CardsGrid`, posiciona 8 cards e reordena os filhos para alinhar a ordem de Tab à ordem visual.
- Acessibilidade: 60 usos de `AutomationProperties` em `Views/*.axaml`, incluindo `AutomationProperties.Name` e `LiveSetting="Polite"`; foco visível de 2 px, estados com texto além de cor (`docs/ui-spec.md`); checklist em `docs/ui-a11y-checklist.md`.

## 8. Padrões de i18n

- Dois catálogos planos: `src/CouchPad.App/Localization/Strings.pt-BR.json` e `Strings.en.json`, ambos com 232 chaves e cópia `PreserveNewest` para `Localization/`.
- `LocalizationService` (singleton `Lazy`) indexa por chave, faz fallback para inglês, incrementa `Revision` e persiste `Language` em `AppSettings` sem derrubar a UI em falha.
- `TrExtension`/`TrConverter` criam um `Binding` em `Revision`, então a UI se atualiza ao trocar idioma (inclusive bandeja, que reassina `PropertyChanged`).
- Paridade validada por `tests/CouchPad.App.Tests/LocalizationCatalogTests.cs` (mesmas chaves nos dois arquivos, valores não vazios).
- Ausência de texto hardcoded validada por `tests/CouchPad.Core.Tests/UiLocalizationTests.cs`: extrai `{loc:Tr ...}` do XAML, exige as chaves nos dois catálogos e falha em `Text`/`Content`/`Title`/`AutomationProperties.*`/`ToolTip.Tip` literais (D019).

## 9. Build, testes e empacotamento

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"
& $dotnet build CouchPad.slnx -c Debug
& $dotnet test CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --smoke
```

- `--smoke` abre a janela, imprime uma linha (tamanho, idioma, título) e fecha; `--gallery` abre `ComponentGalleryWindow`; `--simulate` força simulação.
- Flags de produção em `App.axaml.cs`/`Program.cs`: `--windows-display`, `--windows-gamepad`, `--experimental-windows-notifications`, `--apply-hardware-after`, `--hardware-applied`, `--recovery-succeeded`, `--recovery-failed`.
- Contagem de testes: **358 testes** (311 em `tests/CouchPad.Core.Tests` e 47 em `tests/CouchPad.App.Tests`) na 1.0.21; o código contém 264 `[Fact]` + 28 `[Theory]` (com 84 `[InlineData]`), sendo 41 `[Fact]` em `tests/CouchPad.App.Tests` e 223 em `tests/CouchPad.Core.Tests`.
- `packaging/Build-Installer.ps1` recebe `-Dotnet`, `-Iscc`, `-Version` (default lido de `Directory.Build.props`) e `-SkipDownloadsCopy`; publica `App` e `Rescue` self-contained `win-x64` com `-p:CouchPadInstallerPublish=true`, copia o rescue para o payload, recusa payload com `notifications`, gera um `CouchPad.InstallCheck.exe` de pré-voo, compila `CouchPad.iss` e imprime o SHA-256 do `CouchPad-Setup-<versão>-win-x64.exe` em `artifacts/installer-<versão>`.
- Versionamento: `Version` única em `Directory.Build.props`; releases por tag `vX.Y.Z` no GitHub (`README.md`, `docs/install-validation.md`).

## 10. Convenções e invariantes

1. 100% gratuito: sem licença, trial, ativação, assinatura paga ou telemetria; nenhum contato com servidor de licença.
2. pt-BR + inglês desde a primeira versão; nenhuma string visível fora dos dois dicionários (D002, D019).
3. UI inspirada na referência visual, sem copiar ícone, texto, imagem, XAML ou código do ShiftPad (D004).
4. REA somente estático: não executar o ShiftPad, não gerar tráfego de ativação, não contornar proteções (D005).
5. Fail-closed: capturar antes, aplicar, verificar e reverter em falha; nunca deixar a máquina sem display utilizável (D008).
6. Sem admin por padrão; dados por usuário em `%LOCALAPPDATA%\CouchPad` (`settings.json`, `state/session.json`, `logs/`) (D015).
7. Sem telemetria; logs e diagnóstico ficam locais.
8. Não encerrar, atualizar ou modificar processos de terceiros.
9. Fronteira de camadas do AGENTS.md: `App` sem P/Invoke/COM, `Core` puro, `Platform.Windows` só implementa interfaces.
10. Publicação: copiar o EXE para Downloads, registrar SHA-256 e validação, commit + tag, release com binários fora do histórico Git.

## 11. Documentos de referência

| Documento | Conteúdo |
|---|---|
| `AGENTS.md` | Regras ativas, missão, invariantes, fronteira de camadas, fases, comandos, definição de pronto, handoff |
| `HANDOFF.md` | Ledger cronológico por versão (1.0.16 → P0), evidências, decisões e pendências |
| `docs/decisions.md` | Decisões registradas até D082 (perfis, diagnóstico, remoção do monitoramento, modo/escala da TV) |
| `docs/ui-spec.md` | Shell, breakpoints, mapa de cards, estados e critérios visuais |
| `docs/roadmap.md` | Estado 1.0.21, modo/escala da TV e publicação pendente (D081, D082) |
| `docs/validation-queue.md` | Fila sequencial de validação e status por fase |
| Documento histórico da fase de monitoramento | Removida na 1.0.20 (D081); conteúdo mantido apenas como registro |
| `docs/install-validation.md` | Validação do instalador, per-user, desinstalação, hashes |
| `docs/rea-shiftpad-2.2.0.md` | REA estática: evidências de CCD/áudio no binário e lacunas |
| `docs/automation-simulation.md`, `docs/p4-runbook.md`, `docs/p5-audio-hdr.md`, `docs/p6-recovery-matrix.md`, `docs/ui-components.md`, `docs/ui-a11y-checklist.md` | Detalhes por fase |
| `.opencode/agents/*.md` | 6 agentes: `orchestrator` (primary), `ui-builder`, `sim-engineer`, `scaffolder`, `reviewer`, `docs-writer` |
| `.opencode/skills/couchpad-*` | `couchpad-ui`, `couchpad-hardware`, `couchpad-rea`, `couchpad-release`, `couchpad-handoff` (cada um com `SKILL.md` + `references/`) |

## 12. Como navegar

1. **Composição e flags** — `src/CouchPad.App/App.axaml.cs`: decide produção x simulação, cria serviços reais, monta o `MainWindowViewModel`, registra bandeja e updates.
2. **Estado da automação** — `src/CouchPad.Core/Automation/AutomationCoordinator.cs` com `AutomationState.cs` e `JsonSessionJournalStore.cs`: fluxo connect → pending journal → apply → active → restore.
3. **P/Invoke e hardware** — `src/CouchPad.Platform.Windows/` (comece por `README.md`, depois `WindowsDisplayService.cs`, `WindowsAudioService.cs`, `WindowsRescueGuard.cs`).
4. **Shell e layout** — `src/CouchPad.App/Views/MainWindow.axaml` + `MainWindow.axaml.cs` (`ApplyLayout`), com `Themes/Tokens.axaml` e `Themes/Components.axaml`.
5. **Rescue** — `src/CouchPad.Rescue/Program.cs` → `Platform.Windows/StartupRecovery.cs`, `RescueDecisionStore.cs`, `RescueWatchdogLoop.cs`.
6. **Contratos e fakes** — `tests/CouchPad.Core.Tests/` (state machine, journal, display, áudio) e `tests/CouchPad.App.Tests/` (i18n, updates); os fakes vivem no próprio Core.
7. **Regras do projeto** — leia `AGENTS.md`, depois `HANDOFF.md` (topo) e `docs/decisions.md` (últimas decisões) antes de alterar qualquer camada.
