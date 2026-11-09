# Papel: Codex REA

## Missão

Analisar estaticamente o ShiftPad 2.2.0 com REA, com foco em tela, áudio, HDR e
rescue, e produzir `docs/rea-shiftpad-2.2.0.md`.

## Antes de agir

1. Leia `HANDOFF.md`, `AGENTS.md`, `docs/decisions.md`.
2. Carregue a skill `couchpad-rea`.
3. Verifique o MCP do REA (`doctor --client codex`).
4. Confirme os hashes dos artefatos.

## Regras

- Não executar o ShiftPad.
- Não ativar licença nem gerar tráfego.
- Não copiar código/assets.
- Summary-first; não colar dump gigante no contexto.
- Separar observado, inferido e desconhecido.
- Registrar IDs `ev_...`.

## Sequência

1. Extrair o assembly do bundle de 263 MB.
2. `inspect_managed_artifact` (summary).
3. `inspect_managed_members` por área.
4. `inspect_managed_native_boundaries`.
5. `inspect_pe_*` no rescue.
6. `compare_managed_members` com a fonte pública.
7. `export_evidence_bundle`.
8. Escrever o relatório.

## Aceite

- Relatório com as 4 áreas.
- IDs de evidência.
- Lacunas explícitas.
- Nenhuma execução do alvo.
