# Codex — perfis e uso

## O que é

O Codex não tem subagentes no estilo do OpenCode. Ele tem:

- **perfis** (`-p nome`) que aplicam um arquivo `$CODEX_HOME/<nome>.config.toml`;
- **sessões** independentes;
- **skills** em `~/.codex/skills/` com `SKILL.md` + `agents/openai.yaml`.

Os papéis são implementados por **perfil + prompt de papel + skill**.

## Instalação dos perfis

Copie os arquivos para `C:\Users\Usuario\.codex\`:

```text
rea.config.toml
hardware.config.toml
release.config.toml
review.config.toml
```

## Uso

```powershell
codex -p rea
codex -p hardware
codex -p release
codex -p review
```

Ao abrir, peça para ler o papel:

```text
Leia docs/agents/codex/roles/rea.md e atue conforme esse papel.
```

## Perfis

| Perfil | Modelo | Effort | Tarefa |
|---|---|---|---|
| `rea` | `gpt-6.1-terra` | `high` | REA 2.2.0 |
| `hardware` | `gpt-6.1-terra` | `high` | P4–P6 |
| `release` | `gpt-6.1-terra` | `medium` | P7b |
| `review` | `gpt-6.1-terra` | `medium` | revisão |

O perfil só altera modelo/effort; o restante vem do `config.toml` base.

## Papéis

Copie `roles/*.md` para `docs/agents/codex/roles/` no projeto.

Cada papel define missão, leitura obrigatória, regras, sequência e aceite.

## Skills no Codex

1. Copie as 5 skills para `C:\Users\Usuario\.codex\skills\`.
2. Em cada skill, crie `agents/openai.yaml`:

```yaml
interface:
  display_name: "CouchPad UI"
  short_description: "Padrões de UI do CouchPad em Avalonia"
  default_prompt: "Use $couchpad-ui para implementar a tela solicitada."
policy:
  allow_implicit_invocation: true
```

3. Ajuste `display_name`, `short_description` e `default_prompt` por skill.
4. Verifique com o comando de skills do Codex ou pedindo “liste as skills”.

## MCP do REA

O `config.toml` já tem `[mcp_servers.rea]` apontando para a instalação local.
Se o `doctor --client codex` marcar stale:

```powershell
npx -y rea-agents@latest setup --client codex --yes
```

## Regras de economia

- Use `rea`/`hardware` com `high` só quando necessário.
- `release`/`review` em `medium`.
- Uma sessão por fase; não misture UI e hardware.
- Leia o handoff, não a conversa antiga.
- Não cole evidência gigante; use IDs e resumos.
