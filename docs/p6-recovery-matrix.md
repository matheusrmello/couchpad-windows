# P6 — recuperação no logon

## Estado em 12/11/2026

- Implementado comando separado `CouchPad.Rescue.exe --startup-recovery`; a tarefa de logon ainda não está configurada, e o comando não é chamado automaticamente.
- O rescue adquire a posse exclusiva da sessão e o mesmo lock do watchdog antes de inspecionar o journal. Recusa journal ilegível, estado desconhecido e baseline sem topologia ou áudio completo. A restauração usa a decisão transacional e a verificação existentes. O journal é limpo somente após restauração bem-sucedida e confirmação de que seu conteúdo não mudou.
- A exigência de baseline completo significa que journals antigos sem o snapshot de áudio não são restaurados automaticamente; eles são preservados para recuperação assistida.
- Processo real compilado com backend falso em diretório isolado: `pending` e `active` restaurados e journal limpo; falha de áudio preservou journal; recuperação concorrente foi bloqueada; interrupção preservou journal e tentativa posterior terminou. O backend falso não acessa hardware.
- Verificação adicional de processo: journal ausente não iniciou restore; journal corrompido foi preservado sem restore; posse do lock do watchdog bloqueou a recuperação concorrente. Testes unitários também recusaram estado desconhecido e topologia inválida.
- Falha de display injetada no processo fake impediu a etapa de áudio, preservou o journal e permitiu uma tentativa posterior bem-sucedida. Testes unitários também verificaram erro ao limpar o journal e journal que se torna ilegível após restore. Nenhuma dessas falhas foi injetada no backend de hardware.
- Testes isolados adicionais verificaram cancelamento antes do backend, alteração de metadados do journal com o mesmo baseline e falha em confirmar a remoção. A suíte de processos fake foi repetida com sucesso.
- Repetição solicitada em 12/11/2026: os oito cenários de processo fake passaram novamente (incluindo `pending`, `active`, journal ausente/corrompido, locks, falhas e repetição após interrupção); o build de produção foi refeito com 0 avisos e 0 erros. Não houve alteração de tela ou áudio.
- Anti-loop: limite persistente de três tentativas por baseline, contado antes da escrita; registro inválido bloqueia recuperação. A quarta tentativa com backend falso foi bloqueada sem restore e preservou o journal. Recuperação assistida após o limite ainda precisa de fluxo e validação.
- Produção, journal ausente: comando encerrou com código 0 e journal ausente; a opção interna de teste foi recusada com código 2. Nenhuma recuperação real foi acionada nesta observação.
- Build Debug e 238 testes aprovados; o último smoke também passou. Nenhuma mudança de tela ou áudio foi executada por estes testes. Em 12/11/2026 o usuário informou que a TV está em uso; a validação física fica pendente até poder acompanhá-la.

## Validações restantes deste item

1. **Concluído em 12/11/2026.** Com o usuário presente, o AOC era a única tela ativa e o áudio padrão apontava para o AOC. O controle ativou a Samsung, com imagem confirmada pelo usuário. O journal real estava `active` e continha os baselines de tela e áudio. Após encerrar somente o CouchPad da sessão de teste, o rescue de produção executado manualmente com `--startup-recovery` retornou 0. Os probes confirmaram `InternalOnly`, equivalência semântica da topologia, restauração dos três papéis de áudio e dos valores de volume e mudo do baseline e remoção do journal. O usuário confirmou visualmente a restauração no AOC.
2. Validar `pending` real e reinício/logon, incluindo coordenação com o watchdog, sem inventar estado do journal.
3. Validar falha de recuperação e retenção de journal em cenário seguro, além da evidência com backend falso.
4. Revisar concorrência e integridade, registrar resultados e fazer commit antes de iniciar a integração do agendamento de logon.

## Trabalho isolado sem TV: autostart do App

- `WindowsStartupRegistration` prepara uma entrada de inicialização por usuário que inicia o rescue com `--startup-and-launch`, com caminho absoluto entre aspas e sem administrador. O rescue só inicia o App após a recuperação terminar sem erro. O registro recusa substituir ou remover uma entrada com outro comando e confirma a leitura após gravar ou remover.
- A sequência e a integração da opção foram testadas com callbacks falsos, mas a opção permanece desabilitada até a recuperação ser validada em hardware e logon. Nenhum Registro real foi alterado. A tarefa independente do rescue e a validação de logon continuam pendentes.
- Testes do App verificam estado real simulado, falha com reversão do controle e preservação da preferência quando a opção está indisponível. Um teste adicional confere chaves e valores não vazios nos dois catálogos de idioma. Build e 239 testes passaram; smoke abriu e fechou a janela e verificou textos pt/en. A revisão visual e de acessibilidade completa permanece na P7a.

## Bandeja

- Ícone Windows com abrir e sair, textos pt/en e ocultação ao minimizar/fechar quando habilitada. Saída solicita desativação; estado de recuperação incerto impede encerramento.
- Build 0/0, 241 testes e smoke passaram. Testes simulados cobrem saída inativa e recusa com journal ilegível. O smoke não cria bandeja; interação visual de abrir/minimizar/sair e restauração de sessão real na saída permanecem pendentes.

## Tarefa de logon — preparação

- Definição XML usa SID explícito, `InteractiveToken`, `LeastPrivilege`, `IgnoreNew`, sem timeout que possa interromper uma chamada CCD, e inicia somente `CouchPad.Rescue.exe --startup-recovery`. Criação é exclusiva e recusa sobrescrever uma tarefa existente; após registrar, compara campos essenciais.
- Testes com backend falso cobrem definição, identidades inválidas, conflito e divergência na tarefa registrada. O Agendador local aceitou a definição com `TASK_VALIDATE_ONLY` (sem registrar tarefa). Build isolado, 253 testes e smoke passaram; nenhuma tarefa real foi criada.
- Validação local em 12/11/2026: a tarefa do usuário foi registrada e conferida após tratar a normalização do Agendador (nome da conta no gatilho e valores padrão omitidos). A execução manual da tarefa registrada com journal ausente terminou com resultado 0 e o journal continuou ausente. A tarefa está habilitada para a validação do gatilho real. O registro ainda não está integrado à instalação e o autostart do App não está registrado nesta máquina.

### Comandos para instalação e remoção

- Após copiar os arquivos, o instalador deve chamar o `CouchPad.Rescue.exe --install-logon-task` da pasta de destino, no contexto do usuário. Antes de remover os arquivos, deve chamar `--remove-logon-task` do mesmo executável. Aguarde o código de saída. Código 1 interrompe a etapa e grava diagnóstico local em `%LOCALAPPDATA%\CouchPad\logs\rescue-failures.log`. A remoção recusa sessão em uso ou journal presente. O instalador final pertence à P7b.
- Em 12/11/2026, no Agendador real, o executável aceitou reinstalação da tarefa idêntica, recusou instalação e remoção a partir de outro diretório e recusou remover com o lock de sessão ocupado. Remoção e remoção repetida retornaram 0, com ausência confirmada. A tarefa foi reinstalada, conferida e permaneceu habilitada. A leitura imediatamente após reinstalação mostrou estado Ready e nenhuma execução anterior. O journal permaneceu ausente.
- Build Debug 0/0, 258 testes e smoke aprovados. A evidência do disparo automático exige sair da sessão do Windows, entrar novamente e verificar `LastRunTime`, `LastTaskResult = 0` e journal ausente antes de qualquer execução manual da tarefa.
- Disparo automático confirmado em 12/11/2026 às 22:13, horário local. O operador confirmou que saiu da sessão e entrou novamente. A tarefa recém-reinstalada registrou a primeira execução às 22:13:00, mesmo horário de início do Explorer da sessão interativa 2. O resultado foi 0, a tarefa voltou a Ready e o journal continuou ausente. Nenhuma execução manual foi iniciada entre a reinstalação e essa leitura. Esse cenário valida o gatilho com journal ausente.

## Preparação da validação física

- Confirmar presença do usuário, AOC e Samsung conectados, controle desligado, journal ausente e baseline completo de tela e áudio. Não iniciar se o watchdog ou outra instância mantiver os locks.
- Com a proteção existente ativa, iniciar uma sessão real e registrar journal `active`, topologia e áudio; confirmar imagem com o operador. Encerrar somente o processo de teste identificado e acionar o rescue de produção imediatamente.
- Verificar retorno semântico da topologia, três papéis de áudio, volume/mudo e ausência do journal; obter confirmação visual do operador. Parar imediatamente se a imagem não voltar, a recuperação falhar ou o resultado for incerto.
- A prova de reinício/logon e a prova de `pending` são cenários separados. Não alterar o conteúdo do journal para fabricá-los nem declarar logon validado pelo comando manual.
# Instância única do App

- Build Debug sem avisos ou erros, 253 testes e smoke aprovados.
- `RescueProcessTests.exe --test-app-instance <diretório isolado>` verificou espera pelo listener, encaminhamento com ACK entre processos e transferência da posse.
- O App real foi minimizado para a bandeja. Uma segunda abertura retornou código 0 e tornou visível o mesmo HWND, em estado normal. O journal permaneceu ausente. A automação e as escritas foram desligadas durante a prova, e as preferências originais foram restauradas byte a byte.

## Coordenação de startup

- Build Debug 0/0, 253 testes e smoke aprovados.
- Dois processos rescue com backend falso disputaram o lock durante a restauração de áudio. No sucesso, o iniciador aguardou a limpeza do journal antes de lançar, sem segunda restauração. Na falha, aguardou, preservou o journal e bloqueou o lançamento.
- O executável de produção `--startup-and-launch` retornou 0 e abriu o App real com journal ausente e automação desativada. O App também recebeu uma segunda abertura. Logon do Windows com journal real continua na fila de hardware.

## Recuperação assistida

- Build Debug 0/0, 256 testes (229 Core e 27 App) e smoke aprovados. Os testes do App cobrem a disponibilidade da ação, o encaminhamento do pedido e a reabertura sem reativar a automação.
- Em processos com backend falso, a quarta tentativa automática foi bloqueada. A tentativa assistida esperou a liberação do lock, reportou falha de áudio e preservou o journal. Uma nova tentativa explícita restaurou tela e áudio e removeu o journal e o contador.
- O executável de produção `--assisted-recovery` retornou 0 com journal ausente e reabriu o App real com `--recovery-succeeded`. As preferências usadas na validação foram restauradas. A restauração assistida de um baseline real e a interação física com o botão na janela permanecem sem evidência.
