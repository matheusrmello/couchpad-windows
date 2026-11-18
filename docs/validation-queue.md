# Fila de validação sequencial

- Data: 2026-11-18
- Regra: `AGENTS.md` — uma validação por vez, com evidência registrada antes
  da próxima; continuar automaticamente; pedir apenas ação física indispensável;
  parar em falha de rollback, tela preta ou watchdog que não restaura.
- Estado atual: build Debug/Release 0 avisos/0 erros; **295 testes**; smoke OK;
  candidato `1.0.20` instalado, sem HUD (chord e FPS removidos por D081);
  publicação pendente.

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

- **Dono:** OpenCode.
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

- **Dono:** OpenCode.
- **Implementado e validado:** Steam e Hydra Big Picture em hardware, restore de tela/áudio e preservação da instância Hydra preexistente. Steam próprio sai do Big Picture e permanece aberto, conforme escolha do usuário; instâncias preexistentes não são fechadas. Sem kill forçado.
- **Limites:** launcher ausente foi injetado com serviços reais de hardware e rescue; ausência física e detecção de caminho pelo backend não foram exercitadas. Hydra próprio permaneceu com processos ativos após saída visual. Evidências em `docs/launcher-validation.md`.
- **Evidência:** hardware + captura/estado.

### 3. P6 — rescue no logon, bandeja e autostart

- **Dono:** OpenCode.
- **Validação sem TV concluída:** instância única na janela real, coordenação de startup com rescue concorrente e nova tentativa assistida com backend falso. O iniciador de produção e o rescue assistido abriram o App com journal ausente. Evidências em `docs/p6-recovery-matrix.md`.
- **Itens 4–6 verificados:** instalação e remoção da tarefa no Agendador, disparo automático no logon com journal ausente, reabertura pela bandeja, desativação com restauração de tela/áudio na Samsung/AOC e saída em repouso. Build 0/0, 258 testes e smoke após a matriz.
- **Autostart verificado:** ambas as tarefas por usuário retornaram 0 no logon real e o App abriu pelo rescue. Build final 0/0, 259 testes e smoke.
- **Rescue real verificado:** logon com journal `pending` criado antes de apply e recuperação assistida pelo botão após interrupção de sessão `active` com Somente TV e áudio na Samsung.
- **Evidência:** teste de crash/reinício, matrix P6, tag `v0.6.0-hardware`.

### 4. Notificação nativa (adiada)

- **Status:** protótipo desativado por padrão, sem aceite. O SDK registrou a notificação na Central, mas o usuário não viu o banner; a identidade e as configurações do Windows exigem investigação adicional. A contagem na janela permanece ativa. Antes de habilitar, verificar comportamento do SDK, inclusive ausência de telemetria, e repetir teste visual de contagem/cancelamento.

### 5. HDR (adiado)

- **Dono:** futuro/OpenCode.
- **Validar:** detecção de suporte, captura, reaplicação e restore sem forçar
  HDR incompatível.
- **Evidência:** hardware; não contar como aprovado antes disso.

### 6. P7a — docs, a11y, screenshots

- **Dono:** OpenCode.
- **Status:** aceita em 13/11/2026.
- **Concluído:** checklist de a11y, contraste, foco/nomes via UIAutomation nos
  3 breakpoints, i18n 157 chaves, fluxos de bandeja/autostart/recuperação
  revisados, 6 capturas oficiais em `docs/screenshots/`, README e notas de
  release atualizados.
- **Pendente:** Narrator/NVDA manual; interação do menu nativo da bandeja com
  leitor de tela; anúncio da contagem em atraso de 5 s pode ser verboso.
- **Evidência:** docs + capturas versionadas + build/testes/smoke.

### 7. P7b — release

- **Dono:** OpenCode.
- **Validado:** regressão Debug e Release, 259 testes por configuração, smoke, watchdog e rescue, instalação sem elevação, padrões de primeiro uso, bandeja, autostart, reinstalação e desinstalação em conta padrão.
- **Evidência:** `docs/install-validation.md`, SHA-256 do instalador, notas v1.0.0 e tag `v1.0.0`. Distribuição sem Authenticode (D067).

### 8. Correção de recuperação após mudança de LUID (1.0.11)

- **Dono:** OpenCode.
- **Status:** corrigido e instalado como candidato local `1.0.11`; publicação pendente.
- **Implementação:** `DisplaySnapshotRehydrator` remapeia adapter/source do
  journal por `DevicePath`/EDID para as identidades atuais. Se a topologia
  reidratada já é o baseline ativo, o restore termina sem `SetDisplayConfig`.
- **Evidência:** journal real de 15/11/2026 com AOC `59140` reidratado para
  `59029` (`STRICT=False`, `REHYDRATED=True`); `DisplaySnapshotRehydratorTests`;
  build Debug/Release 0/0; 286 testes na época; smoke; instalação 1.0.11 sem
  elevação; abertura sem banner de recuperação; tasks de logon preservadas.
- **Pendente:** publicar o instalador 1.0.11/1.0.12 com SHA-256 e release notes;
  repetir a recuperação assistida em hardware em uma janela controlada, se
  necessário. Instalador local 1.0.11: 84.286.297 bytes, SHA-256
  `74605662FFD3437E78448AC2D0B300FBFA3B561D479E8A7D38F953715584A1EC`.

### 9. Perfis de ambiente (1.0.12)

- **Dono:** OpenCode.
- **Status:** implementado e instalado localmente; publicação pendente.
- **Implementação:** `AppSettings.ActiveProfileId` + `EnvironmentProfiles`;
  migração de configurações antigas para perfil padrão; seletor, criação,
  renomeação e exclusão na tela de Display; espelhamento do perfil ativo nos
  campos de hardware de startup.
- **Evidência:** `JsonAppSettingsStoreTests`, `EnvironmentProfileTests`,
  troca Sala → Quarto via UIAutomation com escritas de hardware desligadas no
  build e no executável instalado; 6 capturas atualizadas; build/testes/smoke.
- **Pendente:** publicar o instalador 1.0.12 com SHA-256 e release notes.
  Instalador local: 84.294.096 bytes, SHA-256
  `E5DE69BFBFCB7CA8EC5F513B1070007C8D8B819F31E642C50C36C2523DA6F84A`.

### 10. Diagnóstico da identificação (1.0.13)

- **Dono:** OpenCode.
- **Status:** implementado e instalado localmente; publicação pendente.
- **Implementação:** expander no card Display com perfil, TV, monitor e áudio;
  EDID, HDMI, ativo/disponível, motivo da escolha e confiança alta/média/baixa.
  Nenhuma escrita de display/áudio.
- **Evidência:** `HardwareDiagnosisTests`; build/testes/smoke; expander aberto
  por UIAutomation no executável instalado. No hardware real, o diagnóstico
  mostrou TV e monitor com confiança alta nos displays e áudio ambíguo com duas
  saídas Samsung, sem selecionar nem escrever. Instalador local: 84.283.279
  bytes, SHA-256 `109DE17A9AF1E24AAA1000DB373B95EAB28B20C4D4F20BFC7EB707349722F373`.

### 11. HUD de desempenho — Fase 1 (1.0.14)

- **Dono:** OpenCode.
- **Status:** implementado, instalado e validado em simulação/produção; chord
  físico validado em 1.0.18 (item 15); aceite pendente. **Removido na 1.0.20
  (D081).**
- **Implementação:** HUD Avalonia click-through/topmost/sem foco; sampler
  Windows via PDH/GetSystemTimes/GlobalMemoryStatusEx; XInput button source;
  chord LB+RB+Y; modal de métricas/aparência; settings persistidos.
- **Evidência:** build Debug/Release 0/0, **309 testes**, smoke; modal aberto e
  salvo por clique real; HUD oculto antes da sessão e visível após automação
  simulada; sampler real mostrou CPU/GPU/RAM/VRAM/processo/resolução; estilos
  `WS_EX_TRANSPARENT`, `WS_EX_NOACTIVATE` e `WS_EX_TOOLWINDOW` confirmados;
  HUD aberto no executável instalado 1.0.14, com tasks preservadas.
- **Chord físico:** validado em 1.0.18 no controle físico (8BitDo Ultimate 2,
  XInput, slot 0), no build instalado: o log do monitor XInput mostrou
  `0x8300 decoded=Y,LB,RB` segurado por ~2,7 s e o HUD sumiu e voltou. Ver
  item 15.
- **Pendente:** publicação. FPS/frame time/temperatura ficam para as próximas
  fases. Instalador local: 84.337.865 bytes, SHA-256
  `A745068F7A6FA4D3A8025A706C8292B90C73D50F7EC31E35C53BAED89D7032E0`.

### 12. Correção das barras de GPU/VRAM do HUD (1.0.15)

- **Dono:** OpenCode.
- **Status:** corrigido, instalado e validado no executável 1.0.15; publicação
  pendente. **Removido na 1.0.20 (D081).**
- **Causa:** o XAML do HUD e do modal usava `{DynamicResource CouchPad.Accent2}`,
  token que não existe em `Themes/Tokens.axaml`. O recurso não resolvia e a
  barra ficava invisível.
- **Implementação:** GPU/VRAM passam a usar `CouchPad.Info`; barras de
  percentual e proporção recebem largura mínima de 2 px para valores baixos.
- **Evidência:** build Debug/Release 0/0, **310 testes**, smoke; captura do HUD
  real mostrando CPU 8%, GPU 1% e barras desenhadas. Novo teste
  `Dynamic_theme_resources_used_in_xaml_exist_in_tokens` cobre a regressão.
- **Instalação:** executável 1.0.15 validado; HUD real com GPU 0% e modo
  simulação com GPU 74%/VRAM 7,2 GB exibindo as barras azuis. Tasks
  preservadas, journal ausente, `settings.json` restaurado por hash.
  Instalador: 84.333.243 bytes, SHA-256
  `9DEF26147CD7B2DF66715B448B7C9E7B69BC328D605B7AB7B9A2B016D8748A6B`.
- **Pendente:** publicar. Chord validado em 1.0.18 (item 15).

### 13. Redesenho do shell com card HUD (1.0.16)

- **Dono:** OpenCode.
- **Status:** implementado, instalado e validado em capturas; aceite pendente.
  **Removido na 1.0.20 (D081).**
- **Implementação:** cabeçalho com logo em bloco, regra de acento e status;
  card HUD em linha inteira na primeira tela com toggle, configurar, estado,
  posição, atalho, sessão, chips e prévia; cards reorganizados; ordem de foco
  por breakpoint; tokens de HUD compartilhados; `HudText` centraliza chaves.
- **Evidência:** build Debug/Release 0/0, **310 testes**, smoke; capturas
  `p7a-*` atualizadas a partir do executável instalado em 3 colunas (1020),
  2 (780) e 1 (640) nos dois idiomas.
- **Instalação:** 1.0.16 validada; toggle do card criou o overlay `Desempenho`
  e persistiu `performanceHudEnabled`; `settings.json` restaurado por hash,
  tasks `Ready` e journal ausente. Instalador: 84.339.291 bytes, SHA-256
  `C4D587C97ED53011CDF9BBDC2DF75F43FA947CDCFEC9B46497EFFEFD6971809B`.
- **Pendente:** publicar; chord validado no item 15.

### 14. FPS via RTSS (Fase 2)

- **Dono:** OpenCode.
- **Status:** validado com RTSS real; implementação, correções de leitura e
  validação concluídas. **Removido na 1.0.20 (D081).**
- **Implementação:** `IFrameRateSource` no Core; `RtssFrameRateSource`
  (P/Invoke kernel32 para `RTSSSharedMemoryV2`) e `RtssSharedMemoryParser`
  (puro) em `Platform.Windows`, integrados ao `WindowsPerformanceSampler`.
  Métrica opt-in, fora dos defaults; leitura read-only, sem injeção, driver,
  admin ou encerramento de processos. Sem RTSS ou sem frames, o HUD mostra `—`.
  A fonte usa a média de janela (`dwFrames`/`dwTime0..dwTime1`) e teto de
  999 FPS; a leitura usa o bloco local `RTSSSharedMemoryV2` (o `Global\` é
  negado nesta máquina e o fallback cobre). UI no overlay, modal e card, com
  i18n pt-BR/en.
- **Evidência:** build Debug/Release 0/0, **332 testes**, smoke; simulação com
  overlay em 107 FPS e card em 144 FPS; validação real com Silent Hill 2
  (`SHProto-Win64-Shipping`, D3D12, PID 19248 em foreground): 40/40 amostras
  numéricas, sem `null` e sem outliers de ~1 FPS, com o FPS acompanhando o jogo
  (run 1 48,2–49,2; runs seguintes 35,9–39,4 conforme variação de cena/GPU do
  próprio jogo). Correções aplicadas: layout real da entrada (sem dwFlags;
  time0@268, time1@272, frames@276), teto de cópia 16 MiB e média de janela.
  `RtssSharedMemoryParserTests` e `PerformanceFrameRateTests` cobrem o parser e
  o contrato.
- **Decisão:** D080.

### 15. Correção do chord do HUD (1.0.18)

- **Dono:** OpenCode.
- **Status:** corrigido, instalado e validado no controle físico; publicação
  pendente. **Removido na 1.0.20 (D081).**
- **Causa:** `WindowsGamepadButtonSource` só publicava o evento quando o
  conjunto de botões mudava. O detector de chord depende do estado contínuo para
  medir o hold de 800 ms, então recebia um único evento e nunca confirmava
  `LB+RB+Y`.
- **Correção:** o contrato passou a reportar o estado a cada poll (~30 Hz) em
  `IGamepadButtonSource.ButtonsReported`; a detecção de borda/hold fica no
  consumidor. Teste de regressão cobre o hold.
- **Evidência:** build Debug/Release 0/0, **333 testes** (283 Core + 50 App) e
  smoke; no build `1.0.18` instalado, o controle físico (8BitDo Ultimate 2,
  XInput, slot 0) apareceu no log do monitor XInput como
  `0x8300 decoded=Y,LB,RB` segurado por ~2,7 s, e o HUD sumiu e voltou com o
  chord.
- **Pendente:** publicar.

## Próxima ação

O instalador e o SHA-256 da `1.0.10` estão publicados na [release v1.0.10](https://github.com/matheusrmello/couchpad-windows/releases/tag/v1.0.10).
A `1.0.20` está instalada e com a publicação pendente. Ela remove o HUD de
monitoramento (overlay, modal, card, chord e FPS via RTSS) conforme D081, e o
shell volta a 8 cards. O foco de validação passa a ser a automação:

- ativação e restauração de display e áudio com o controle físico;
- journal: `pending` antes do apply, `active` depois, e limpeza ao restaurar;
- rescue no logon e recuperação assistida quando o journal fica `active`.

O gate atual é build Debug/Release 0/0, **295 testes** e smoke OK. Falta
publicar o instalador 1.0.20 (gh ausente; usar a UI web). A reconexão do watcher
durante a contagem continua sem evidência independente; HDR e notificações
nativas seguem adiados.
