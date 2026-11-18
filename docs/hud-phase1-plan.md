# Fase 1 — HUD de desempenho (MVP)

> **Removido na 1.0.20 (D081).** Documento histórico; a feature de HUD não existe mais.

- Status: **Fase 1 implementada; chord físico e aceite pendentes**
- Data: 2026-11-17
- Roadmap: `docs/roadmap.md`
- Concept: `docs/screenshots/couchpad-performance-hud-concept.png`
- Decisão: D078

## 1. Objetivo

Entregar um HUD local, opt-in e read-only com métricas básicas da sessão,
métricas selecionáveis e toggle por combinação no controle. Sem injeção em
jogos, sem kernel driver, sem admin e sem telemetria.

## 2. Escopo do MVP

### Incluído

- Métricas:
  - CPU total;
  - CPU do processo do jogo;
  - GPU;
  - RAM usada/total;
  - VRAM usada;
  - processo em primeiro plano;
  - resolução e taxa de atualização.
- HUD flutuante:
  - sempre no topo;
  - sem foco;
  - click-through;
  - sem entrada na barra de tarefas/Alt+Tab;
  - posição no canto superior direito da tela principal;
  - opacidade e escala;
  - atualização a cada 1 s.
- Card de configuração:
  - toggle de ativação;
  - seleção de métricas;
  - posição, opacidade e escala;
  - “mostrar só durante a sessão”;
  - prévia do HUD.
- Combinação no controle:
  - padrão **LB + RB + Y**, segurada por 0,8 s;
  - presets configuráveis;
  - debounce, detecção de borda e rearme após soltar.
- Persistência em `AppSettings`.
- i18n pt-BR/en.
- Testes puros e de integração com fakes.

### Fora do MVP

- FPS, frame time e 1% low.
- Temperatura e potência.
- Integração com RTSS/PresentMon/HWiNFO.
- Múltiplos monitores e posição por perfil.
- Histórico/gráficos.
- Overlay em fullscreen exclusivo.
- Injeção, kernel driver, overclock ou encerramento de processos.

## 3. Fluxo do usuário

1. O usuário abre **Parâmetros → HUD de desempenho → Configurar**.
2. Escolhe as métricas e ajusta posição/opacidade/escala.
3. Salva.
4. O HUD aparece quando a sessão estiver ativa e o toggle estiver ligado.
5. Durante o jogo, segura **LB + RB + Y** por 0,8 s para mostrar/ocultar.
6. Ao desligar a automação ou encerrar o CouchPad, o HUD fecha.

### Visibilidade

- `PerformanceHudEnabled` é o master da funcionalidade.
- O chord alterna a visibilidade da sessão, sem alterar o master.
- Com `ShowOnlyInSession`, o HUD só pode aparecer durante
  `AutomationState.Active`; ao sair da sessão, ele é ocultado.
- Ao iniciar uma nova sessão, a visibilidade volta a ligada se o master estiver
  ligado.
- Com o master desligado, o chord não tem efeito.

## 4. Modelo de dados

Adicionar em `AppSettings`:

```csharp
public bool PerformanceHudEnabled { get; init; }
public List<string>? PerformanceHudMetrics { get; init; }
public string? PerformanceHudPosition { get; init; }
public double PerformanceHudOpacity { get; init; } = 0.9;
public double PerformanceHudScale { get; init; } = 1.0;
public bool PerformanceHudShowOnlyInSession { get; init; } = true;
public string? PerformanceHudToggleChord { get; init; }
```

Padrões:

```text
enabled = false
metrics = cpu, gpu, ram, process
position = top-right
opacity = 0.9
scale = 1.0
showOnlyInSession = true
chord = LB+RB+Y
```

Métricas como strings para não quebrar o serializador atual de settings.
Valores desconhecidos são ignorados; a lista vazia cai no padrão.

## 5. Contratos

### Core

```csharp
public sealed record PerformanceSnapshot(
    double? CpuTotalPercent,
    double? CpuProcessPercent,
    double? GpuPercent,
    ulong MemoryUsedBytes,
    ulong MemoryTotalBytes,
    ulong? VramUsedBytes,
    string? ForegroundProcessName,
    int? ScreenWidth,
    int? ScreenHeight,
    int? RefreshHz,
    DateTimeOffset CapturedAt);

public interface IPerformanceSampler
{
    Task<PerformanceSnapshot> SampleAsync(CancellationToken cancellationToken = default);
}
```

```csharp
public enum PerformanceMetric
{
    Cpu,
    CpuProcess,
    Gpu,
    Ram,
    Vram,
    Process,
    Display,
}
```

```csharp
[Flags]
public enum GamepadButtons
{
    None = 0,
    A = 1 << 0,
    B = 1 << 1,
    X = 1 << 2,
    Y = 1 << 3,
    LeftShoulder = 1 << 4,
    RightShoulder = 1 << 5,
    Back = 1 << 6,
    Start = 1 << 7,
}

public sealed record GamepadChord(GamepadButtons Buttons, TimeSpan Hold);

public sealed class GamepadChordDetector
{
    public GamepadChordDetector(GamepadChord chord, TimeSpan? cooldown = null);
    public bool Update(GamepadButtons pressed, DateTimeOffset now);
    public void Reset();
}
```

`Update` retorna `true` uma única vez quando o chord foi mantido pelo tempo
mínimo. Depois é preciso soltar todos os botões para rearmar.

```csharp
public interface IGamepadButtonSource : IDisposable
{
    // Disparado a cada ciclo de poll com o estado atual; a detecção de borda/hold
    // é responsabilidade do consumidor (GamepadChordDetector).
    event EventHandler<GamepadButtons>? ButtonsReported;
}
```

### App

```csharp
public interface IPerformanceHudPresenter
{
    bool IsVisible { get; }
    void Show(PerformanceSnapshot snapshot, AppSettings settings);
    void Update(PerformanceSnapshot snapshot, AppSettings settings);
    void Hide();
}
```

`PerformanceHudCoordinator`:

- recebe `IPerformanceSampler`, `IGamepadButtonSource`, `IPerformanceHudPresenter`,
  `IAppSettingsStore` e `TimeProvider`;
- aplica settings;
- inicia/para a amostragem;
- escuta o chord;
- respeita `ShowOnlyInSession`;
- trata falha de sampler mostrando `—`;
- não toca em display/áudio/journal.

### Platform.Windows

Implementações de `IGamepadButtonSource` e do sampler:

- `WindowsPerformanceSampler` — PDH/GetSystemTimes/GlobalMemoryStatusEx/
  foreground process/EnumDisplaySettings.
- `WindowsGamepadButtonSource` — polling XInput a 50 ms, somente leitura.
- `WindowsOverlayWindow` — aplica `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE |
  WS_EX_TRANSPARENT | WS_EX_LAYERED`, topmost e posição.

## 6. Coleta de métricas

| Métrica | Implementação |
|---|---|
| CPU total | `GetSystemTimes` com delta entre amostras |
| CPU do processo | `Process.TotalProcessorTime` + delta / núcleos |
| GPU | PDH `\GPU Engine(*)\Utilization Percentage`, engine 3D, maior adapter |
| RAM | `GlobalMemoryStatusEx` |
| VRAM | PDH `\GPU Adapter Memory(*)\Dedicated Usage` |
| Processo | `GetForegroundWindow` + `GetWindowThreadProcessId` |
| Resolução/refresh | `EnumDisplaySettings` do monitor primário |

Regras:

- usar `PdhAddEnglishCounter` para não depender do idioma do Windows;
- amostrar a cada 1 s;
- primeira amostra de CPU/processo pode vir `—` até existir delta;
- GPU/VRAM ausentes → `—`;
- processo sem janela → “Área de trabalho” ou `—`;
- nenhuma consulta pode bloquear a UI por mais de alguns milissegundos.

## 7. HUD

- `PerformanceHudOverlayWindow` em Avalonia:
  - `SystemDecorations=None`;
  - `ShowInTaskbar=false`;
  - `Topmost=true`;
  - `ShowActivated=false`;
  - `Focusable=false`;
  - fundo transparente;
  - card compacto com as métricas selecionadas.
- `WindowsOverlayWindow` aplica click-through/no-activate após `Opened`.
- Posição no canto superior direito da tela principal, margem de 24 px.
- Escala via `LayoutTransformControl`; opacidade via `Opacity`.
- Métricas em linhas curtas:
  - `FPS` não aparece no MVP;
  - CPU/GPU/RAM/VRAM com barra e valor;
  - processo e resolução em texto.
- Atualização via `DispatcherTimer` de 1 s ou `PeriodicTimer` + `Dispatcher.UIThread`.
- Falha de sampler: manter o HUD, mostrar `—`, não fechar e não derrubar o App.

## 8. Combo do controle

- Padrão: **LB + RB + Y**, 0,8 s.
- Presets:
  - LB + RB + Y;
  - LB + RB + X;
  - LB + RB + Back.
- O jogo também recebe os botões; documentar que o combo deve ser raro.
- Resetar o detector ao desconectar o controle.
- Não disparar no connect/disconnect.
- Não disparar repetidamente enquanto o chord estiver pressionado.
- Cooldown de 1 s após o toggle.
- Testes com relógio fake e sequências de press/release.

## 9. UI de configuração

- Linha em **Parâmetros**:
  - toggle “HUD de desempenho”;
  - link “Configurar”.
- Item na bandeja **HUD de desempenho** para alternar a visibilidade sem o
  controle.
- Modal `PerformanceHudSettingsWindow`:
  - título e toggle;
  - chips de métricas;
  - FPS/frame time desabilitados com selo “Fase 2”;
  - combo selector com key chips;
  - posição, escala, opacidade;
  - “só durante a sessão”;
  - prévia estática/live do HUD;
  - Cancelar/Salvar.
- Salvar apenas os campos de HUD, preservando perfis e demais settings.
- Não aplicar escrita de display/áudio.

## 10. Persistência e migração

- Settings antigos sem campos de HUD carregam padrões.
- `JsonAppSettingsStoreTests` cobre roundtrip e defaults.
- Não alterar `ActiveProfileId`/`EnvironmentProfiles`.
- O HUD pertence ao App, não ao perfil de ambiente no MVP.

## 11. i18n e acessibilidade

Novas chaves em pt-BR/en:

- `hud.title`, `hud.help`, `hud.enabled`, `hud.configure`;
- `hud.metrics`, `hud.metric.cpu`, `hud.metric.gpu`, `hud.metric.ram`,
  `hud.metric.vram`, `hud.metric.process`, `hud.metric.display`;
- `hud.metric.fps`, `hud.metric.frametime`, `hud.phase2`;
- `hud.chord`, `hud.chord.hold`, `hud.chord.preset.*`;
- `hud.position`, `hud.opacity`, `hud.scale`, `hud.session_only`;
- `hud.preview`, `hud.save`, `hud.cancel`.

Acessibilidade:

- modal navegável por teclado;
- nomes e ajuda em chips/toggles;
- HUD não recebe foco e não intercepta mouse;
- atalho de teclado/tray para alternar, além do controle.

## 12. Testes

### Core

- `GamepadChordDetectorTests`:
  - não dispara antes do hold;
  - dispara uma vez no hold;
  - rearma após soltar;
  - reset no disconnect;
  - cooldown;
  - combinações parciais.
- `PerformanceMetricParsingTests`: strings válidas/inválidas.
- `JsonAppSettingsStoreTests`: defaults/roundtrip dos campos de HUD.

### App

- `PerformanceHudCoordinatorTests` com fakes:
  - toggle mostra/oculta;
  - show-only-in-session;
  - sampler falha → `—`;
  - métricas selecionadas;
  - settings aplicados.
- `PerformanceHudViewModelTests`:
  - formatação de CPU/GPU/RAM/VRAM;
  - processo ausente;
  - resolução/refresh.
- `PerformanceHudSettingsViewModelTests`: salvar preserva perfis.

### Platform

- `WindowsGamepadButtonSource` com `IXInputStateSource` fake:
  - mapeamento de botões;
  - evento só na mudança;
  - release.
- `WindowsPerformanceSampler` com fonte de contadores fake:
  - primeira amostra;
  - deltas;
  - ausência de GPU.
- `WindowsOverlayWindow` não é testável sem HWND; validar manualmente.

## 13. Validação manual

1. Abrir o App em simulação e produção.
2. Abrir o modal, habilitar HUD, selecionar CPU/GPU/RAM/processo.
3. Confirmar HUD no canto superior direito.
4. Clicar através do HUD em uma janela abaixo.
5. Confirmar que o HUD não aparece no Alt+Tab e não rouba foco.
6. Segurar LB+RB+Y e confirmar toggle.
7. Soltar e repetir; confirmar que não dispara duas vezes.
8. Desconectar/reconectar o controle; confirmar reset.
9. Alternar “só durante a sessão”.
10. Fechar/reabrir o App e confirmar persistência.
11. Confirmar journal ausente, sem escrita de display/áudio e sem admin.
12. Medir CPU/RAM do próprio App com o HUD ligado.

## 14. Sequência de implementação

1. **Core**: modelos, settings, parser de métricas, chord detector + testes.
2. **Platform.Windows**: sampler, button source, overlay helper + testes.
3. **App**: coordinator, presenter, HUD window + testes com fakes.
4. **App UI**: modal, linha em Parâmetros, i18n.
5. **Integração**: `App.axaml.cs`, watcher, tray/atalho.
6. **Validação**: build/test/smoke, HW manual, overhead, screenshots.
7. **Docs**: roadmap, HANDOFF, validation queue, screenshots README.

## 15. Critérios de aceite

- [x] HUD desligado por padrão.
- [ ] HUD abre/fecha pelo combo LB+RB+Y no controle físico. O detector está coberto por testes unitários.
- [x] Métricas selecionadas aparecem; não selecionadas não aparecem.
- [x] CPU/GPU/RAM/VRAM/processo/resolução atualizam a cada 1 s.
- [x] Métrica indisponível mostra `—`.
- [x] HUD é click-through e não rouba foco.
- [x] “Só durante a sessão” funciona.
- [x] Settings persistem e migram.
- [x] Sem escrita de display/áudio, sem journal, sem admin.
- [x] Nenhuma telemetria ou rede.
- [x] Build 0/0, 309 testes e smoke.
- [ ] Validação manual do chord físico e aceite do usuário.
- [x] Capturas do HUD/modal e docs atualizados.

## 16. Riscos e mitigação

| Risco | Mitigação |
|---|---|
| GPU/VRAM indisponível em alguns drivers | `—` + fallback; não bloquear |
| Overlay não aparece em fullscreen exclusivo | documentar; borderless/windowed |
| Anti-cheat bloqueia overlay | opt-in; sem injeção; documentar |
| Combo conflita com o jogo | presets raros; hold 0,8 s; configurável |
| Sampler pesa na UI | 1 s, fora da UI thread, sem WMI |
| Click-through falha | `WS_EX_TRANSPARENT` + validação manual |
| Múltiplas GPUs | mostrar o adapter mais ativo no MVP |
| Settings antigos | defaults + testes de migração |

## 17. Fronteira de camadas

- **OpenCode/App+Core:** modelos, settings, detector, coordinator, UI, i18n,
  testes e docs.
- **OpenCode/Platform.Windows:** PDH, APIs Win32, XInput button source, overlay
  interop, testes de plataforma.
- A divisão acima é arquitetural, não de agentes. O Codex foi descontinuado
  (D079); o OpenCode pode implementar todas as camadas, mantendo build/testes e
  validação de hardware quando aplicável.

## 18. Próximo passo

Implementar o pacote 1 (Core) e o pacote 2 (Platform.Windows) em commits
separados, com testes antes da UI. A UI só entra depois que o sampler e o chord
detector estiverem estáveis.
