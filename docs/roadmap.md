# CouchPad — Roadmap

- Atualizado: 2026-11-17
- Estado atual: 1.0.16 instalado (shell redesenhado com card HUD); publicação pendente
- Invariantes: 100% gratuito, sem telemetria, sem admin por padrão, fail-closed
- Fila de validação: `docs/validation-queue.md`
- Decisões: `docs/decisions.md`

## Agora

1. **Publicar a 1.0.16** com tag, SHA-256 e release notes. Ela inclui o shell
   redesenhado com card HUD (1.0.16), a correção das barras de GPU/VRAM (1.0.15),
   o HUD MVP (1.0.14), o diagnóstico (1.0.13), os perfis (1.0.12) e a
   reidratação do journal (1.0.11).
2. **Validar o chord físico do HUD** com um controle real e publicar.

## HUD de desempenho — Fase 1 implementada

Concept visual: `docs/screenshots/couchpad-performance-hud-concept.png`.
Fonte do mockup: `docs/screenshots/couchpad-performance-hud-concept.html`.
Plano detalhado da Fase 1: `docs/hud-phase1-plan.md`.
Status da Fase 1: **implementada em 2026-11-17**; validação do chord em controle
físico pendente.

### Objetivo

Um medidor local, opt-in, selecionável, exibido como HUD flutuante e
ativado/desativado por uma combinação no controle. Sem injeção em jogos, sem
driver de kernel e sem telemetria.

### Fase 1 — MVP

- Preferências em `AppSettings`:
  - `PerformanceHudEnabled`;
  - `PerformanceHudMetrics`;
  - `PerformanceHudPosition`;
  - `PerformanceHudOpacity`;
  - `PerformanceHudScale`;
  - `PerformanceHudShowOnlyInSession`;
  - `PerformanceHudToggleChord`.
- Métricas:
  - CPU total e do processo do jogo;
  - GPU;
  - RAM;
  - VRAM;
  - processo em primeiro plano;
  - resolução e taxa de atualização.
- HUD Avalonia:
  - sempre no topo;
  - sem foco;
  - click-through;
  - posicionado na tela da TV ativa;
  - opacidade e escala configuráveis.
- Combinação padrão: **LB + RB + Y**, segurada por 0,8 s.
  - detecção de borda, debounce e tempo mínimo;
  - combinação configurável entre presets;
  - o jogo também recebe os botões, então o combo deve ser raro.
- Card de configuração no App:
  - toggle de ativação;
  - chips de métricas;
  - posição, escala e opacidade;
  - “mostrar só durante a sessão”;
  - prévia do HUD.
- i18n pt-BR/en e acessibilidade do card.
- Critérios de aceite:
  - métricas atualizam a cada 1 s;
  - HUD abre/fecha pelo combo;
  - nenhuma escrita de display/áudio;
  - nenhum admin, driver ou injeção;
  - falha de métrica mostra `—`, sem derrubar o App;
  - build, testes e smoke passam.

### Fase 2 — FPS e frame time

- **Status:** FPS implementado e validado com RTSS real (Silent Hill 2, D3D12;
  shared memory read-only, opt-in); frame time e 1% low pendentes;
  PresentMon/ETW experimental pendente.
- **RTSS shared memory** quando o RTSS estiver instalado:
  - FPS;
  - frame time;
  - 1% low;
  - fallback `—` quando indisponível.
- **PresentMon/ETW** como alternativa experimental, em processo helper, sem
  injeção no jogo.
- Documentar a limitação de fullscreen exclusivo e anti-cheat.

### Fase 3 — Sensores opcionais

- Temperatura e potência via shared memory do HWiNFO ou SDK do fabricante.
- Nunca como dependência padrão.
- Sem driver de kernel e sem admin por padrão.

### Fase 4 — Polimento

- Posição multi-monitor e por perfil de ambiente.
- Presets de HUD (compacto, completo, só FPS).
- Métricas por jogo/perfil.
- Ajuste de escala e opacidade com preview em tempo real.

### Fronteira de camadas

- **Core:** modelos, interfaces, detecção de combo, preferências.
- **Platform.Windows:** PDH, `GetSystemTimes`, `GlobalMemoryStatusEx`, overlay
  Win32 e leitura opcional do RTSS/HWiNFO.
- **App:** HUD Avalonia, card de configuração, i18n e integração com o watcher.

### Fora de escopo

- Injeção/hook em jogos.
- Kernel driver.
- Overclock.
- Matar processos ou serviços do Windows.
- Telemetria ou envio de métricas.

## Depois do HUD

- Evidência independente da reconexão do watcher durante a contagem.
- Matriz de áudio em hardware para endpoint tardio e papéis distintos.
- Suporte a controles HID sem XInput.
- Seleção automática de perfil pela TV conectada.
- Exportação de diagnóstico local e modo seguro.
- CI de build/test/instalador, assinatura de código e winget.

## Adiados

- HDR.
- Notificações nativas do Windows.
- Atualização automática dentro do App.
