# Validação da distribuição v1.0.0

O script `packaging/Build-Installer.ps1` mantém o artefato no projeto e também copia o instalador para a pasta Downloads da conta que executa o build. A cópia é conferida por SHA-256. Arquivos com o mesmo nome e conteúdo diferente recebem um sufixo numérico. Use `-SkipDownloadsCopy` em builds automatizados que só precisam do artefato do projeto.

## Correção da desinstalação com uma cópia de desenvolvimento

- No perfil principal, o desinstalador 1.0.0 abortava porque as tarefas de rescue e autostart apontavam para os binários de desenvolvimento. O journal estava ausente.
- O hook de desinstalação agora preserva tarefas com outro caminho de executável e entradas antigas pertencentes a outra instalação. Tarefas próprias continuam sujeitas à validação da definição. A instalação continua recusando sobrescrever tarefas existentes de outra cópia.
- Build Release com 0 avisos e 0 erros, 261 testes e smoke aprovados. O hook corrigido retornou 0 na instalação afetada e a comparação do XML confirmou que as duas tarefas de desenvolvimento ficaram intactas. A conclusão pelo desinstalador gráfico ainda precisa ser confirmada.
- O operador confirmou a desinstalação e reproduziu o conflito ao instalar novamente o EXE publicado da 1.0.0. O instalador antigo encontrou a tarefa de desenvolvimento somente após copiar arquivos. O perfil tinha journal ausente e tarefas de rescue e autostart apontando para o checkout de desenvolvimento.
- O candidato 1.0.1 executa uma verificação autocontida antes de copiar os arquivos. Com as tarefas de desenvolvimento presentes, o helper retornou 3 e o instalador completo retornou 7 com a mensagem específica de conflito. Os hashes dos arquivos no destino permaneceram iguais.
- Depois de guardar as definições das duas tarefas de desenvolvimento, os registros foram removidos para permitir a instalação no perfil principal. O instalador concluiu com código 0, registrou 1.0.1 e criou a tarefa de recuperação apontando para o rescue instalado. O rescue retornou 0 com journal ausente e o App instalado passou no smoke em simulação. Autostart do App pode ser habilitado novamente na interface. A publicação do patch fica pendente.
- Build Release 0/0, 261 testes e smoke passaram. SHA-256 do candidato 1.0.1: `30C0D83D212A50903FF69D7395E1FAB4CA773881379D7923F2744B56C131C861`.
- O operador desinstalou a 1.0.1 pelo Windows. A consulta confirmou ausência da pasta do programa, atalho, entrada de desinstalação, processos e tarefas de rescue e autostart. O `settings.json` permaneceu no perfil e o journal estava ausente.
- O operador confirmou a reinstalação a partir da cópia de Downloads. A consulta confirmou versão 1.0.1, atalho correto e tarefa de recuperação habilitada com o caminho do rescue instalado. Depois de abrir, a automação preservada ativou Somente TV e o áudio na Samsung nos três papéis, com journal `active` e os baselines de tela e áudio presentes. A confirmação de restauração após esta sessão está pendente.

## Regressão de entrada

- Base `6bf07b5`, árvore limpa. O diff desde `v0.6.0-hardware` confirmou que a P7a preservou o Core, o backend Windows e o rescue.
- Em 13/11/2026, builds isolados Debug e Release com 0 avisos e 0 erros. Cada configuração passou 259 testes (232 Core e 27 App) e smoke.
- Passaram os testes de processo de startup e watchdog com filho falso e rescue com backend falso, incluindo concorrência, falhas, timeout, recuperação assistida e morte do processo pai.
- A UI aceita na P7a é a referência congelada desta distribuição.

## Instalador validado

- Inno Setup 6.7.4, instalação per-user com `PrivilegesRequired=lowest`, App e rescue autocontidos para `win-x64`. O pacote inclui runtime, catálogos pt-BR/en e assets incorporados. Notificações experimentais ausentes do payload.
- `artifacts/CouchPad-Setup-1.0.0-win-x64.exe` — 53.494.908 bytes. SHA-256: `23D28331B7A45081EA6812CCD3E66CDC2EBC22292145069DFE5845C0591834F0`.
- O App publicado passou no smoke usando o executável autocontido. O rescue publicado carregou o runtime incluído e retornou 2 para um argumento desconhecido.
- Authenticode do instalador, do App e do rescue: `NotSigned`. Os repositórios pessoais de certificados do usuário e da máquina não contêm certificado de assinatura de código. Ver D067.

## Validação em conta limpa

### Primeira instalação em conta limpa

- Em 13/11/2026, o operador criou a conta local `Teste` e confirmou que o candidato foi instalado sem pedir credenciais de administrador e que o CouchPad abriu normalmente. A consulta na conta principal confirmou um processo `CouchPad.exe` na sessão 10 da conta `Teste`. O caminho do processo e a definição da tarefa exigem consulta na própria conta de teste.
- A captura enviada pelo operador confirma a versão 1.0.0 em pt-BR, estado inativo, simulação local e automação, XInput, autostart e permissões de escrita de tela e áudio desligados. Os seletores de TV, monitor e saída de áudio estavam vazios. A opção de minimizar para a bandeja veio habilitada.
- O operador confirmou que minimizou o App instalado na conta `Teste` e reabriu a janela pela opção **Abrir** do menu da bandeja.
- O operador confirmou que o CouchPad abriu sozinho após habilitar **Iniciar com o Windows**, sair e entrar novamente na conta `Teste`. A consulta na conta principal encontrou a nova sessão 11 às 09:09 e o processo do CouchPad iniciado às 09:09:34 na mesma sessão. A consulta entre contas não retornou a linha de comando nem o resultado das tarefas. O disparo das duas tarefas e o marcador do bootstrap têm evidência independente na matriz P6.
- O operador confirmou a reinstalação do mesmo candidato na conta `Teste`. **Iniciar com o Windows** permaneceu ligado. Automação e permissões de tela e áudio permaneceram desligadas. O estado dos controles foi confirmado pelo operador. A contagem das tarefas ainda depende de consulta na conta de teste.
- O operador confirmou a desinstalação na conta `Teste`. A verificação após a remoção encontrou ausência da pasta instalada em `AppData\Local\Programs\CouchPad`, do atalho no menu Iniciar, das tarefas de rescue e autostart e de processos App/rescue. A leitura de `settings.json` a partir da conta principal foi negada pelo Windows, portanto a preservação desse arquivo não foi verificada diretamente.

O teste foi executado na conta padrão `Teste` para preservar a instalação de desenvolvimento. O mesmo pacote está disponível em `C:\Users\Public\Downloads\CouchPad-Setup-1.0.0-win-x64.exe`, com SHA-256 conferido.

## Roteiro para repetir a validação

1. Confirme o SHA-256 e instale pelo EXE. Registre o resultado, o nível de privilégio, a pasta de destino, o atalho, o registro do desinstalador e o caminho da tarefa de rescue.
2. Abra pelo atalho. Confirme automação, XInput e escritas de display/áudio desligados por padrão. Minimize, reabra e saia pela bandeja.
3. Habilite **Iniciar com o Windows**, saia pela bandeja e entre novamente no Windows. Confira as duas tarefas, o marcador `--windows-autostart`, a origem do processo e a ausência de journal.
4. Feche pela bandeja. Reinstale o mesmo pacote e confirme a preservação das preferências e a ausência de tarefas duplicadas. Se houver falha, interrompa a validação e preserve o log.
5. Desinstale. Confirme ausência dos binários, atalho, tarefas, registro de desinstalação e processos do CouchPad. Os dados por usuário são preservados conforme D066.

Para voltar a uma versão anterior, conclua qualquer recuperação pendente, faça backup das configurações, saia do App pela bandeja, desinstale e reinstale o pacote anterior quando disponível. Preserve os baselines originais para diagnóstico. A validação dessa sequência com um instalador anterior depende da existência desse artefato.

## Regressão final da versão 1.0.0

- Builds isolados Debug e Release com 0 avisos e 0 erros. Em cada configuração, 259 testes (232 Core e 27 App) e smoke passaram.
- SHA-256 reconferido. A distribuição usa o mesmo EXE que passou pelo ciclo de instalação, reinstalação e desinstalação na conta `Teste`. O pacote foi gerado com a versão 1.0.0 definida no comando de build. O código-fonte agora declara a mesma versão por padrão.

## Candidato 1.0.11

- Correção: snapshots CCD do journal são reidratados por `DevicePath` exato e, se ele mudou, por EDID/nome; se a topologia já é o baseline ativo, o restore não escreve. Falha de reidratação é fail-closed e registrada como `DisplaySnapshotRehydrationException`.
- Evidência read-only com o journal real de 15/11/2026: `STRICT=False`, `REHYDRATED=True`, AOC `59140:24646` para `59029:24646`.
- Build Debug/Release 0/0, 286 testes e smoke.
- Instalação per-user sem elevação por cima da instalação existente: exit 0. App e rescue reportaram `1.0.11.0`; ProductVersion `1.0.11+fa6967fe8fa8e69d0c9a1bd02c512fb608bf9dca`.
- Tarefas `CouchPad-Autostart-*` e `CouchPad-Rescue-*` permaneceram `Ready`, apontando para `%LOCALAPPDATA%\Programs\CouchPad\CouchPad.Rescue.exe`.
- Abertura com automação/escritas temporariamente desligadas: sem banner de recuperação e toggle de automação habilitado. `settings.json` restaurado com hash idêntico.
- `CouchPad.Rescue.exe --startup-recovery` sem journal retornou 0 e não criou journal.
- Instalador `CouchPad-Setup-1.0.11-win-x64.exe`: 84.286.297 bytes, SHA-256 `74605662FFD3437E78448AC2D0B300FBFA3B561D479E8A7D38F953715584A1EC`. Publicação no GitHub pendente.

## Candidato 1.0.12

- Recurso: perfis de ambiente nomeados, com TV, monitor interno e saída de áudio; migração de configurações antigas para perfil padrão; seletor, criação, renomeação e exclusão.
- Build Debug/Release 0/0, 290 testes e smoke.
- Troca Sala → Quarto via UIAutomation no build e no executável instalado, com `AllowDisplayWrites=false`, `AllowAudioWrites=false`, automação desligada e XInput desligado. O TV selecionado mudou para `tv-quarto`, `activeProfileId` virou `quarto`, o topo de settings foi espelhado e os dois perfis permaneceram.
- Instalação per-user sem elevação por cima da instalação existente: exit 0. App e rescue reportaram `1.0.12.0`; ProductVersion `1.0.12+5563af889f35e78079773d2b0fc00cc0a3d1b423`.
- Tasks `CouchPad-Autostart-*` e `CouchPad-Rescue-*` permaneceram `Ready`; journal ausente. `settings.json` restaurado após o teste.
- Capturas oficiais de 16/11/2026 atualizadas para `1.0.12` em `docs/screenshots/`.
- Instalador `CouchPad-Setup-1.0.12-win-x64.exe`: 84.294.096 bytes, SHA-256 `E5DE69BFBFCB7CA8EC5F513B1070007C8D8B819F31E642C50C36C2523DA6F84A`. Publicação no GitHub pendente.

## Candidato 1.0.13

- Recurso: diagnóstico explicável da identificação de TV, monitor e áudio, com EDID, HDMI, ativo/disponível, motivo e confiança alta/média/baixa; expander read-only.
- Build Debug/Release 0/0, 294 testes e smoke.
- Instalação per-user sem elevação por cima da instalação existente: exit 0. App e rescue reportaram `1.0.13.0`; ProductVersion `1.0.13+dca36e4356807a2cbcc4cf13731dde2b66b9f7e4`.
- Expander aberto por UIAutomation no executável instalado. No hardware real, o texto mostrou TV SAMSUNG e monitor AOC com motivos de TV HDMI inativa e monitor ativo não-HDMI; o áudio apareceu como ambíguo por duas saídas Samsung compatíveis e não foi selecionado. Nenhuma escrita de display/áudio ocorreu.
- Tasks `CouchPad-Autostart-*` e `CouchPad-Rescue-*` permaneceram `Ready`; journal ausente. `settings.json` foi restaurado ao conteúdo original após os testes.
- Capturas oficiais de 16/11/2026 atualizadas para `1.0.13` em `docs/screenshots/`.
- Instalador `CouchPad-Setup-1.0.13-win-x64.exe`: 84.283.279 bytes, SHA-256 `109DE17A9AF1E24AAA1000DB373B95EAB28B20C4D4F20BFC7EB707349722F373`. Publicação no GitHub pendente.

## Candidato 1.0.14

- Recurso: MVP do HUD de desempenho com CPU, CPU do jogo, GPU, RAM, VRAM, processo e resolução/refresh; métricas selecionáveis, chord LB+RB+Y, posição/opacidade/escala e “só durante a sessão”.
- Build Debug/Release 0/0, **309 testes** e smoke.
- Modal de configuração aberto e salvo por clique real; HUD oculto antes da sessão e visível após ativação simulada; estilos `WS_EX_TRANSPARENT`, `WS_EX_NOACTIVATE` e `WS_EX_TOOLWINDOW` confirmados.
- Sampler real em produção mostrou CPU, GPU, RAM, VRAM, processo e 1920×1080 · 120 Hz com escritas desligadas.
- Instalação per-user sem elevação por cima da instalação existente: exit 0. App e rescue reportaram `1.0.14.0`; ProductVersion `1.0.14+78aeadd6ff848f7eab25b26f525c7de021895ecb`.
- HUD aberto no executável instalado com métricas reais; tasks `CouchPad-Autostart-*` e `CouchPad-Rescue-*` permaneceram `Ready`; journal ausente.
- `settings.json` restaurado ao conteúdo original após os testes; chord físico em controle real ainda pendente.
- Instalador `CouchPad-Setup-1.0.14-win-x64.exe`: 84.337.865 bytes, SHA-256 `A745068F7A6FA4D3A8025A706C8292B90C73D50F7EC31E35C53BAED89D7032E0`. Publicação no GitHub pendente.

## Candidato 1.0.22

- Corrige o modo Automático da TV: selecionar "Automático" limpa resolução/refresh do perfil (escala preservada).
- 358 testes; build Debug/Release 0/0; smoke do build e do instalado OK.
- Instalação per-user exit 0; App/rescue `1.0.22.0`; tasks `Ready`; journal ausente; settings preservado na instalação.
- Perfil ajustado para 1920×1080@60 (escala 200% preservada); UIA confirma "Resolução 1920×1080" e "Taxa 60 Hz".
- Instalador `CouchPad-Setup-1.0.22-win-x64.exe`: 84.313.330 bytes, SHA-256 `EFE45675DD34A84A9B109E23D6BCEE3DD626971260DD413D5103738BC12F1F72`. Publicação pendente.

## Candidato 1.0.21

- Recurso: resolução, taxa de atualização e escala do Windows da TV por perfil de ambiente (D082); modos lidos do EDID; apply via `ChangeDisplaySettingsEx` com validação/rollback; escala por monitor via `PerMonitorSettings\DpiValue`.
- 358 testes; build Debug/Release 0/0; smoke do build e do instalado OK.
- Instalação per-user exit 0; App/rescue `1.0.21.0` (ProductVersion `1.0.21+744732e9…`); tasks `Ready`; journal ausente; `settings.json` preservado por hash logo após instalar.
- UIA no executável instalado: seção “Modo da TV” com resoluções Automático/3840×2160/2560×1440/1920×1080/1280×720; para 1080p, taxas 60/50/30/25/24 Hz; escalas Automático/100/125/150/175/200%; sem `display.tv_mode_unavailable`. Nenhuma escrita de display durante a leitura.
- Aplicação real na TV (modo + escala) pendente de teste do usuário.
- Instalador `CouchPad-Setup-1.0.21-win-x64.exe`: 84.312.220 bytes, SHA-256 `1C76BB5232D5E66B32B3F88F3170FA19C3720C399CD8F7AC582843F6D7BA2DC5`. Publicação pendente.

## Candidato 1.0.20

- Remove o HUD de monitoramento de hardware e a integração RTSS (D081); shell com 8 cards, foco em automação.
- 295 testes; build Debug/Release 0/0; smoke do build e do executável instalado OK.
- Instalação per-user exit 0; App/rescue `1.0.20.0` (ProductVersion `1.0.20+89831489c20469f2af2d55ed4fa4931723b2462d`); tasks `CouchPad-Autostart-*`/`CouchPad-Rescue-*` `Ready`; journal ausente; `settings.json` sem `performanceHud*`.
- UIA: nenhuma ocorrência de HUD/FPS; os 8 cards existem no XAML e o conteúdo das 8 áreas está presente. Títulos de card não são expostos como peers de texto no UIA — registrado como pendência de a11y.
- Instalador `CouchPad-Setup-1.0.20-win-x64.exe`: 84.285.012 bytes, SHA-256 `05AB1EDC6E3D04832EE6346E165CCA2D83B37C8ACE7083C5C7613F3E3F7FD256`. Publicação no GitHub pendente.

## Candidato 1.0.19

- Correção da GPU: PDH consultado em Unicode, `CStatus` validado e instâncias `engtype_3D` filtradas pelo PID do processo em primeiro plano. Antes o HUD mostrava o pico global e oscilava entre 0% e 100%.
- Correção da VRAM: o total do adaptador é lido do registro (`HardwareInformation.qwMemorySize`, maior adaptador); o texto passa a mostrar usado/total e a barra é desenhada.
- Build Debug/Release 0/0, **342 testes** e smoke.
- Instalação per-user sem elevação por cima da instalação existente: exit 0. App e rescue reportaram `1.0.19.0`.
- Smoke do executável instalado aprovado. Tasks `CouchPad-Autostart-*` e `CouchPad-Rescue-*` permaneceram `Ready`; journal (`state\session.json`) ausente; `settings.json` preservado.
- HUD no executável instalado: VRAM passou a mostrar `4,7 / 12,0 GB`; com Elden Ring em foco, `4,8 / 12,0 GB`. O total presente confirma a leitura do registro.
- Numa amostra válida com o jogo em foco, **GPU 67%** e **FPS 60**, com `Processo=eldenring`; o valor por processo substitui o pico global anterior.
- Ressalva: o Elden Ring crashou (`0xc0000374`, corrupção de heap em `ntdll.dll`) cerca de 3 s após o foco, antes de completar as três amostras de estabilidade. Não há evidência de causalidade com o HUD; a estabilidade da GPU por processo segue pendente.
- Instalador `CouchPad-Setup-1.0.19-win-x64.exe`: 84.352.177 bytes, SHA-256 `7FA72E6E3F0DAF75EDEA5A76381F28EAC3C2216E543D2D185D5F0A44589D785E`. Publicação no GitHub pendente.

## Candidato 1.0.18

- Correção do chord do HUD: `IGamepadButtonSource` só reportava mudanças de botão, então o detector nunca conseguia medir o hold de 800 ms e `LB+RB+Y` não disparava. O contrato passou a ser `ButtonsReported` (estado a cada poll, ~30 Hz) e a detecção de borda/hold ficou no consumidor; teste de regressão cobre o hold.
- Build Debug/Release 0/0, **333 testes** (283 Core + 50 App) e smoke.
- Instalação per-user sem elevação por cima da instalação existente: exit 0. App e rescue reportaram `1.0.18.0`; ProductVersion `1.0.18+78bf34c74d6af5fa45590117a2e8da6da2eba2dd`.
- Smoke do executável instalado: `SMOKE: window opened 1020x780; initial=En; pt='Ouvindo gamepad'; en='Listening for gamepad'; language=En; title='CouchPad'`.
- Chord LB+RB+Y validado no controle físico no build instalado. Evidência: 8BitDo Ultimate 2 no slot 0 XInput, log do monitor com `0x8300 decoded=Y,LB,RB` segurado por ~2,7 s; o HUD sumiu e voltou com o chord.
- Tasks `CouchPad-Autostart-*` e `CouchPad-Rescue-*` permaneceram `Ready`; journal (`state\session.json`) ausente.
- `settings.json` preservado: 2.143 bytes e SHA-256 `7100E8D64E79133D0C016052712D56DE87D083026975E5A156AE1B5EC5BDF0D1` antes e depois da instalação; chord `LB+RB+Y`. O campo `performanceHudShowOnlyInSession` apareceu como `true` logo após a instalação porque o app antigo, fechado para instalar, regravou o estado em memória ao sair; o valor foi restaurado para `false` após o fechamento desse app antigo e o `settings.json` final foi validado com chord `LB+RB+Y` e `showOnlyInSession=false`. Sem pendência aberta nesse ponto.
- Instalador `CouchPad-Setup-1.0.18-win-x64.exe`: 84.348.738 bytes, SHA-256 `C9661F72C97E4A78B66EDA48AF1D129DE394E60650A8A8361548585485F91621`. Publicação no GitHub pendente.

## Candidato 1.0.17

- Recurso: métrica de FPS no HUD via shared memory do RTSS (`RTSSSharedMemoryV2`, read-only, opt-in), com `IFrameRateSource` no Core e `RtssFrameRateSource`/`RtssSharedMemoryParser` em `Platform.Windows`; validada com RTSS real (Silent Hill 2) e correções de layout da entrada, teto de 16 MiB e janela de frames. Decisão D080.
- Build Debug/Release 0/0, **332 testes** e smoke.
- Instalação per-user sem elevação por cima da instalação existente: exit 0. App e rescue reportaram `1.0.17.0`; ProductVersion `1.0.17+b001ec995b045436e611e1383979d31a320c39dd`.
- Smoke do executável instalado: `SMOKE: window opened 1020x780; initial=En; pt='Ouvindo gamepad'; en='Listening for gamepad'; language=En; title='CouchPad'`.
- Tasks `CouchPad-Autostart-*` e `CouchPad-Rescue-*` permaneceram `Ready`; journal (`state\session.json`) ausente.
- `settings.json` preservado: 2.131 bytes e SHA-256 `DAC235DB0EDA05DE602C3BF3562C607A620D8B2374783800EDBB929307FFBD41` antes e depois da instalação.
- Instalador `CouchPad-Setup-1.0.17-win-x64.exe`: 84.343.548 bytes, SHA-256 `3C7BFA792F1D4C28290A39F684BD74B0646B9F7372F3DFC66D9649315408F307`. Publicação no GitHub pendente.

## Candidato 1.0.16

- Redesenho do shell: cabeçalho com logo em bloco, regra de acento e status à direita.
- Card HUD em linha inteira na primeira tela, com toggle, configurar, estado, posição, atalho, sessão, chips de métricas e prévia fiel.
- Cards reorganizados e ordem de foco atualizada nos três breakpoints; tokens `HudSurface`, `HudBorder`, `HudTrack` e `Radius.Hud` compartilhados.
- Build Debug/Release 0/0, **310 testes** e smoke; capturas `p7a-*` em 3 breakpoints × 2 idiomas.
- Instalação local validada sem elevação: App/rescue `1.0.16.0`, ProductVersion `1.0.16+7d35e20d4b933b044c981923f3cd5e6950236ee4`. O toggle do card criou o overlay e persistiu a configuração; `settings.json` restaurado, tasks `Ready`, journal ausente. Instalador `CouchPad-Setup-1.0.16-win-x64.exe`: 84.339.291 bytes, SHA-256 `C4D587C97ED53011CDF9BBDC2DF75F43FA947CDCFEC9B46497EFFEFD6971809B`. Publicação pendente.

## Candidato 1.0.15

- Correção: as barras de GPU/VRAM do HUD e da prévia usavam `CouchPad.Accent2`, token inexistente, e não eram desenhadas. Passam a usar `CouchPad.Info`.
- Barras ganham largura mínima de 2 px para valores baixos; GPU em 1% continua visível.
- Build Debug/Release 0/0, **310 testes** e smoke. Novo teste garante que todo `{DynamicResource CouchPad.*}` do XAML exista em `Themes/Tokens.axaml`.
- Captura do HUD real confirmou CPU 8%, GPU 1%, RAM, VRAM, processo e resolução com as barras desenhadas.
- Instalação per-user sem elevação por cima da 1.0.14: exit 0. App e rescue reportaram `1.0.15.0`; ProductVersion `1.0.15+15f759a723d1b3b1110166af9d4a61a56fe25203`.
- Executável instalado: HUD real abriu com GPU em 0% e, em modo simulação, GPU 74% e VRAM 7,2/12,0 GB com as barras azuis desenhadas. Tasks de autostart/rescue permaneceram `Ready`; journal ausente.
- `settings.json` restaurado ao conteúdo original (hash conferido); chord físico em controle real ainda pendente.
- Instalador `CouchPad-Setup-1.0.15-win-x64.exe`: 84.333.243 bytes, SHA-256 `9DEF26147CD7B2DF66715B448B7C9E7B69BC328D605B7AB7B9A2B016D8748A6B`. Publicação no GitHub pendente.
