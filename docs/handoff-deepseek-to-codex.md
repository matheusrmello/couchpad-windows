# Handoff — P3 (DeepSeek) → REA/P4 (Codex)

- Data: 2026-11-09
- Origem: P3 — serviço simulado, state machine, atrasos, journal e persistência
- Destino: REA 2.2.0 + P4 — display real
- Tag da origem: `v0.4.0-sim`
- Build: 0 avisos, 0 erros
- Testes: 56 aprovados
- Árvore: limpa

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
3. **`DisplayState`** é abstrato (`Mode`, `PrimaryActive`, `TvActive`). Se a
   implementação real precisar de mais dados (target path, EDID, resolução),
   adicione campos opcionais ao record ou um estado interno na implementação,
   sem quebrar a UI.
4. **Journal**: mantenha `state`, `displayMode`, `audioMode`, `updatedAt`,
   `restoreDisplay` e `restoreAudio`. A P6 usa o baseline para rescue.
5. **Settings**: conserve os campos existentes e a gravação que relê o arquivo
   antes de salvar (`language` e extensões desconhecidas).
6. **UI**: o `MainWindowViewModel` aceita `IGamepadWatcher`, `IDisplayService`,
   `IAudioService` e `IDelayScheduler` no construtor. A composição em
   `App.axaml.cs` escolhe simulação ou implementação real.
7. **Sem admin por padrão**, per-user em `%LOCALAPPDATA%\CouchPad`.
8. **Sem P/Invoke no App/Core**: implementações Win32/COM ficam em
   `src/CouchPad.Platform.Windows/**`.
9. **i18n**: nada de texto visível fora dos catálogos; se a P4 adicionar
   estados, use chaves nos dois idiomas e atualize o teste de XAML.

---

## 2. O que a P4 precisa fazer

Ordem recomendada:

1. **REA 2.2.0 estático** (Codex) — preencher
   `docs/rea-shiftpad-2.2.0.md` com evidência, sem executar o binário.
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
