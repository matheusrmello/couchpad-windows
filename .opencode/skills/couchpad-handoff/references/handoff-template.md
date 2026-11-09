# Templates de handoff

## DeepSeek → Codex

Arquivo: `docs/handoff-deepseek-to-codex.md`

```md
# Handoff DeepSeek → Codex

## Identidade

- Tag: v0.3.0-sim
- Commit: <sha>
- Data: AAAA-MM-DD
- Fases concluídas: P0, P1, P2, P3

## Como rodar

```powershell
$env:DOTNET = 'C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe'
& $env:DOTNET build
& $env:DOTNET test
& $env:DOTNET run --project src/CouchPad.App
```

## O que está pronto

- Tema, i18n pt-BR/en, shell, componentes, estados.
- Serviço simulado com atrasos, cancelamento e falhas.
- Testes: <n> passando.

## O que é simulado

- `FakeDisplayService`, `FakeAudioService`, `FakeHdrService`, `FakeGamepadService`.
- Nenhuma chamada real de tela/áudio/HDR.

## Contratos

- `IDisplayService`: <assinatura>
- `IAudioService`: <assinatura>
- `IHdrService`: <assinatura>
- `IGamepadService`: <assinatura>
- `IRescueStore`: <assinatura>

## TODOs para o Codex

- [ ] Implementar `Platform.Windows` sem quebrar as interfaces.
- [ ] Confirmar identidade do TV no REA.
- [ ] Trocar fakes por implementações reais.
- [ ] Ajustar estados de erro conforme evidência.

## Perguntas abertas

1. <pergunta> — precisa de REA/hardware.

## Evidências

- Screenshots: `docs/screenshots/p3/`
- Testes: `<comando e resumo>`
- Tag: `v0.3.0-sim`
```

## Codex → DeepSeek

Arquivo: `docs/handoff-codex-to-deepseek.md`

```md
# Handoff Codex → DeepSeek

## Identidade

- Tag: v0.6.0-hardware
- Commit: <sha>
- Data: AAAA-MM-DD
- Fases concluídas: REA, P4, P5, P6

## O que mudou no comportamento real

- Display: <APIs, ordem, falhas>
- Áudio: <endpoint, timing, restauração>
- HDR: <captura/reaplicação>
- Rescue: <journal, gatilho, decisão>

## Impacto na UI

- Novos estados: <lista>
- Novos erros: <códigos e textos sugeridos>
- Ajustes de layout: <lista>

## Limitações e riscos

- <limitação>
- <desconhecido>

## O que falta

- [ ] P7a: textos, i18n, a11y, screenshots.
- [ ] Ajustes visuais para estados reais.

## Evidências

- Relatório: `docs/rea-shiftpad-2.2.0.md`
- IDs: `ev_...`
- Testes de hardware: `docs/hardware-tests/`
```

## Regras

- Sem segredos, tokens ou dados pessoais.
- Caminhos absolutos quando necessários.
- Sempre incluir tag/commit.
- Sempre listar o que é real e o que é simulado.
- Sempre listar desconhecidos.
