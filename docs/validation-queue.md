# Fila de validação sequencial

- Data: 2026-11-11
- Regra: `AGENTS.md` — uma validação por vez, com evidência registrada antes
  da próxima; continuar automaticamente; pedir apenas ação física indispensável;
  parar em falha de rollback, tela preta ou watchdog que não restaura.
- Estado atual: build 0 avisos/0 erros; **197 testes**; smoke OK; nenhuma tag
  de hardware criada.

## Status das fases

| Fase | Estado | Evidência/limite |
|---|---|---|
| P0–P3 | Aceitas | Shell, componentes, simulação, journal e persistência |
| P4 display | **Aceita com limitação** | Hardware: TvOnly/Extend, rollback, falha/cancelamento, desconexão e kill após retorno do CCD; morte dentro de `SetDisplayConfig` não comprovada |
| P5 áudio | **Aceita com limitações** | Aceite do usuário em 11/11/2026; hardware: fluxo com controle simulado, troca/restore, falha, cancelamento, endpoint ausente, crash do filho de teste e rescue de produção; HDR adiado |
| P6 rescue/tray/autostart | Pendente | Rescue/watchdog existe; logon, bandeja e autostart pendentes |
| P7a docs/a11y/screenshots | Pendente | Depende da P6 |
| P7b release | Pendente | Depende da P7a |

## Fila, em ordem

### 1. Watcher de gamepad físico

- **Dono:** Codex.
- **Implementação:** `WindowsGamepadWatcher` via XInput, polling dos quatro slots; ativação opt-in com `--windows-gamepad`; a simulação segue padrão. Build 0/0, smoke OK e 197 testes aprovados. Dispositivos HID sem XInput estão fora deste slice (D050).
- **Status:** validação física interrompida; não aprovado. Em 12/11/2026, a UI reconheceu `XInput gamepad`. Após o usuário informar a desconexão, a observação foi interrompida; na consulta seguinte o display estava `InternalOnly` (AOC ativo, TV inativa) e os três papéis de áudio apontavam ao AOC, mas o journal ainda estava `active` com baseline AOC. Não há evidência de limpeza nem de que o watcher causou a restauração. Preservar o journal e investigar antes de qualquer nova mudança física.
- **Confirmação visual:** usuário informou AOC normal e Samsung sem imagem; controle mantido desconectado.
- **Investigação somente leitura:** XInput retornou sucesso no slot 0 e `ERROR_DEVICE_NOT_CONNECTED` nos slots 1–3. O Windows listou presente `Controlador XBOX 360 para Windows` (USB `VID_2DC8&PID_310B`); não está comprovado que corresponda ao controle desconectado. Isso pode explicar o journal ativo. Nenhum dispositivo foi alterado.
- **Validar:** conectar/desconectar controle real; contagem configurável;
  cancelar atraso; ativar/desativar; ausência de controle; reconexão.
- **Evidência:** hardware + logs + estado/journal.

### 2. Launcher real

- **Dono:** Codex.
- **Validar:** abrir Steam Big Picture ou caminho customizado; fechar ao
  desativar; falha de caminho; não afetar processos de terceiros.
- **Evidência:** hardware + captura/estado.

### 3. P6 — rescue no logon, bandeja e autostart

- **Dono:** Codex.
- **Validar:** journal pendente após reinício/logon; restore automático;
  bandeja; autostart por usuário; instância única/concorrência; sem admin.
- **Evidência:** teste de crash/reinício, matrix P6, tag `v0.6.0-hardware`.

### 4. HDR (adiado)

- **Dono:** futuro/Codex.
- **Validar:** detecção de suporte, captura, reaplicação e restore sem forçar
  HDR incompatível.
- **Evidência:** hardware; não contar como aprovado antes disso.

### 5. P7a — docs, a11y, screenshots

- **Dono:** DeepSeek.
- **Validar:** checklist de acessibilidade, screenshots 3 breakpoints × 2
  idiomas, i18n, release notes.
- **Evidência:** docs + capturas versionadas.

### 6. P7b — release

- **Dono:** Codex.
- **Validar:** regressão final, instalador, assinatura, tag `v1.0.0`.
- **Evidência:** build/test, instalador, smoke e notas.

## Próxima ação

A próxima validação é o **watcher de gamepad físico**. As seguintes exigem ação
física (controle real, launcher, logon) e serão executadas uma por vez, com
evidência registrada antes de avançar.
