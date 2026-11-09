---
name: couchpad-handoff
description: Use ao terminar uma fase ou trocar entre OpenCode/DeepSeek e Codex. Garante ledger atualizado, tag, documentação de handoff e continuidade sem retrabalho.
---

# CouchPad Handoff

## Quando usar

- Ao terminar P0, P1, P2, P3, P6, P7a ou P7b.
- Antes de trocar de agente (DeepSeek ↔ Codex).
- Antes de criar uma tag de fase.
- Quando o trabalho for pausado por mais de um dia.

## Quando NÃO usar

- No meio de uma tarefa pequena, sem troca de fase.
- Para substituir `AGENTS.md` ou `docs/decisions.md`.

## Antes de qualquer handoff

1. `dotnet build` passa.
2. `dotnet test` passa.
3. Nenhuma alteração não commitada.
4. Ledger atualizado.
5. Decisões novas registradas.
6. Riscos e desconhecidos escritos.

## Procedimento

### 1. Fechar a fase

- Rodar build e testes; guardar o resumo.
- Gerar screenshots, se for fase de UI.
- Verificar se o escopo da fase foi cumprido.
- Listar o que ficou de fora, com motivo.

### 2. Atualizar o ledger

- Arquivo: `HANDOFF.md`, seção **Ledger de status**.
- Marcar a fase como `concluída` ou `aceita`.
- Registrar evidência: comando, hash, screenshot, teste.
- Atualizar a próxima fase para `pendente` ou `em andamento`.
- Formato em `references/ledger.md`.

### 3. Registrar decisões

- Nova decisão → `docs/decisions.md`.
- Formato: data, ID, decisão, motivo, impacto.
- Não reabrir D001–D009 sem pedido explícito.

### 4. Escrever o handoff

- DeepSeek → Codex: `docs/handoff-deepseek-to-codex.md`.
- Codex → DeepSeek: `docs/handoff-codex-to-deepseek.md`.
- Use o template em `references/handoff-template.md`.

### 5. Commit e tag

```powershell
git add -A
git commit -m "feat(P3): simulação e testes"
git tag v0.3.0-sim
git push --tags
```

Tags previstas:

| Fase | Tag |
|---|---|
| P0–P3 | `v0.3.0-sim` |
| P4–P6 | `v0.6.0-hardware` |
| P7a | `v0.9.0-rc` |
| P7b | `v1.0.0` |

### 6. Verificar a árvore

```powershell
git status --short
git log --oneline -5
```

A árvore deve estar limpa antes da troca.

## Leitura ao chegar

O próximo agente deve ler, nesta ordem:

1. `HANDOFF.md` (ledger, decisões, pendências).
2. `AGENTS.md` (regras sempre ativas).
3. `docs/decisions.md`.
4. O handoff da fase anterior.
5. `docs/rea-shiftpad-2.2.0.md`, se for fase de hardware.

## Regras de não-retrabalho

- Não refazer o que já está marcado como `concluído` sem evidência de regressão.
- Não mudar interface do Core sem documentar e manter a UI compilando.
- Não alterar arquivos de outra faixa (DeepSeek não toca em `Platform.Windows`).
- Não reabrir decisões travadas.
- Se faltar contexto, ler os arquivos acima antes de perguntar.
- Se uma decisão nova for inevitável, registrar e pedir confirmação.

## Fronteira entre agentes

| Área | Dono |
|---|---|
| `src/CouchPad.App/**` | DeepSeek |
| `src/CouchPad.Core/**` (interfaces/simulação) | DeepSeek |
| `src/CouchPad.Platform.Windows/**` | Codex |
| `analysis/**`, `docs/rea-*` | Codex |
| `docs/ui-spec.md`, `docs/ui-a11y-checklist.md` | DeepSeek |
| `docs/decisions.md`, `HANDOFF.md` | Ambos (append-only) |

## Erros comuns

- Handoff sem tag ou com árvore suja.
- Ledger desatualizado.
- Handoff que só lista arquivos, sem contratos e pendências.
- Próximo agente começar sem ler o handoff.
- Reabrir decisão sem registrar.
- Deixar TODO sem caminho de arquivo.

## Referências

- `references/ledger.md` — formato do ledger e do log de decisões.
- `references/handoff-template.md` — templates dos dois sentidos.
- `HANDOFF.md` — fonte de verdade do projeto.
