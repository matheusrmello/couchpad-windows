# CouchPad — Automação simulada (P3)

- Data: 2026-11-09
- Fase: P3 — serviço simulado, state machine, atrasos, persistência e journal
- Plataforma: Avalonia 12.1.4 / .NET 10 (`net10.0`)
- Escopo: **nenhum hardware real**. Display, áudio e gamepad são simulados.

---

## 1. Objetivo

Validar o fluxo completo de automação antes de qualquer chamada de display,
áudio ou HDR:

1. O usuário liga a automação.
2. Um gamepad simulado conecta.
3. O atraso de ativação começa; desconectar/trocar de estado cancela.
4. Display e áudio simulados são aplicados (`Applying` → `Active`).
5. O estado aplicado e o baseline anterior vão para o journal.
6. Ao desconectar (ou desligar), o atraso de desativação roda e o baseline é
   restaurado (`Pending` → `Applying` → `WaitingForGamepad`/`Idle`).
7. Preferências sobrevivem ao restart.

O fluxo é determinístico e testado com serviços/fakes e um scheduler manual.

---

## 2. Camadas e arquivos

| Item | Caminho |
|---|---|
| State machine | `src/CouchPad.Core/Automation/AutomationCoordinator.cs` |
| Estados/eventos | `src/CouchPad.Core/Automation/AutomationState.cs` |
| Configuração | `src/CouchPad.Core/Automation/AutomationSettings.cs` |
| Atrasos | `src/CouchPad.Core/Automation/DelayOptions.cs`, `DelayScheduler.cs` |
| Snapshot | `src/CouchPad.Core/Automation/AutomationSnapshot.cs` |
| Journal | `src/CouchPad.Core/Automation/SessionJournalEntry.cs`, `JsonSessionJournalStore.cs` |
| Gamepad | `src/CouchPad.Core/Devices/GamepadDevice.cs` |
| Display | `src/CouchPad.Core/Display/DisplayService.cs` |
| Áudio | `src/CouchPad.Core/Audio/AudioService.cs` |
| ViewModel | `src/CouchPad.App/ViewModels/MainWindowViewModel.cs` |
| Shell | `src/CouchPad.App/Views/MainWindow.axaml` |
| Testes | `tests/CouchPad.Core.Tests/AutomationCoordinatorTests.cs`, `JsonSessionJournalStoreTests.cs` |

Nenhum arquivo em `src/CouchPad.Platform.Windows/**` foi alterado.

---

## 3. Interfaces

```csharp
interface IGamepadWatcher
{
    GamepadDevice? Current { get; }
    event EventHandler<GamepadConnectionChangedEventArgs>? ConnectionChanged;
}

interface IDisplayService
{
    DisplayState GetCurrent();
    Task<DisplayState> ApplyAsync(DisplayMode mode, CancellationToken ct = default);
    Task RestoreAsync(DisplayState state, CancellationToken ct = default);
}

interface IAudioService
{
    AudioOutput GetCurrent();
    Task<AudioOutput> ApplyAsync(AudioMode mode, CancellationToken ct = default);
    Task RestoreAsync(AudioOutput state, CancellationToken ct = default);
}

interface IDelayScheduler
{
    Task DelayAsync(TimeSpan delay, CancellationToken ct);
}
```

Implementações da P3:

- `SimulatedGamepadWatcher` — `Connect()`/`Disconnect()`.
- `SimulatedDisplayService` — latência curta configurável, estado abstrato
  `DisplayState(Mode, PrimaryActive, TvActive)`.
- `SimulatedAudioService` — `Default`/`Hdmi`, latência curta.
- `SystemDelayScheduler` — `Task.Delay`; testes usam um scheduler manual.

O ViewModel só conhece as interfaces e a state machine. Nenhum P/Invoke.

---

## 4. State machine

| Estado | Significado | Entradas | Saídas |
|---|---|---|---|
| `Idle` | automação desligada | inicio, desligar, restaurar | `WaitingForGamepad` ao ligar sem gamepad |
| `WaitingForGamepad` | ligada, sem controle | ligar sem gamepad, desconectar | `Pending` ao conectar |
| `Pending` | atraso de ativação/desativação | conectar/desconectar com atraso | `Applying` ao terminar; cancelável |
| `Applying` | aplicando/restaurando serviços | fim do atraso | `Active`, `Error` ou estado de espera |
| `Active` | display/áudio aplicados | aplicação bem-sucedida | `Pending`/`Applying` ao desativar |
| `Error` | falha em aplicar/restaurar | exceção de serviço | `WaitingForGamepad`/`Idle` na próxima transição |
| `RescuePending` | reservado para a P6 | — | — |

### 4.1 Transições

```text
                 ligar sem gamepad
Idle ───────────────────────────────► WaitingForGamepad
  ▲                                       │
  │ desligar                              │ conectar
  │                                       ▼
  │                                    Pending
  │                                       │ atraso
  │                                       ▼
  │                                   Applying
  │                                       │ sucesso
  │                                       ▼
  └──────────── desligar ◄──────────── Active
                                          │ desconectar
                                          ▼
                                       Pending
                                          │ atraso
                                          ▼
                                      Applying
                                          │
                                          ▼
                                  WaitingForGamepad
```

- `Error` é alcançado por exceção em `ApplyAsync`/`RestoreAsync`; o rollback
  tenta voltar ao estado anterior e o erro fica em `LastError`.
- Desligar a automação usa desativação **imediata** (sem atraso) e restaura o
  baseline.
- Desconectar mantém a automação ligada e usa `DeactivationDelay`.

### 4.2 Cancelamento

- Cada transição tem um `CancellationTokenSource`.
- Uma transição nova cancela a anterior; um `SemaphoreSlim` serializa a
  execução.
- Conectar durante a desativação cancela a restauração e reativa.
- Desconectar durante a ativação cancela o atraso/aplicação e volta para
  `WaitingForGamepad`.
- O estado só é atualizado pela transição corrente; canceladas não publicam
  estado final.

### 4.3 Snapshot e restauração

`AutomationSnapshot` guarda:

- `RestoreDisplay` e `RestoreAudio`: **baseline anterior**, alvo da restauração.
- `DisplayMode` e `AudioMode`: modo aplicado.
- `AppliedAudio`: saída aplicada.
- `RestoreDisplay.Topology`: snapshot **opaco** da topologia completa,
  serializado pela implementação de plataforma (schema/versão próprio). O
  Core não interpreta o conteúdo; ele persiste e devolve no restore.

Reaplicar um modo enquanto `Active` (ex.: trocar TV-only por estender) usa o
baseline original do snapshot anterior, não o estado aplicado. Isso evita
restaurar “TV-only” quando o baseline era “estender”.

Se uma reaplicação falhar, o snapshot anterior é mantido, o journal continua
válido e o estado vira `Error`; desligar/redefinir ainda restaura o baseline.

Mudar modo enquanto `Pending`/`Applying` vale para o próximo ciclo: a
transição corrente usa a cópia imutável de `AutomationSettings` capturada no
início. Com `Active`, a troca de modo reaplica imediatamente.

---

## 5. Atrasos

| Índice | Valor |
|---|---|
| 0 | `Zero` (imediato) |
| 1 | 1 s |
| 2 | 2 s |
| 3 | 3 s |
| 4 | 5 s |

`DelayOptions.FromIndex` clampa índices inválidos para zero. O atraso de
ativação começa ao conectar com a automação ligada; o de desativação, ao
desconectar. O estado visível durante o atraso é `Pending`
(`status.pending` = “Aguardando atraso”/“Waiting for delay”).

---

## 6. Journal

- Arquivo: `%LOCALAPPDATA%\CouchPad\state\session.json`.
- Gravação atômica (`.tmp` + move), tolerante a JSON corrompido (`.bak`).
- Entrada:
  ```json
  {
    "state": "active",
    "displayMode": "tvOnly",
    "audioMode": "moveToHdmi",
    "updatedAt": "2026-11-09T20:00:00+00:00",
    "restoreDisplay": {
      "mode": "extend",
      "primaryActive": true,
      "tvActive": false,
      "topology": "{\"schema\":1,\"paths\":[]}"
    },
    "restoreAudio": "default",
    "appliedAudio": "hdmi"
  }
  ```
- `pending` é gravado **antes** de qualquer mudança, com o baseline e os
  modos alvo. Se o processo cair durante o apply, a P6 encontra este estado e
  restaura o baseline.
- `active` é gravado após o sucesso. Se essa gravação falhar, o `pending`
  anterior permanece e ainda serve para o rescue.
- Se a gravação `pending` falhar em uma ativação inicial, a mudança é
  abortada com `Error` (fail-closed): sem journal não há como recuperar.
- `restoreDisplay`/`restoreAudio` são o **baseline anterior**; a P6 usa esses
  valores para o rescue, não o modo aplicado.
- `restoreDisplay.topology` é opaco e obrigatório para restaurar a topologia
  completa; a P4 preenche esse campo.
- O arquivo é apagado quando a restauração termina com sucesso. Se o rollback
  falhar, o journal é mantido para a P6.
- A P6 usará o journal para rescue no logon; a P3 só grava/limpa.

---

## 7. Persistência

`AppSettings` agora guarda:

- `automationEnabled`, `closeLauncherOnActivate`, `startWithWindows`,
  `minimizeToTray`;
- `activationDelayIndex`, `deactivationDelayIndex`;
- `displayModeIndex`, `audioModeIndex`;
- `steamEnabled`, `customLauncherPath`;
- `language` (já existente).

Cada gravação relê o arquivo antes de salvar para preservar `language` e
chaves desconhecidas (`[JsonExtensionData]`). Falha de escrita é silenciosa.

---

## 8. UI

- Card **Automação**: toggle liga/desliga; as linhas Gamepad/Tela/Áudio/Launcher
  refletem a state machine e os serviços simulados.
- Card **Gamepad**: botão “Conectar controle”/“Desconectar”, resumo
  “Controle simulado”/“Nenhum controle conectado” e dica de simulação.
- Card **Display**: “Atual” mostra o modo aplicado quando `Active`; “Destino”
  continua “TV · HDMI”.
- Cabeçalho/rodapé: pill com `status.pending`, `status.applying`,
  `status.active` etc.; tom neutro/aviso/acento/perigo.
- Configurações e caminho do launcher persistem; reset volta aos padrões e
  desliga a automação.
- O refresh de displays continua um placeholder de 700 ms até a P4.

---

## 9. Testes

`AutomationCoordinatorTests` cobre:

- conectar com automação desligada fica `Idle`;
- ligar sem gamepad fica `WaitingForGamepad`;
- atraso de ativação: `Pending` → `Active`, journal `pending` antes do apply
  e `active` depois, com baseline;
- falha do journal antes do apply aborta a ativação sem mudar o display;
- falha do journal depois do apply mantém o `pending` com o baseline;
- topologia opaca é persistida no journal;
- desconectar durante o atraso cancela e não aplica;
- desconectar sem snapshot fica `WaitingForGamepad`; com automação desligada
  fica `Idle`;
- desconectar após ativo espera o atraso e restaura;
- reconectar durante a desativação cancela a restauração;
- falha de aplicação vira `Error`, restaura e não grava journal;
- desligar durante `Active` restaura imediatamente;
- trocar modo durante `Active` reaplica;
- reaplicação falha mantém o snapshot anterior para restore;
- reaplicação preserva o baseline original;
- eventos reportam `Pending`, `Applying`, `Active`;
- `DelayOptions` clampa índices.

`JsonSessionJournalStoreTests` cobre roundtrip, arquivo ausente, JSON
corrompido com backup, clear, ausência de `.tmp`, criação de diretório e
journal antigo sem baseline (campos nulos).

`JsonAppSettingsStoreTests` cobre os novos campos e a preservação de chaves
desconhecidas.

Total na P3: **59 testes**.

---

## 10. Limites e próximos passos

- Sem display/áudio/HDR reais; a P4/P5 substitui as implementações simuladas
  mantendo as interfaces e a state machine.
- Sem watcher real de gamepad; a P4/P6 decide a origem (HID/XInput).
- Sem rescue no logon; a P6 lê o journal e implementa `RescuePending` de fato.
- Refresh de displays ainda é placeholder; a P4 troca por CCD.
- A P3 não abre diálogo de pasta; o fluxo da P2 continua na view.
- Nenhuma licença, ativação, assinatura ou telemetria foi adicionada.
