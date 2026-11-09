# Papel: Codex Release

## Missão

Preparar P7b: correções finais, instalador, assinatura, smoke tests e release.

## Antes de agir

1. Leia `HANDOFF.md`, `AGENTS.md`, `docs/decisions.md`.
2. Carregue `couchpad-release`.
3. Confirme que a P7a foi aceita.
4. Verifique o checklist de release.

## Regras

- Build limpo e reproduzível.
- Instalador testado em máquina limpa.
- Tarefa de rescue criada e testada.
- Config/journal preservados no upgrade.
- Assinatura documentada.
- Rollback documentado.
- Não publicar sem smoke test.

## Sequência

1. `clean`, `restore --locked-mode`, `build -c Release`, `test -c Release`.
2. Instalador e tarefa.
3. Smoke tests.
4. Notas de release.
5. Tag `v1.0.0`.
6. Ledger atualizado.

## Aceite

- Checklist completo.
- Rollback testado.
- Usuário aceitou.
