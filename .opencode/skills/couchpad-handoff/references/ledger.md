# Ledger e registro de decisões

## Ledger de status

Arquivo: `HANDOFF.md`, seção **Ledger de status**.

| Fase | Dono | Status | Evidência/observação |
|---|---|---|---|
| Setup REA | OpenCode | pendente | — |
| REA 2.2.0 | OpenCode | pendente | — |
| P0 bootstrap | OpenCode | pendente | — |
| P1 shell | OpenCode | pendente | — |
| P2 componentes | OpenCode | pendente | — |
| P3 simulação | OpenCode | pendente | — |
| P4 display | OpenCode | pendente | — |
| P5 áudio/HDR | OpenCode | pendente | — |
| P6 rescue/tray | OpenCode | pendente | — |
| P7a docs/a11y | OpenCode | pendente | — |
| P7b release | OpenCode | pendente | — |

## Vocabulário de status

| Status | Significado |
|---|---|
| `pendente` | não iniciado |
| `em andamento` | iniciado, sem aceite |
| `bloqueado` | depende de decisão/hardware/evidência |
| `concluído` | implementado, aguardando revisão |
| `aceito` | revisado e aprovado pelo usuário |
| `cancelado` | fora de escopo, com motivo |

## Evidência mínima

Cada linha concluída deve ter pelo menos um:

- Comando de build/teste com resultado.
- Hash/tag de commit.
- Screenshot ou caminho de artefato.
- ID de evidência REA (`ev_...`).
- Link para `docs/` com a decisão.

Exemplo:

```md
| P3 simulação | OpenCode | aceito | commit `abc1234`, tag `v0.3.0-sim`; `dotnet test` 42/42; screenshots em `docs/screenshots/p3/` |
```

## Registro de decisões

Arquivo: `docs/decisions.md`.

```md
## D010 — Estratégia de identidade do TV

- Data: 2026-11-12
- Decisão: usar target device path + EDID quando disponível.
- Motivo: nome amigável não é estável entre reconexões.
- Impacto: DisplayService precisa capturar e comparar os dois.
- Evidência: ev_..., teste em hardware em 2026-11-15.
```

## Orçamento

Opcional, mas útil:

| Fase | Entrada prevista | Entrada real | Saída real |
|---|---:|---:|---:|
| P4 | 4–9M | — | — |
| P5 | 5–10M | — | — |

Se estourar 1,5×, registrar o motivo no handoff.

## Regras

- Uma linha por fase; não duplicar.
- Não apagar histórico; corrigir com nota.
- Datas em ISO (`AAAA-MM-DD`).
- Evidência sempre verificável.
