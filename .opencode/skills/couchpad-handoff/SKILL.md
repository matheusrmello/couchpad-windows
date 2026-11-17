---
name: couchpad-handoff
description: Use ao terminar uma fase ou pausar o CouchPad. Garante ledger atualizado, tag, documentação de fechamento e continuidade sem retrabalho.
---

# CouchPad Handoff

## Quando usar

- Ao terminar qualquer fase do roadmap.
- Antes de criar uma tag de fase.
- Quando o trabalho for pausado por mais de um dia.
- Antes de entregar o estado para o usuário ou para uma nova sessão.

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

### 4. Escrever o fechamento

- Não há mais troca de agente: o OpenCode é o único desenvolvedor.
- Registre o fechamento em `docs/handoff-<fase>.md` quando o usuário pedir um
  documento separado; caso contrário, o ledger em `HANDOFF.md` é suficiente.
- Use o template em `references/handoff-template.md` como referência de
  identidade, contratos, evidências e pendências.

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

A próxima sessão deve ler, nesta ordem:

1. `HANDOFF.md` (ledger, decisões, pendências).
2. `AGENTS.md` (regras sempre ativas).
3. `docs/decisions.md`.
4. O fechamento da fase anterior, se existir.
5. `docs/rea-shiftpad-2.2.0.md`, se for fase de hardware.

## Regras de não-retrabalho

- Não refazer o que já está marcado como `concluído` sem evidência de regressão.
- Não mudar interface do Core sem documentar e manter a UI compilando.
- Não violar a fronteira de camadas: `App` sem P/Invoke/COM, `Core` sem
  Avalonia/Win32, `Platform.Windows` sem regra de produto.
- Não reabrir decisões travadas.
- Se faltar contexto, ler os arquivos acima antes de perguntar.
- Se uma decisão nova for inevitável, registrar e pedir confirmação.

## Fronteira de camadas

| Área | Regra |
|---|---|
| `src/CouchPad.App/**` | UI, ViewModels, i18n; sem P/Invoke/COM |
| `src/CouchPad.Core/**` | Modelos, interfaces, simulação, state machine; sem Avalonia/Win32 |
| `src/CouchPad.Platform.Windows/**` | Implementação Windows; sem regra de produto/texto de UI |
| `analysis/**`, `docs/rea-*` | Evidência estática do ShiftPad |
| `docs/decisions.md`, `HANDOFF.md` | Append-only; decisões e ledger |

Todas as áreas pertencem ao OpenCode. Mudanças em `Platform.Windows` exigem
build/testes e validação de hardware quando aplicável.
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
