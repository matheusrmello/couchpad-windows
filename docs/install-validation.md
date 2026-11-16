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
