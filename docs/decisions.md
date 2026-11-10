# CouchPad — Registro de decisões

> Fonte canônica para o projeto. Copiar para `docs/decisions.md` no repositório.
> Novas decisões entram no fim, com data e ID. Não apagar histórico.

## Confirmadas

| ID | Data | Decisão | Motivo |
|---|---|---|---|
| D001 | 2026-11-09 | Avalonia 12.1.4 + .NET 10 (`net10.0`) | Prova Avalonia existente; XAML/MVVM |
| D002 | 2026-11-09 | pt-BR + inglês | Público-alvo e i18n |
| D003 | 2026-11-09 | App 100% free | Sem licença/ativação/trial/telemetria |
| D004 | 2026-11-09 | UI inspirada na referência, não copiada | Direitos autorais e identidade própria |
| D005 | 2026-11-09 | REA somente estático | Segurança e escopo |
| D006 | 2026-11-09 | Escopo: tela, áudio, HDR e rescue | Foco funcional |
| D007 | 2026-11-09 | Serviço simulado primeiro | Evitar risco de hardware |
| D008 | 2026-11-09 | Fail-closed em hardware | Não deixar a máquina sem display |
| D009 | 2026-11-09 | Journal com escrita atômica | Recuperação confiável |
| D010 | 2026-11-09 | Nome **CouchPad**; repo `couchpad-windows`; identidade original | Marca própria e independência |
| D011 | 2026-11-09 | Licença **MIT**, Copyright (c) 2026 Matheus Mello | Consistência com o projeto `couchpad` existente |
| D012 | 2026-11-09 | Windows 10 1903+ e Windows 11, x64; ARM64 via emulação no v1 | Cobertura realista |
| D013 | 2026-11-09 | Distribuição self-contained, x64 | Sem dependência de runtime |
| D014 | 2026-11-09 | Idioma padrão = cultura do sistema; pt-BR + inglês; fallback inglês | Primeira experiência |
| D015 | 2026-11-09 | Config em `%LOCALAPPDATA%\CouchPad\settings.json`; journal em `state\session.json`; logs em `logs\` | Per-user, sem admin |
| D016 | 2026-11-09 | Primeiro uso com automação desligada e configuração mínima guiada | Evitar mudança acidental |
| D017 | 2026-11-09 | Layout do shell reposicionado em code-behind com 3 breakpoints (`≥1000`, `720–999`, `<720`); janela mínima `640×640`; conteúdo rolável | Alcançar os 3 layouts sem duplicar XAML |
| D018 | 2026-11-09 | P1 é somente UI: toggle de automação alterna `Inativo`/`Aguardando gamepad`; refresh de displays é placeholder de 700 ms; nada de CCD/áudio/HDR | Validar o shell sem risco de hardware |
| D019 | 2026-11-09 | Até glifos curtos (chips `1`/`LB`/`RB`/`A` e unidades `1 s`) vêm dos catálogos; teste de XAML bloqueia texto visível literal | Cumprir "nenhuma string fora dos dicionários" |
| D020 | 2026-11-09 | P2: componentes como `ContentControl`/`TemplatedControl` com `ControlTheme` em `Themes/Components.axaml`; shell consome `Card`, `StatusPill`, `StatusRow`, `KeyChip`, `PathPicker` e `ActionButton`; galeria de estados via `--gallery` | Reuso, estados consistentes e validação visual sem hardware |
| D021 | 2026-11-09 | `ActionButton` tem `Variant` (Primary/Secondary) e `State` (Idle/Loading/Success/Error); foco usa borda `TextPrimary` de 2 px; hover/pressed primários usam `AccentMutedHover`/`AccentMutedPressed` | Estados distinguíveis, foco visível e cor nunca como único indicador |
| D022 | 2026-11-09 | Classes `Button.loading` e `ComboBox.loading` definem `IsEnabled=false`; o `ActionButton` não altera `IsEnabled` por si só e o shell desabilita o refresh durante a atualização | Bloquear teclado e ponteiro e evitar duplo acionamento |
| D023 | 2026-11-09 | Seletor de pasta: o ViewModel expõe `ChooseLauncherFolderCommand` e o evento `FolderPickerRequested`; a view usa `StorageProvider.OpenFolderPickerAsync` | Manter `StorageProvider` fora do ViewModel/Core e permitir teste sem diálogo |
| D024 | 2026-11-09 | P3: display, áudio e gamepad simulados atrás de `IDisplayService`, `IAudioService` e `IGamepadWatcher` no Core; atrasos via `IDelayScheduler` injetável | Testar o fluxo completo sem hardware nem espera real |
| D025 | 2026-11-09 | State machine `Idle`, `WaitingForGamepad`, `Pending`, `Applying`, `Active`, `Error` (`RescuePending` reservado); toda transição é cancelável por CTS e serializada por gate | Atrasos cancelam ao desconectar/desligar e não há transição concorrente |
| D026 | 2026-11-09 | `AutomationSnapshot` guarda o baseline de restauração separado do modo aplicado; reaplicar modo preserva o baseline; o journal grava modo aplicado, baseline (`restoreDisplay`/`restoreAudio`) e é limpo ao restaurar | Fail-closed: restaurar sempre o estado anterior real, inclusive no rescue da P6 |
| D027 | 2026-11-09 | `AppSettings` persiste toggles, atrasos, modos, launcher e caminho; cada gravação relê o arquivo para preservar `language` e chaves desconhecidas | Preferências sobrevivem ao restart sem perder extensões |
| D028 | 2026-11-09 | UI da P3 expõe conectar/desconectar gamepad simulado; o watcher real só entra na P4/P6 | Validar a state machine com risco zero |
| D029 | 2026-11-09 | Endurecimento do contrato P3/P4: `DisplayState.Topology` é um snapshot opaco da topologia completa; journal grava `pending` antes do apply e `active` depois, com baseline; falha do `pending` inicial aborta a mudança | Fechar a janela de crash e permitir rescue fiel na P6 |
| D030 | 2026-11-09 | Suprimir somente `NU1900` quando a fonte de auditoria do NuGet estiver indisponível; a auditoria permanece habilitada quando houver rede | Manter build reproduzível offline com 0 avisos sem esconder vulnerabilidades reais |
| D031 | 2026-11-10 | `DisplayMode.InternalOnly` representa o baseline sem TV; `DisplayState.Mode` é resumo e `Topology` é a fonte de verdade para restore; mapeamento de `Extend` vs `Mirror` vem de sources/modos dos paths ativos | Mapear o estado real sem inferir TV pela ordem dos paths |
| D032 | 2026-11-10 | `IRescueGuard`/`IRescueLease` no Core: begin após journal pending, commit após apply/verify, abort após rollback, abandon quando o rollback falha; `NoopRescueGuard` na simulação; processo real é da P4/Codex | Falhar fechado e permitir watchdog independente sem acoplar Core à plataforma |
| D033 | 2026-11-10 | A raiz de composição do App pode referenciar Platform.Windows para criar, em conjunto, `WindowsDisplayService` e `WindowsRescueGuard` após seleção explícita; ambos permanecem com escrita e recuperação desabilitadas por padrão | Injetar implementações sem levar Win32, COM ou decisões de fluxo para a UI |

## Pendentes

### Antes de P4/P5

1. Identidade do TV (target path + EDID; fallback nome).
2. Modos de display suportados (target-only, estender, espelhar; padrão target-only).
3. API de display (CCD).
4. API de áudio e endpoint padrão (documented APIs; `IPolicyConfig` como fallback).
5. Política de HDR (capturar/reaplicar; não forçar).
6. Acesso a hardware de teste.
7. Privilégios (per-user; admin só se inevitável).
8. Matriz mínima de teste.

### Antes de P6

9. Rescue separado (`CouchPadRescue.exe`) ou flag.
10. Formato/caminho do journal do CouchPad.
11. Gatilho e atraso da tarefa.
12. Lock, concorrência e anti-loop.
13. Bandeja e autostart.
14. Instância única e IPC.

### Antes de P7

15. Instalador (Inno Setup per-user).
16. Assinatura (certificado, self-signed ou nenhuma documentada).
17. Atualização (manual no v1).
18. Canal de release e suporte.
19. Licenças de dependências (`NOTICE`).
20. Definição de v1.0.
21. CI (GitHub Actions).
22. Crash reporting (sem telemetria).
23. Metas de performance.

### Pode esperar

- Tema claro.
- Mais idiomas.
- Modo portátil.
- CLI.
- Auto-update.
- Winget.
- ARM64 nativo.
- Estatísticas locais.

## Formato de nova decisão

```md
## D0XX — Título curto

- Data: AAAA-MM-DD
- Decisão:
- Motivo:
- Impacto:
- Evidência:
```
