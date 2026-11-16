# Fila de validação sequencial

- Data: 2026-11-16
- Regra: `AGENTS.md` — uma validação por vez, com evidência registrada antes
  da próxima; continuar automaticamente; pedir apenas ação física indispensável;
  parar em falha de rollback, tela preta ou watchdog que não restaura.
- Estado atual: build Debug/Release 0 avisos/0 erros; **286 testes**; smoke OK;
  candidato `1.0.11` instalado localmente; publicação do instalador pendente.

## Status das fases

| Fase | Estado | Evidência/limite |
|---|---|---|
| P0–P3 | Aceitas | Shell, componentes, simulação, journal e persistência |
| P4 display | **Aceita com limitação** | Hardware: TvOnly/Extend, rollback, falha/cancelamento, desconexão e kill após retorno do CCD; morte dentro de `SetDisplayConfig` não comprovada |
| P5 áudio | **Aceita com limitações** | Aceite do usuário em 11/11/2026; hardware: fluxo com controle simulado, troca/restore, falha, cancelamento, endpoint ausente, crash do filho de teste e rescue de produção; HDR adiado |
| P6 rescue/tray/autostart | Aceita em 13/11/2026 | Logon, bandeja, recuperação assistida e autostart verificados |
| P7a docs/a11y/screenshots | **Aceita em 13/11/2026** | A11y, i18n, UIAutomation, screenshots e notas concluídos; Narrator/NVDA manual pendente |
| P7b release | **Aceita** | Instalador em conta padrão, autostart, reinstalação, desinstalação e tag `v1.0.0` |

## Fila, em ordem

### 1. Watcher de gamepad físico

- **Nome no candidato 1.0.4 (D069):** seleção XInput preservada. O nome é associado ao slot pelo par fabricante/produto. Build Release 0/0, 267 testes e smoke passaram, incluindo IDs do Ultimate 2, Ultimate 2C e fallback. O controle estava desligado durante a consulta, então a identificação na interface aguarda confirmação física.

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
- **Itens 4–6 verificados:** instalação e remoção da tarefa no Agendador, disparo automático no logon com journal ausente, reabertura pela bandeja, desativação com restauração de tela/áudio na Samsung/AOC e saída em repouso. Build 0/0, 258 testes e smoke após a matriz.
- **Autostart verificado:** ambas as tarefas por usuário retornaram 0 no logon real e o App abriu pelo rescue. Build final 0/0, 259 testes e smoke.
- **Rescue real verificado:** logon com journal `pending` criado antes de apply e recuperação assistida pelo botão após interrupção de sessão `active` com Somente TV e áudio na Samsung.
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
- **Status:** aceita em 13/11/2026.
- **Concluído:** checklist de a11y, contraste, foco/nomes via UIAutomation nos
  3 breakpoints, i18n 157 chaves, fluxos de bandeja/autostart/recuperação
  revisados, 6 capturas oficiais em `docs/screenshots/`, README e notas de
  release atualizados.
- **Pendente:** Narrator/NVDA manual; interação do menu nativo da bandeja com
  leitor de tela; anúncio da contagem em atraso de 5 s pode ser verboso.
- **Evidência:** docs + capturas versionadas + build/testes/smoke.

### 7. P7b — release

- **Dono:** Codex.
- **Validado:** regressão Debug e Release, 259 testes por configuração, smoke, watchdog e rescue, instalação sem elevação, padrões de primeiro uso, bandeja, autostart, reinstalação e desinstalação em conta padrão.
- **Evidência:** `docs/install-validation.md`, SHA-256 do instalador, notas v1.0.0 e tag `v1.0.0`. Distribuição sem Authenticode (D067).

### 8. Correção de recuperação após mudança de LUID (1.0.11)

- **Dono:** DeepSeek/Codex na revisão.
- **Status:** corrigido e instalado como candidato local `1.0.11`; publicação pendente.
- **Implementação:** `DisplaySnapshotRehydrator` remapeia adapter/source do
  journal por `DevicePath`/EDID para as identidades atuais. Se a topologia
  reidratada já é o baseline ativo, o restore termina sem `SetDisplayConfig`.
- **Evidência:** journal real de 15/11/2026 com AOC `59140` reidratado para
  `59029` (`STRICT=False`, `REHYDRATED=True`); `DisplaySnapshotRehydratorTests`;
  build Debug/Release 0/0; 286 testes; smoke; instalação 1.0.11 sem elevação;
  abertura sem banner de recuperação; tasks de logon preservadas.
- **Pendente:** publicar o instalador 1.0.11 com SHA-256 e release notes;
  repetir a recuperação assistida em hardware em uma janela controlada, se
  necessário. Instalador local: 84.286.297 bytes, SHA-256
  `74605662FFD3437E78448AC2D0B300FBFA3B561D479E8A7D38F953715584A1EC`.

## Próxima ação

O instalador e o SHA-256 da `1.0.10` estão publicados na [release v1.0.10](https://github.com/matheusrmello/couchpad-windows/releases/tag/v1.0.10).
O candidato `1.0.11` corrige a reidratação do journal e está instalado
localmente, mas ainda não foi publicado. A reconexão do watcher durante a
contagem continua sem evidência independente; HDR e notificações nativas
seguem adiados.
