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
- **Retirada do receptor, 12/11/2026:** após o usuário remover o receptor, os quatro slots retornaram `ERROR_DEVICE_NOT_CONNECTED`; o journal estava ausente, o probe indicou `InternalOnly` com AOC ativo e TV inativa e os três papéis de áudio apontavam ao endpoint anterior do AOC. O usuário confirmou imagem normal no AOC e ausência da área de trabalho na Samsung. A duração do atraso e volume/mudo não foram medidos nesta observação.
- **Receptor reconectado com controle desligado, 12/11/2026:** usuário identificou o controle como 8BitDo Ultimate 2 e informou receptor reconectado com controle desligado; leitura mostrou os quatro slots desconectados, journal ausente e AOC ativo sem TV. Isso não demonstra que o receptor necessariamente mantém XInput ativo; medir desligamento com receptor conectado continua pendente.
- **Ativação por controle físico, 12/11/2026:** usuário ligou o Ultimate 2 com receptor conectado e confirmou Samsung com imagem e AOC apagado. Probe indicou `TvOnly`, TV ativa, monitor inativo; áudio Samsung nos três papéis e journal `active`. A duração da contagem não foi medida.
- **Desligamento com receptor conectado, 12/11/2026:** usuário desligou o controle e confirmou AOC normal e Samsung sem área de trabalho. Leitura posterior: quatro slots XInput desconectados, journal ausente, `InternalOnly`, AOC ativo e TV inativa; os três papéis de áudio, volume 1 e mudo corresponderam ao baseline capturado. A duração da contagem ainda não foi medida.
- **Tentativa de cancelamento, 12/11/2026:** usuário relatou que a contagem não apareceu. Consulta posterior mostrou XInput sem controle, journal ausente, AOC ativo, automação ligada e atraso de ativação configurado para 5 s; usuário informou receptor conectado e controle desligado. Resultado inconclusivo: a consulta posterior não prova se o controle chegou a ser detectado durante a tentativa.
- **Contagem de ativação, 12/11/2026:** com atraso configurado para 5 s e receptor conectado, o usuário ligou o controle, confirmou que viu a contagem e que a Samsung assumiu a imagem. Leitura posterior confirmou `TvOnly`, áudio Samsung nos três papéis e journal `active`. A duração exata não foi cronometrada.
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
