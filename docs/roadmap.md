# CouchPad — Roadmap

- Atualizado: 2026-11-23
- Estado atual: 1.0.21 com modo/escala da TV por perfil; instalada em 18/11/2026, aplicação real na TV e publicação pendentes
- Invariantes: 100% gratuito, sem telemetria, sem admin por padrão, fail-closed
- Fila de validação: `docs/validation-queue.md`
- Decisões: `docs/decisions.md`

## Agora

1. **Validar a 1.0.21 na TV**: aplicar resolução, taxa de atualização e escala
   por perfil (D082) no hardware real e confirmar a restauração ao desconectar.
   A lista de modos/escala já foi validada no executável instalado (UIA), mas a
   escrita real de display ainda não foi exercitada.
2. **Publicar a 1.0.21** com tag, SHA-256 e release notes. Ela acrescenta modo e
   escala da TV por perfil (D082) sobre a base da 1.0.20 (sem HUD, D081), com
   diagnóstico (1.0.13), perfis (1.0.12) e reidratação do journal (1.0.11).
3. **Validar os fluxos de automação** na 1.0.21: ativação e restauração pelo
   gamepad, journal e rescue, conforme `docs/validation-queue.md`.

## HUD de monitoramento — removido (D081)

A feature de HUD (CPU, CPU do jogo, GPU, RAM, VRAM, processo, resolução/refresh
e FPS) foi removida na 1.0.20 por decisão D081. Saíram o overlay, o modal, o
card, o chord, o sampler PDH, a integração RTSS e as settings
`performanceHud*`.

- O shell volta a 8 cards e o produto retorna ao foco em automação de display,
  áudio, gamepad e rescue.
- D078 (HUD aprovado) e D080 (FPS via RTSS) ficam superseded; a dependência
  opcional do RTSS e o P/Invoke de PDH/registro também saíram.
- O histórico da feature continua no `CHANGELOG.md` e nos itens 11–15 de
  `docs/validation-queue.md`, marcados como removidos.
- A feature não volta sem nova decisão registrada em `docs/decisions.md`.

## Modo e escala da TV — 1.0.21 (D082)

Cada perfil de ambiente pode definir resolução, taxa de atualização e escala do
Windows da TV. Os modos são lidos do EDID do display (`Automation`/`native`),
aplicados por `ChangeDisplaySettingsEx` com validação e rollback, e a escala por
monitor usa `PerMonitorSettings\DpiValue`. A UI fica no card Display, com opção
“Automático” que mantém o comportamento anterior.

- Validado no executável instalado: lista de resoluções, taxas e escalas.
- Pendente: aplicação real na TV e restauração ao desconectar.

## Próximos passos

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
