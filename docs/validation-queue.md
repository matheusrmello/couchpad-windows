# Fila de validação sequencial

- Data: 2026-11-11
- Regra: `AGENTS.md` — uma validação por vez, com evidência registrada antes
  da próxima; continuar automaticamente; pedir apenas ação física indispensável;
  parar em falha de rollback, tela preta ou watchdog que não restaura.
- Estado atual: build 0 avisos/0 erros; **213 testes**; smoke OK; nenhuma tag
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
- **Status:** validação física em andamento; ativação, desligamento e cancelamento de ativação foram observados. A reconexão durante o atraso tem relato visual, mas carece de timestamps/eventos independentes e revisão da evidência. O journal foi limpo nas restaurações posteriores documentadas abaixo. O usuário dispensou trabalho adicional no botão de ativação em 12/11/2026; seu teste físico não é critério pendente.
- **Confirmação visual:** usuário informou AOC normal e Samsung sem imagem; controle mantido desconectado.
- **Investigação somente leitura:** XInput retornou sucesso no slot 0 e `ERROR_DEVICE_NOT_CONNECTED` nos slots 1–3. O Windows listou presente `Controlador XBOX 360 para Windows` (USB `VID_2DC8&PID_310B`); não está comprovado que corresponda ao controle desconectado. Isso pode explicar o journal ativo. Nenhum dispositivo foi alterado.
- **Retirada do receptor, 12/11/2026:** após o usuário remover o receptor, os quatro slots retornaram `ERROR_DEVICE_NOT_CONNECTED`; o journal estava ausente, o probe indicou `InternalOnly` com AOC ativo e TV inativa e os três papéis de áudio apontavam ao endpoint anterior do AOC. O usuário confirmou imagem normal no AOC e ausência da área de trabalho na Samsung. A duração do atraso e volume/mudo não foram medidos nesta observação.
- **Receptor reconectado com controle desligado, 12/11/2026:** usuário identificou o controle como 8BitDo Ultimate 2 e informou receptor reconectado com controle desligado; leitura mostrou os quatro slots desconectados, journal ausente e AOC ativo sem TV. Isso não demonstra que o receptor necessariamente mantém XInput ativo; medir desligamento com receptor conectado continua pendente.
- **Ativação por controle físico, 12/11/2026:** usuário ligou o Ultimate 2 com receptor conectado e confirmou Samsung com imagem e AOC apagado. Probe indicou `TvOnly`, TV ativa, monitor inativo; áudio Samsung nos três papéis e journal `active`. A duração da contagem não foi medida.
- **Desligamento com receptor conectado, 12/11/2026:** usuário desligou o controle e confirmou AOC normal e Samsung sem área de trabalho. Leitura posterior: quatro slots XInput desconectados, journal ausente, `InternalOnly`, AOC ativo e TV inativa; os três papéis de áudio, volume 1 e mudo corresponderam ao baseline capturado. A duração da contagem ainda não foi medida.
- **Tentativa de cancelamento, 12/11/2026:** usuário relatou que a contagem não apareceu. Consulta posterior mostrou XInput sem controle, journal ausente, AOC ativo, automação ligada e atraso de ativação configurado para 5 s; usuário informou receptor conectado e controle desligado. Resultado inconclusivo: a consulta posterior não prova se o controle chegou a ser detectado durante a tentativa.
- **Contagem de ativação, 12/11/2026:** com atraso configurado para 5 s e receptor conectado, o usuário ligou o controle, confirmou que viu a contagem e que a Samsung assumiu a imagem. Leitura posterior confirmou `TvOnly`, áudio Samsung nos três papéis e journal `active`. A duração exata não foi cronometrada.
- **Cancelamento da ativação, 12/11/2026:** usuário ligou o controle e retirou o receptor durante a contagem de 5 s; confirmou que o AOC permaneceu normal. Consulta posterior confirmou `InternalOnly`, journal ausente e áudio anterior nos três papéis, volume e mudo. A ausência de uma mudança transitória de tela tem confirmação visual do usuário.
- **Tentativa de reconexão durante desativação, 12/11/2026:** com atraso de desativação ajustado para 5 s, usuário informou retorno ao AOC. Após a tentativa, o receptor estava conectado e o controle desligado; leitura mostrou quatro slots desconectados, `InternalOnly` e journal ausente. Resultado inconclusivo para cancelamento: não houve evidência de reconexão XInput durante a contagem.
- **Repetição da reconexão, 12/11/2026:** usuário relatou retirada e reconexão do receptor durante a contagem, com Samsung mantendo imagem e controle reconectado. Leitura posterior confirmou slot XInput 0 conectado, `TvOnly`, áudio Samsung nos três papéis e journal `active`. Não foram capturados timestamps nem eventos XInput intermediários; o cancelamento efetivo durante o intervalo não está comprovado de forma independente.
- **Retorno após reconexão, 12/11/2026:** após desligar o controle, o usuário confirmou AOC normal e Samsung sem área de trabalho. Leitura confirmou `InternalOnly`, journal ausente e endpoint AOC nos três papéis com volume e mudo anteriores.
- **Validar:** reconexão durante o atraso com evidência independente e revisão
  dos cenários do controle; os cenários já observados estão descritos acima.
- **Evidência:** hardware + logs + estado/journal.

### 2. Launcher real

- **Dono:** Codex.
- **Implementado e validado:** Steam e Hydra Big Picture em hardware, restore de tela/áudio e preservação da instância Hydra preexistente. Steam próprio sai do Big Picture e permanece aberto, conforme escolha do usuário; instâncias preexistentes não são fechadas. Sem kill forçado.
- **Limites:** launcher ausente foi injetado com serviços reais de hardware e rescue; ausência física e detecção de caminho pelo backend não foram exercitadas. Hydra próprio permaneceu com processos ativos após saída visual. Evidências em `docs/launcher-validation.md`.
- **Evidência:** hardware + captura/estado.

### 3. P6 — rescue no logon, bandeja e autostart

- **Dono:** Codex.
- **Validação sem TV concluída:** instância única na janela real, coordenação de startup com rescue concorrente e nova tentativa assistida com backend falso. O iniciador de produção e o rescue assistido abriram o App com journal ausente. Evidências em `docs/p6-recovery-matrix.md`.
- **Validar:** journal pendente após reinício/logon; restore automático;
  integração de autostart por usuário; recuperação assistida com baseline real; sem admin.
- **Evidência:** teste de crash/reinício, matrix P6, tag `v0.6.0-hardware`.

### 4. Notificação nativa (adiada)

- **Status:** protótipo desativado por padrão, sem aceite. O SDK registrou a notificação na Central, mas o usuário não viu o banner; a identidade e as configurações do Windows exigem investigação adicional. A contagem na janela permanece ativa. Antes de habilitar, verificar comportamento do SDK, inclusive ausência de telemetria, e repetir teste visual de contagem/cancelamento.

### 5. HDR (adiado)

- **Dono:** futuro/Codex.
- **Validar:** detecção de suporte, captura, reaplicação e restore sem forçar
  HDR incompatível.
- **Evidência:** hardware; não contar como aprovado antes disso.

### 6. P7a — docs, a11y, screenshots

- **Dono:** DeepSeek.
- **Validar:** checklist de acessibilidade, screenshots 3 breakpoints × 2
  idiomas, i18n, release notes.
- **Evidência:** docs + capturas versionadas.

### 7. P7b — release

- **Dono:** Codex.
- **Validar:** regressão final, instalador, assinatura, tag `v1.0.0`.
- **Evidência:** build/test, instalador, smoke e notas.

## Próxima ação

Prosseguir com P6, preservando a pendência independente da reconexão do watcher.
As validações físicas serão executadas uma por vez, com evidência registrada
antes de avançar.
