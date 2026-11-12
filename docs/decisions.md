# CouchPad — Registro de decisões

> Fonte canônica para o projeto. Copiar para `docs/decisions.md` no repositório.
> Novas decisões entram no fim, com data e ID. Não apagar histórico.

## Confirmadas

| ID | Data | Decisão | Motivo |
|---|---|---|---|
| D001 | 2026-11-09 | Avalonia 12.1.4 + .NET 10 (`net10.0`) | Prova Avalonia existente; XAML/MVVM |
| D002 | 2026-11-09 | pt-BR + inglês | Público-alvo e i18n |
| D003 | 2026-11-09 | App 100% free | Sem licença/ativação/trial/telemetria |
| D004 | 2026-11-09 | UI inspirada na referência, não copiada | Direitos autorais e identidade própria |
| D005 | 2026-11-09 | REA somente estático | Segurança e escopo |
| D006 | 2026-11-09 | Escopo: tela, áudio, HDR e rescue | Foco funcional |
| D007 | 2026-11-09 | Serviço simulado primeiro | Evitar risco de hardware |
| D008 | 2026-11-09 | Fail-closed em hardware | Não deixar a máquina sem display |
| D009 | 2026-11-09 | Journal com escrita atômica | Recuperação confiável |
| D010 | 2026-11-09 | Nome **CouchPad**; repo `couchpad-windows`; identidade original | Marca própria e independência |
| D011 | 2026-11-09 | Licença **MIT**, Copyright (c) 2026 Matheus Mello | Consistência com o projeto `couchpad` existente |
| D012 | 2026-11-09 | Windows 10 1903+ e Windows 11, x64; ARM64 via emulação no v1 | Cobertura realista |
| D013 | 2026-11-09 | Distribuição self-contained, x64 | Sem dependência de runtime |
| D014 | 2026-11-09 | Idioma padrão = cultura do sistema; pt-BR + inglês; fallback inglês | Primeira experiência |
| D015 | 2026-11-09 | Config em `%LOCALAPPDATA%\CouchPad\settings.json`; journal em `state\session.json`; logs em `logs\` | Per-user, sem admin |
| D016 | 2026-11-09 | Primeiro uso com automação desligada e configuração mínima guiada | Evitar mudança acidental |
| D017 | 2026-11-09 | Layout do shell reposicionado em code-behind com 3 breakpoints (`≥1000`, `720–999`, `<720`); janela mínima `640×640`; conteúdo rolável | Alcançar os 3 layouts sem duplicar XAML |
| D018 | 2026-11-09 | P1 é somente UI: toggle de automação alterna `Inativo`/`Aguardando gamepad`; refresh de displays é placeholder de 700 ms; nada de CCD/áudio/HDR | Validar o shell sem risco de hardware |
| D019 | 2026-11-09 | Até glifos curtos (chips `1`/`LB`/`RB`/`A` e unidades `1 s`) vêm dos catálogos; teste de XAML bloqueia texto visível literal | Cumprir "nenhuma string fora dos dicionários" |
| D020 | 2026-11-09 | P2: componentes como `ContentControl`/`TemplatedControl` com `ControlTheme` em `Themes/Components.axaml`; shell consome `Card`, `StatusPill`, `StatusRow`, `KeyChip`, `PathPicker` e `ActionButton`; galeria de estados via `--gallery` | Reuso, estados consistentes e validação visual sem hardware |
| D021 | 2026-11-09 | `ActionButton` tem `Variant` (Primary/Secondary) e `State` (Idle/Loading/Success/Error); foco usa borda `TextPrimary` de 2 px; hover/pressed primários usam `AccentMutedHover`/`AccentMutedPressed` | Estados distinguíveis, foco visível e cor nunca como único indicador |
| D022 | 2026-11-09 | Classes `Button.loading` e `ComboBox.loading` definem `IsEnabled=false`; o `ActionButton` não altera `IsEnabled` por si só e o shell desabilita o refresh durante a atualização | Bloquear teclado e ponteiro e evitar duplo acionamento |
| D023 | 2026-11-09 | Seletor de pasta: o ViewModel expõe `ChooseLauncherFolderCommand` e o evento `FolderPickerRequested`; a view usa `StorageProvider.OpenFolderPickerAsync` | Manter `StorageProvider` fora do ViewModel/Core e permitir teste sem diálogo |
| D024 | 2026-11-09 | P3: display, áudio e gamepad simulados atrás de `IDisplayService`, `IAudioService` e `IGamepadWatcher` no Core; atrasos via `IDelayScheduler` injetável | Testar o fluxo completo sem hardware nem espera real |
| D025 | 2026-11-09 | State machine `Idle`, `WaitingForGamepad`, `Pending`, `Applying`, `Active`, `Error` (`RescuePending` reservado); toda transição é cancelável por CTS e serializada por gate | Atrasos cancelam ao desconectar/desligar e não há transição concorrente |
| D026 | 2026-11-09 | `AutomationSnapshot` guarda o baseline de restauração separado do modo aplicado; reaplicar modo preserva o baseline; o journal grava modo aplicado, baseline (`restoreDisplay`/`restoreAudio`) e é limpo ao restaurar | Fail-closed: restaurar sempre o estado anterior real, inclusive no rescue da P6 |
| D027 | 2026-11-09 | `AppSettings` persiste toggles, atrasos, modos, launcher e caminho; cada gravação relê o arquivo para preservar `language` e chaves desconhecidas | Preferências sobrevivem ao restart sem perder extensões |
| D028 | 2026-11-09 | UI da P3 expõe conectar/desconectar gamepad simulado; o watcher real só entra na P4/P6 | Validar a state machine com risco zero |
| D029 | 2026-11-09 | Endurecimento do contrato P3/P4: `DisplayState.Topology` é um snapshot opaco da topologia completa; journal grava `pending` antes do apply e `active` depois, com baseline; falha do `pending` inicial aborta a mudança | Fechar a janela de crash e permitir rescue fiel na P6 |
| D030 | 2026-11-09 | Suprimir somente `NU1900` quando a fonte de auditoria do NuGet estiver indisponível; a auditoria permanece habilitada quando houver rede | Manter build reproduzível offline com 0 avisos sem esconder vulnerabilidades reais |
| D031 | 2026-11-10 | `DisplayMode.InternalOnly` representa o baseline sem TV; `DisplayState.Mode` é resumo e `Topology` é a fonte de verdade para restore; mapeamento de `Extend` vs `Mirror` vem de sources/modos dos paths ativos | Mapear o estado real sem inferir TV pela ordem dos paths |
| D032 | 2026-11-10 | `IRescueGuard`/`IRescueLease` no Core: begin após journal pending, commit após apply/verify, abort após rollback, abandon quando o rollback falha; `NoopRescueGuard` na simulação; processo real é da P4/Codex | Falhar fechado e permitir watchdog independente sem acoplar Core à plataforma |
| D033 | 2026-11-10 | App referencia `Platform.Windows` apenas na raiz de composição; o serviço real é opt-in | Manter a UI testável e o padrão seguro |
| D034 | 2026-11-10 | Perda do watchdog antes da liberação marca `recoveryUncertain` e bloqueia novas mutações até reiniciar | Não tentar outra mudança sem saber o estado |
| D035 | 2026-11-10 | UI de seleção de TV/monitor + `AllowDisplayWrites` (padrão falso); App só compõe serviço real e rescue quando o opt-in e os seletores existem; banner de recuperação incerta | Deixar a matriz P4 configurável sem tornar a escrita acidental |
| D033 | 2026-11-10 | A raiz de composição do App pode referenciar Platform.Windows para criar, em conjunto, `WindowsDisplayService` e `WindowsRescueGuard` após seleção explícita; ambos permanecem com escrita e recuperação desabilitadas por padrão | Injetar implementações sem levar Win32, COM ou decisões de fluxo para a UI |
| D034 | 2026-11-10 | `IRescueLease` expõe sinal de falha e verificação de disponibilidade; o coordenador vincula o sinal ao cancelamento da transição e mantém o journal se o watchdog falhar | Detectar perda do processo de rescue durante mudanças sem acoplar o Core a processos ou APIs Windows |
| D036 | 2026-11-10 | Um `AutomationCoordinator` configurado com `IRescueGuard` exige um journal explícito; sem guard, a simulação pode continuar sem journal | Evitar iniciar um guard real sem baseline persistido para recuperação |
| D037 | 2026-11-10 | Cada apply, inclusive reaplicação, exige gravação bem-sucedida do journal `pending`; em falha, preservar o snapshot e o journal anteriores e não alterar hardware | Não supor que uma gravação anterior permanece disponível após uma falha de persistência |
| D038 | 2026-11-10 | Resultado de rescue indeterminado bloqueia novas transições na sessão e preserva o journal, sem iniciar rollback concorrente; a plataforma deve reconciliar decisões duráveis antes de informar o resultado | Não executar uma segunda alteração enquanto o watchdog pode estar restaurando |
| D039 | 2026-11-10 | Toda instância Windows do App que usa o journal adquire posse exclusiva antes de criar o coordenador e mantém a posse até o processo terminar; o coordenador bloqueia a automação ao encontrar journal anterior ou não inspecionável | Evitar sobrescrever baseline pendente entre instâncias, inclusive simuladas, ou após falha anterior |
| D040 | 2026-11-10 | Falha ao limpar journal é observável e mantém o coordenador em recuperação incerta, sem descartar o snapshot | Evitar tratar como encerrada uma sessão cujo registro de recuperação ainda existe |
| D041 | 2026-11-10 | Desligar o opt-in de escrita revoga novos applies imediatamente e cancela o apply corrente; restores continuam disponíveis, e reativar a escrita exige reinício | Impedir que um serviço já composto continue aplicando mudanças após o usuário desligar a opção |
| D042 | 2026-11-10 | O contrato de display pode sinalizar que um apply falhou comprovadamente antes de qualquer tentativa de mutação; o coordenador dispensa restore somente nesse caso ou antes de chamar apply | Impedir SDC_APPLY de rollback após recusa de validação ou cancelamento prévio, preservando rollback conservador quando houve tentativa ou há dúvida |
| D043 | 2026-11-10 | Espelhamento deixa de ser modo selecionável ou aplicável, inclusive na simulação; modo e snapshot espelhados existentes permanecem legíveis e restauráveis. Configuração antiga de espelhamento volta ao padrão com automação desligada | Atender à decisão do usuário sem prejudicar a recuperação de uma topologia anterior |
| D044 | 2026-11-10 | Novas configurações e redefinição usam atraso de ativação de 2 segundos, com contagem regressiva localizada; valores já salvos continuam respeitados | Dar tempo ao usuário antes de mudar a tela, mantendo o atraso configurável |
| D045 | 2026-11-10 | Usar a imagem fornecida explicitamente pelo usuário como marca no cabeçalho e ícone da janela e executável; o ICO contém tamanhos para Windows | Atender à escolha de imagem feita pelo usuário; preservar a origem do arquivo fornecido |
| D046 | 2026-11-11 | Baseline real de áudio terá schema explícito, IDs de endpoint por papel e estados de volume/mudo por endpoint; será adicionado ao journal de forma compatível com registros anteriores e vinculado à decisão de rescue antes de permitir escrita real | `AudioOutput` não identifica dispositivos, e o Windows pode alterar padrões automaticamente ao mudar a topologia |

## Pendentes

### Antes de P4/P5

1. Identidade do TV (target path + EDID; fallback nome).
2. Modos de display suportados (target-only, estender, espelhar; padrão target-only).
3. API de display (CCD).
4. API de áudio e endpoint padrão (documented APIs; `IPolicyConfig` como fallback).
5. Política de HDR (capturar/reaplicar; não forçar).
6. Acesso a hardware de teste.
7. Privilégios (per-user; admin só se inevitável).
8. Matriz mínima de teste.

### Antes de P6

9. Rescue separado (`CouchPadRescue.exe`) ou flag.
10. Formato/caminho do journal do CouchPad.
11. Gatilho e atraso da tarefa.
12. Lock, concorrência e anti-loop.
13. Bandeja e autostart.
14. Instância única e IPC.

### Antes de P7

15. Instalador (Inno Setup per-user).
16. Assinatura (certificado, self-signed ou nenhuma documentada).
17. Atualização (manual no v1).
18. Canal de release e suporte.
19. Licenças de dependências (`NOTICE`).
20. Definição de v1.0.
21. CI (GitHub Actions).
22. Crash reporting (sem telemetria).
23. Metas de performance.

### Pode esperar

- Tema claro.
- Mais idiomas.
- Modo portátil.
- CLI.
- Auto-update.
- Winget.
- ARM64 nativo.
- Estatísticas locais.

## D047 — Exceção isolada para seleção de áudio padrão

- Data: 2026-11-11
- Decisão: o usuário autorizou explicitamente usar `IPolicyConfig`, interface Windows não documentada, como exceção isolada em Platform.Windows para definir o endpoint padrão por papel e recuperá-lo.
- Motivo: não foi identificada API pública documentada equivalente; a troca automática de endpoint não garante restauração em todas as configurações.
- Impacto: sem garantia de compatibilidade entre versões do Windows; falha de ativação, chamada ou verificação deve impedir operação ou manter recuperação pendente. Escrita real continua fechada até watchdog e testes estarem prontos. O restante continua com APIs públicas, sem admin ou telemetria.
- Evidência: [consulta Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/1162167/how-to-find-audio-devices-that-are-connected); autorização explícita do usuário em 11/11/2026.

## D048 — Configuração explícita do endpoint de áudio

- Data: 2026-11-11
- Decisão: adicionar às configurações do Core um ID de endpoint de TV e um opt-in de escrita de áudio, desligado por padrão. O App apresenta seleção de endpoint e informa que alterações exigem reinício.
- Motivo: o mesmo display apresentou IDs de áudio diferentes em TvOnly e Extend; nome ou EDID de vídeo não comprovam a identidade do endpoint de áudio.
- Impacto: a escolha é específica para a topologia configurada; endpoint ausente ou ambíguo deve falhar fechado. A configuração é per-user, sem API Windows no Core; a seleção automática CCD↔áudio continua não comprovada.
- Evidência: inventários e testes físicos em `docs/p5-audio-hdr.md`.

## D049 — Liberar locks se áudio travar durante transação

- Data: 2026-11-11
- Decisão: em uma transação com baseline de áudio, se ocorrer timeout ou quebra do pipe sem decisão, o watchdog pode encerrar apenas o processo pai identificado como `CouchPad.exe` no mesmo diretório do rescue antes de recuperar; valida PID, horário de início e caminho. Não encerra a árvore de processos nem aplicativos de terceiros.
- Motivo: chamadas COM síncronas podem prender os locks de decisão e mutação; cancelar uma task não interrompe uma chamada nativa travada, impedindo a recuperação do display.
- Impacto: o App pode ser encerrado para recuperar a sessão; o journal permanece até recuperação confirmada. Um pai de caminho não reconhecido não é encerrado e o watchdog reporta falha em vez de tocar outro processo; o App não habilita áudio real quando executado via `dotnet.exe`. A conclusão de chamadas no serviço de áudio do Windows após término do App não é garantida e segue como limitação a avaliar.
- Evidência: revisão independente P5 e teste de processo falso com pai mantendo os dois locks.

## D050 — Fonte física de gamepad via XInput, com opt-in de inicialização

- Data: 2026-11-11
- Decisão: a primeira fonte física será XInput por polling dos quatro slots, somente leitura, em `Platform.Windows`. O padrão permanece simulado; `--windows-gamepad` habilita a fonte real para a sessão. Dispositivos HID que não exponham XInput não são detectados neste slice.
- Motivo: o contrato `IGamepadWatcher` já representa conexão/desconexão e aceita uma implementação de plataforma sem mudança no Core; XInput é suportado pelo Windows sem admin nem dependência adicional.
- Impacto: o nome reportado é genérico (“XInput gamepad”) e o ID identifica o slot XInput, não um serial físico. O modo real só inicia com o argumento explícito; smoke/galeria continuam simulados. A validação de hardware ainda é necessária para conexão, desconexão, atraso, cancelamento, ausência e reconexão.
- Evidência: `WindowsGamepadWatcher` e testes puros de seleção de slot; build 0/0, smoke OK e 197 testes aprovados em 11/11/2026.

## D051 — Persistir opt-in para watcher físico

- Data: 2026-11-12
- Decisão: acrescentar `UseWindowsGamepad`, desligado por padrão, às configurações e UI. O App usa XInput quando a opção estiver ligada; é necessário reiniciar para trocar a implementação ativa. `--windows-gamepad` continua como override explícito de diagnóstico.
- Motivo: permitir habilitar o watcher real sem terminal e sem mudar o padrão simulado, garantindo um caminho reproduzível para o teste assistido de hardware.
- Impacto: altera o modelo de preferências do Core e acrescenta um toggle localizado no App. Ativar o watcher não liga a automação nem habilita escrita de display/áudio; esses opt-ins continuam independentes.
- Evidência: decisão tomada durante preparação da validação física de 12/11/2026.

## D052 — Experimento adiado: contagem em notificação local do Windows

- Data: 2026-11-12
- Decisão: prototipar notificações locais atualizáveis para atrasos em processo auxiliar Windows com SDK oficial; manter a contagem na janela e deixar o experimento desativado por padrão (`--experimental-windows-notifications`). O usuário adiou a funcionalidade após o banner não aparecer em testes visuais.
- Motivo: tornar a contagem visível fora da janela e manter `net10.0` no App/Core, enquanto o SDK exige TFM Windows versionado.
- Impacto: o build inclui componente Windows App SDK self-contained; falha, atraso, bloqueio ou descarte de notificação não impede nem altera a automação. O SDK aceitou e registrou notificações na Central, mas o usuário não viu o banner; testes e diagnóstico adicionais são necessários antes de habilitar por padrão. Sem servidor ou telemetria do CouchPad; textos são localizados no App.

## D053 — Botão de ativação fora do trabalho pendente

- Data: 2026-11-12
- Decisão: o usuário dispensou implementação adicional do botão de ativação. O controle existente permanece; seu teste físico específico deixa de ser critério pendente.
- Motivo: priorizar o fluxo automático por conexão e desconexão do controle conforme a orientação do usuário.
- Impacto: a reconexão durante o atraso ainda precisa de evidência independente; nenhuma mudança no comportamento existente foi feita.

## D054 — Launcher após ativação protegida

- Data: 2026-11-12
- Decisão: acrescentar contrato de launcher no Core e implementação Windows; oferecer Steam Big Picture e Hydra Big Picture após confirmação de tela/áudio, conforme escolha posterior do usuário. `CloseLauncherOnActivate` passa a significar fechar ao restaurar somente o processo iniciado e identificado pelo CouchPad.
- Ajuste de 12/11/2026: a pedido do usuário, para Steam a opção solicita `steam://close/bigpicture` à instância iniciada e identificada pelo CouchPad, mantendo o cliente Steam aberto. Não solicita saída de uma instância preexistente; o envio do comando não prova que a interface mudou. Hydra mantém o fechamento da janela própria.
- Motivo: executar o fluxo solicitado mantendo a restauração independente de falhas no launcher.
- Impacto: sem encerramento forçado, árvore de processos ou processos preexistentes; launcher já aberto pode receber o comando, mas não é encerrado pelo CouchPad. Falha do launcher é informada e não impede restore. Configurações antigas de caminho personalizado são preservadas, mas não selecionam launcher. Testes com fakes precedem a validação física.

## D055 — Recuperação independente na inicialização

- Data: 2026-11-12
- Decisão: o rescue separado pode recuperar journal `pending` ou `active` após obter os locks exclusivos da sessão e do watchdog. Exige snapshots válidos de display e áudio, usa decisão transacional e somente remove um journal inalterado após restore bem-sucedido.
- Motivo: evitar disputa com uma sessão ou watchdog ainda ativo e não declarar recuperação completa sem baseline de áudio verificável.
- Impacto: journals antigos sem áudio completo permanecem para recuperação assistida; o comando não está agendado nem passou por validação em hardware ou logon. Testes em processo com backend falso estão descritos em `docs/p6-recovery-matrix.md`.

## Formato de nova decisão

```md
## D0XX — Título curto

- Data: AAAA-MM-DD
- Decisão:
- Motivo:
- Impacto:
- Evidência:
```
