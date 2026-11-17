# HUD de desempenho — referência de design

## Anatomia

```text
┌──────────────────────────────────┐
│ ● Desempenho            CouchPad │
│ CPU 32%                GPU 91%   │
│ ▬▬▬▬▬                  ▬▬▬▬▬▬▬  │
│ RAM 12,4 / 32,0 GB     VRAM 7,2 GB│
│ ▬▬▬                    ▬▬▬▬▬▬    │
│                                  │
│ Processo   firefox               │
│ Resolução  1920×1080 · 120 Hz    │
└──────────────────────────────────┘
```

- Tamanho de projeto: **300×210 DIP**; escala 0,75–1,5.
- Cabeçalho: ponto de 6 px em `Accent`, título `hud.overlay.title` e `app.name`.
- Corpo em duas colunas para CPU/GPU e RAM/VRAM; rodapé para processo/resolução.
- Sem controles interativos: o overlay é somente leitura.

## Métricas

| Métrica | Chave | Valor | Barra | Cor |
|---|---|---|---|---|
| CPU | `cpu` | `NN%` | sim | `Accent` |
| CPU do jogo | `cpuProcess` | `NN%` | não | — |
| GPU | `gpu` | `NN%` | sim | `Info` |
| RAM | `ram` | `usado / total GB` | sim | `Accent` |
| VRAM | `vram` | `usado GB` | sim | `Info` |
| Processo | `process` | nome do foreground | não | — |
| Resolução | `display` | `L×A · N Hz` | não | — |

Formatação:

- Percentual com 0 casas: `32%`. Indisponível: `—`.
- Bytes com 1 casa e sufixo `GB`; se o total for desconhecido, mostre só o usado.
- Processo truncado com `TextTrimming=CharacterEllipsis`.
- Nunca use `0%` como sinônimo de indisponível.

## Barras

- Percentual: `largura = clamp(valor, 0, 100) / 100 * 110`.
- Proporção: `largura = clamp(usado / total, 0, 1) * 110`.
- Valor > 0 recebe **mínimo de 2 px**; valor 0 deixa a barra vazia.
- Trilho de 3 px, raio 2, fundo `HudTrack`; preenchimento com `Accent` ou `Info`.
- A barra é reforço visual: o número e o rótulo carregam a informação.

## Overlay

- `Topmost`, `ShowActivated=False`, `ShowInTaskbar=False`,
  `WindowDecorations=None`, `TransparencyLevelHint=Transparent`,
  `IsHitTestVisible=False`, `Focusable=False`.
- Win32: `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_LAYERED`.
- Posição: área útil da tela primária + margem de 24 DIP, em um dos 4 cantos.
- Amostragem a cada 1 s; só desenha quando o HUD está visível.
- Não adicione botões, sliders ou qualquer hit-test no overlay.

## Modal de configuração

- 800×720, corpo rolável, rodapé fixo, `ShowDialog` com dono.
- Campos: ativar, métricas (7 checkboxes), atalho (3 presets), posição (4),
  opacidade 0,4–1,0, escala 0,75–1,5, "só durante a sessão" e prévia.
- Sem métrica selecionada: mostrar aviso e nunca salvar uma lista vazia.
- Salvar devolve o `PerformanceHudSettings`; cancelar devolve `null`.

## Atalho

- Presets: LB+RB+Y (padrão), LB+RB+X, LB+RB+Back, com retenção de 800 ms.
- Exige os botões exatos; parcial ou com botão extra não dispara.
- Disparo por borda, com cooldown de 1 s; soltar rearma.
- O detector tem testes de unidade; a validação em controle físico é registrada
  em `docs/validation-queue.md`.

## Armadilhas conhecidas

- **Token inexistente:** a barra de GPU/VRAM já usou `CouchPad.Accent2`, que não
  existia no tema, e ficou invisível. Use `CouchPad.Info`.
- **Valor baixo:** 1% sem largura mínima desaparece. O mínimo de 2 px resolve.
- **Zero preenchido:** 0% deve deixar a barra vazia, não 2 px.
- **Prévia enganosa:** os números da prévia são ilustrativos e precisam do
  rótulo `hud.preview_note`.
- **Corte no modal:** manter o corpo em `ScrollViewer` para 200% DPI.
- **Prévia no card:** 250×182 fixos; não deixe crescer nem estourar em 1 coluna.

## Validação visual

1. Build/test/smoke.
2. Abrir com todas as métricas e conferir CPU, GPU, RAM, VRAM, processo e tela.
3. Verificar GPU em 0% (barra vazia) e em carga (barra > 0).
4. Conferir opacidade, escala e os 4 cantos.
5. Confirmar click-through e ausência de foco.
6. Repetir em pt-BR e inglês.
7. Capturar `docs/screenshots/hud-fase1-overlay.png` quando o visual mudar.

## Arquivos

- `src/CouchPad.App/Views/PerformanceHudOverlayWindow.axaml`
- `src/CouchPad.App/Views/PerformanceHudSettingsWindow.axaml`
- `src/CouchPad.App/ViewModels/PerformanceHudViewModel.cs`
- `src/CouchPad.App/Views/MainWindow.axaml` (card HUD e prévia)
- `src/CouchPad.Core/Performance/PerformanceHudSettings.cs`
- `src/CouchPad.Platform.Windows/WindowsPerformanceSampler.cs`
- `src/CouchPad.App/Performance/PerformanceHudCoordinator.cs`
- Tokens: `HudSurface`, `HudBorder`, `HudTrack`, `Radius.Hud`.
