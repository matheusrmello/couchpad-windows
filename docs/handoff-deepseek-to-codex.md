# Handoff — P3 (DeepSeek) → REA/P4 (Codex)

- Data: 2026-11-09
- Origem: P3 — serviço simulado, state machine, atrasos, journal e persistência
- Destino: REA 2.2.0 + P4 — display real
- Tag da origem: `v0.4.0-sim`
- Contrato endurecido: `5f5c8c4` (v0.4.1), `DisplayMode.InternalOnly` (v0.4.2), guard de rescue no Core (v0.4.3)
- Build: 0 avisos, 0 erros
- Testes: 144 aprovados
- Árvore: limpa (os arquivos de REA/P4 do Codex são commitados separadamente)
- Aceite do usuário: 09/11/2026

> Atualização de contrato: `DisplayState.Topology` e o journal `pending`
> antes do apply foram adicionados depois da tag `v0.4.0-sim`. Use o HEAD
> atual, não apenas a tag, ao iniciar a P4.

---

## 1. O que a P3 entrega para a P4

### Interfaces do Core

Em `src/CouchPad.Core`:

- `Devices/GamepadDevice.cs`
  - `IGamepadWatcher`, `ISimulatedGamepad`, `SimulatedGamepadWatcher`.
- `Display/DisplayService.cs`
  - `IDisplayService`, `DisplayMode`, `DisplayState`, `SimulatedDisplayService`.
- `Audio/AudioService.cs`
  - `IAudioService`, `AudioMode`, `AudioOutput`, `SimulatedAudioService`.
- `Automation/DelayScheduler.cs`
  - `IDelayScheduler`, `SystemDelayScheduler`.
- `Automation/AutomationCoordinator.cs`
  - state machine e orquestração.
- `Automation/SessionJournalEntry.cs` + `JsonSessionJournalStore.cs`
  - journal atômico com baseline de restauração.
- `Automation/RescueGuard.cs`
  - `IRescueGuard`/`IRescueLease` e `NoopRescueGuard`; o coordenador já
    executa begin/commit/abort/abandon.
- `Automation/AutomationSnapshot.cs`
  - baseline separado do modo aplicado.

### Contratos que a P4 deve manter

1. **A state machine fica no Core.** Não replique transições em
   `Platform.Windows` nem na UI.
2. **`IDisplayService`**:
   - `GetCurrent()` devolve o baseline antes de qualquer apply;
   - `ApplyAsync(mode, ct)` respeita cancelamento e retorna o estado aplicado;
   - `RestoreAsync(state, ct)` restaura exatamente o `DisplayState` recebido;
   - falhas devem ser exceções; o coordenador faz rollback e vira `Error`.
3. **`DisplayState`** é abstrato (`Mode`, `PrimaryActive`, `TvActive`) e tem
   `Topology` opaco. A implementação real deve preencher `Topology` em
   `GetCurrent()`/`ApplyAsync` com o snapshot completo da topologia CCD
   (paths, modes, flags e versão do schema). O Core não interpreta o conteúdo;
   `RestoreAsync` o consome.
   - `Mode` é um resumo, não a fonte de verdade:
     - só interno ativo → `InternalOnly`;
     - só TV ativa → `TvOnly`;
     - TV + interno com sources/modos distintos → `Extend`;
     - TV + interno clonados (mesmo source/modo) → `Mirror`.
   - `PrimaryActive`/`TvActive` vêm dos paths ativos e do seletor de TV.
   - Seletor: EDID + target path/UID primeiro; nome exato como fallback;
     ambiguidade recusa. Não inferir a TV pela ordem dos paths.
4. **Journal**: mantenha `state` (`pending`/`active`), `displayMode`,
   `audioMode`, `updatedAt`, `restoreDisplay` (com `Topology`), `restoreAudio`
   e `appliedAudio`. O `pending` é gravado **antes** do apply; o `active`
   depois. A P6 usa o baseline para rescue.
5. **Falha de journal**: se o `pending` inicial não puder ser gravado, o
   coordenador aborta a mudança com `Error`. Se o `active` falhar, o `pending`
   permanece e ainda serve para rescue.
6. **Settings**: conserve os campos existentes e a gravação que relê o arquivo
   antes de salvar (`language` e extensões desconhecidas).
7. **UI**: o `MainWindowViewModel` aceita `IGamepadWatcher`, `IDisplayService`,
   `IAudioService` e `IDelayScheduler` no construtor. A composição em
   `App.axaml.cs` escolhe simulação ou implementação real.
8. **Sem admin por padrão**, per-user em `%LOCALAPPDATA%\CouchPad`.
9. **Sem P/Invoke no App/Core**: implementações Win32/COM ficam em
   `src/CouchPad.Platform.Windows/**`.
10. **i18n**: nada de texto visível fora dos catálogos; se a P4 adicionar
    estados, use chaves nos dois idiomas e atualize o teste de XAML.
11. **Watchdog/rescue**: implemente `IRescueGuard` em `Platform.Windows` com
    processo separado. `BeginAsync` é chamado depois do journal `pending` e
    antes do apply; `CommitAsync` após apply/verify; `AbortAsync` após rollback
    bem-sucedido; `Abandon` quando o rollback falhar. O watchdog deve ler o
    journal e restaurar o baseline se o processo pai morrer/timeout. O Core já
    chama esse contrato e usa `NoopRescueGuard` na simulação.
12. **Opt-in de escrita**: a UI grava `AllowDisplayWrites` e os seletores de TV
    e monitor interno em `settings.json`. O App só cria `WindowsDisplayService`
    com `AllowWrites=true` e `WindowsRescueGuard` com `allowRecovery=true`
    quando o opt-in e os seletores estão completos; o padrão é simulação.
    Após alterar o seletor/opt-in, reinicie o app. Para a matriz, use a UI ou a
    configuração explícita; nunca habilite por padrão.
13. **Recuperação incerta**: se o coordenador marcar
    `recoveryUncertain`, a UI mostra um aviso e desabilita o toggle de
    automação; só reiniciar destrava. Não tente outra mutação.

---

## 2. O que a P4 precisa fazer

Ordem recomendada:

1. **REA 2.2.0 estático** (Codex) — concluída no commit `d2e1a74`;
   consulte `docs/rea-shiftpad-2.2.0.md`. O que restou como desconhecido virou
   hipótese e não bloqueia a implementação clean-room.
2. Identidade da TV:
   - target path + EDID quando disponível;
   - fallback por nome/fabricante;
   - nunca assumir que o primeiro monitor é a TV.
3. Modos de display:
   - `TvOnly` (padrão), `Extend`, `Mirror`;
   - consultar modos suportados antes de aplicar;
   - capturar a configuração atual antes de mudar.
4. Aplicar com CCD (`SetDisplayConfig`) em `Platform.Windows`, implementando
   `IDisplayService`.
5. Rollback fail-closed:
   - qualquer falha em display/áudio restaura o baseline;
   - journal é gravado apenas em `Active` e limpo após restore;
   - P6 fará o rescue no logon a partir do journal.
6. Refresh de displays real: substituir o placeholder de 700 ms no VM ou
   ligar o botão a uma leitura de `IDisplayService`.
7. Testes:
   - unitários puros no Core para a state machine (já existem);
   - testes de hardware com matriz documentada;
   - nenhum teste de hardware na suíte padrão (marcar/separar).
8. Atualizar docs, ledger e criar a tag de hardware ao final de P6, conforme o
   plano (`v0.6.0-hardware`).

---

## 3. Pontos de composição

`src/CouchPad.App/App.axaml.cs`:

```csharp
var settingsStore = new JsonAppSettingsStore(settingsPath);
var journalStore = new JsonSessionJournalStore(journalPath);

var vm = new MainWindowViewModel(
    LocalizationService.Instance,
    settingsStore,
    journalStore,
    gamepadWatcher,   // P4/P6: real; hoje null => simulado
    displayService,   // P4: WindowsDisplayService; hoje null => simulado
    audioService,     // P5: WindowsAudioService; hoje null => simulado
    delayScheduler);  // padrão: SystemDelayScheduler
```

- Enquanto a P4 não estiver pronta, mantenha os defaults simulados.
- A UI continua funcionando com qualquer implementação das interfaces.
- `IsSimulationAvailable` esconde o botão de conectar/desconectar quando um
  watcher real é injetado.

---

## 4. Riscos e decisões pendentes

1. **Identidade da TV**: EDID/target path pode não estar disponível; defina o
   fallback e registre em `docs/decisions.md`.
2. **Modos suportados**: validar `DisplayConfigGetDeviceInfo` e
   `QueryDisplayConfig`; não aplicar modo não suportado.
3. **Rollback**: guardar a configuração completa, não só o modo.
4. **HDR**: P5; não forçar HDR se não suportado.
5. **Áudio**: P5; endpoint padrão e retry.
6. **Combinação de botões**: P6; a UI já tem os chips, mas o botão Alterar
   continua desabilitado.
7. **Rescue**: P6; `RescuePending` está reservado no enum e o journal já tem
   o baseline.
8. **Privilégios**: per-user, sem admin; se alguma API exigir elevação,
   documentar e pedir decisão.

---

## 5. Verificação antes de começar

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"

& $dotnet build CouchPad.slnx -c Debug
& $dotnet test CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --smoke
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --gallery
```

- O smoke imprime uma linha e fecha.
- A galeria é o catálogo visual dos componentes (P2), sem hardware.
- O fluxo P3 está documentado em `docs/automation-simulation.md`.
- O runbook da matriz P4 está em `docs/p4-runbook.md`.
- Capturas do fluxo: `p3-01-idle.png` … `p3-06-restored.png` em
  `C:\Users\Usuario\AppData\Local\Temp\opencode\`.

---

## 6. Fronteira entre agentes

| Área | Dono |
|---|---|
| `src/CouchPad.App/**` | DeepSeek |
| `src/CouchPad.Core/**` | DeepSeek |
| `src/CouchPad.Platform.Windows/**` | Codex |
| `analysis/**`, `docs/rea-*` | Codex |
| `docs/decisions.md`, `HANDOFF.md` | Ambos (append-only) |

Codex não reescreve a UI sem manter o contrato do Core. DeepSeek não escreve
em `Platform.Windows`. Qualquer mudança de interface do Core precisa ser
registrada e manter a UI compilando.

---

## 7. Próximo handoff

Ao terminar P4–P6, Codex deve:

1. Atualizar `HANDOFF.md` (ledger + seção de evidência).
2. Registrar decisões novas em `docs/decisions.md`.
3. Rodar build, testes e a matriz de hardware.
4. Criar a tag `v0.6.0-hardware`.
5. Escrever `docs/handoff-codex-to-deepseek.md` para a P7a (docs, a11y,
   screenshots, release notes).
6. Parar para aceite do usuário.
