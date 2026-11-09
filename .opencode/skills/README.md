# Skills do CouchPad Windows

Pacote com as 5 skills recomendadas para o projeto. Cada skill tem um
`SKILL.md` curto e arquivos de referência carregados sob demanda.

| Skill | Dono | Quando carregar |
|---|---|---|
| `couchpad-ui` | DeepSeek | P0–P2, P7a: telas, tema, i18n, a11y |
| `couchpad-handoff` | Ambos | Fim de fase / troca OpenCode ↔ Codex |
| `couchpad-rea` | Codex | REA do ShiftPad 2.2.0 |
| `couchpad-hardware` | Codex | P4–P6: display, áudio, HDR, rescue |
| `couchpad-release` | Codex | P7b: instalador, assinatura, release |

## Instalação

### Projeto (recomendado)

Copie a pasta `skills` para:

```text
C:\Users\Usuario\Documents\couchpad windows\.opencode\skills\
```

Estrutura final:

```text
.opencode/skills/
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

## Codex

O Codex pode não carregar skills do OpenCode automaticamente. Por isso:

- O conteúdo canônico profundo fica em `docs/` (`ui-spec.md`,
  `rea-shiftpad-2.2.0.md`, `decisions.md`).
- As skills apenas apontam para esses arquivos.
- O Codex lê `AGENTS.md` + `HANDOFF.md` + os docs diretamente.

Mantenha uma única fonte de verdade: não copie o conteúdo dos docs para dentro da
skill; referencie.

## Ordem de criação no projeto

| Momento | Skills |
|---|---|
| P0 | `couchpad-ui`, `couchpad-handoff` |
| Antes do Codex | `couchpad-rea`, `couchpad-hardware` |
| Antes da P7b | `couchpad-release` |

## Regras

- `SKILL.md` curto; detalhe em `references/`.
- Descrição com gatilho claro (“Use ao…”).
- Sem segredos, tokens ou caminhos voláteis.
- Não duplicar `AGENTS.md`.
- Não criar skill para tarefa genérica de C#/Avalonia.
