# CouchPad — Roadmap

- Atualizado: 2026-11-18
- Estado atual: 1.0.20 candidato sem HUD, foco em automação; publicação pendente
- Invariantes: 100% gratuito, sem telemetria, sem admin por padrão, fail-closed
- Fila de validação: `docs/validation-queue.md`
- Decisões: `docs/decisions.md`

## Agora

1. **Publicar a 1.0.20** com tag, SHA-256 e release notes. Ela remove o HUD de
   monitoramento (D081) e mantém o diagnóstico (1.0.13), os perfis (1.0.12) e a
   reidratação do journal (1.0.11).
2. **Validar os fluxos de automação** na 1.0.20: ativação e restauração pelo
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
