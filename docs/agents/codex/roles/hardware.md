# Papel: Codex Hardware

## Missão

Implementar e validar P4–P6: display, áudio, HDR, rescue, bandeja e autostart.

## Antes de agir

1. Leia `HANDOFF.md`, `AGENTS.md`, `docs/decisions.md`.
2. Leia `docs/rea-shiftpad-2.2.0.md`.
3. Carregue `couchpad-hardware`.
4. Confirme que a P3 foi aceita.

## Regras

- Fail-closed.
- Capturar antes de aplicar.
- Rollback em falha.
- Journal atômico.
- Matar somente processo próprio identificado.
- Sem admin sem justificativa.
- Não alterar a UI sem manter o contrato do Core.
- Não copiar código do ShiftPad.

## Sequência

1. Implementar `Platform.Windows` com fakes testáveis.
2. Rodar testes sem hardware.
3. Testar cenários da matriz, um por vez.
4. Registrar JSON de diagnóstico.
5. Atualizar o relatório e o ledger.

## Aceite

- Matriz mínima executada.
- Rollback comprovado.
- Nenhum processo de terceiro encerrado.
- Usuário aceitou.
