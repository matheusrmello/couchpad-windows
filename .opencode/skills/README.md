# Skills do CouchPad Windows

Pacote com as 6 skills do projeto. Cada skill tem um `SKILL.md` curto e
arquivos de referência carregados sob demanda.

| Skill | Dono | Quando carregar |
|---|---|---|
| `couchpad-uiux` | OpenCode | UI/UX: direção visual, HUD, fluxos, a11y, revisão |
| `couchpad-ui` | OpenCode | P0–P2, P7a: implementação de telas, tema, i18n, a11y |
| `couchpad-handoff` | OpenCode | Fim de fase / pausa / continuidade |
| `couchpad-rea` | OpenCode | REA do ShiftPad 2.2.0 |
| `couchpad-hardware` | OpenCode | P4–P6: display, áudio, HDR, rescue |
| `couchpad-release` | OpenCode | P7b: instalador, assinatura, release |

## Instalação

### Projeto (recomendado)

Copie a pasta `skills` para:

```text
C:\Users\Usuario\Documents\couchpad windows\.opencode\skills\
```

Estrutura final:

```text
.opencode/skills/
  couchpad-uiux/SKILL.md
  couchpad-ui/SKILL.md
  couchpad-handoff/SKILL.md
  couchpad-rea/SKILL.md
  couchpad-hardware/SKILL.md
  couchpad-release/SKILL.md
```

### Global (todos os projetos)

```text
C:\Users\Usuario\.config\opencode\skills\
```

## Verificação

1. Abra o OpenCode no projeto.
2. Peça: “liste as skills disponíveis” ou “use a skill `couchpad-ui`”.
3. A skill deve aparecer com nome e descrição.
4. Se não aparecer, confira o caminho e o `name` no frontmatter.

## OpenCode

O OpenCode carrega estas skills do projeto. O conteúdo canônico profundo fica
em `docs/` (`ui-spec.md`, `rea-shiftpad-2.2.0.md`, `decisions.md`). As skills
apenas apontam para esses arquivos.

Mantenha uma única fonte de verdade: não copie o conteúdo dos docs para dentro da
skill; referencie. O Codex foi descontinuado em 2026-11-17 (D079).

## Ordem de criação no projeto

| Momento | Skills |
|---|---|
| P0 | `couchpad-ui`, `couchpad-handoff` |
| Antes de REA/hardware | `couchpad-rea`, `couchpad-hardware` |
| Antes da P7a/P7b | `couchpad-uiux`, `couchpad-release` |

## Regras

- `SKILL.md` curto; detalhe em `references/`.
- Descrição com gatilho claro (“Use ao…”).
- Sem segredos, tokens ou caminhos voláteis.
- Não duplicar `AGENTS.md`.
- Não criar skill para tarefa genérica de C#/Avalonia.
