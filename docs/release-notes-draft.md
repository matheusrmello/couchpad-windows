# CouchPad 1.0.0

Aplicativo para Windows 10 1903+ e Windows 11, x64. Distribuição por usuário
com runtime .NET incluído, sem ativação, assinatura paga ou telemetria.

## Funcionalidades

- UI em pt-BR e inglês, com bandeja e início opcional no logon do Windows.
- Simulação local por padrão. Controle físico via XInput e escritas de tela
  e áudio exigem opções explícitas.
- Modos Somente TV e Estender, seleção de áudio HDMI e atrasos configuráveis
  para ativação e restauração.
- Steam e Hydra em Big Picture, watchdog e recuperação assistida ou no logon.

## Instalação

O instalador `CouchPad-Setup-1.0.0-win-x64.exe` instala em
`%LOCALAPPDATA%\Programs\CouchPad`, com atalho no menu Iniciar e tarefa
de recuperação por usuário. A desinstalação remove os binários, o atalho
e as tarefas próprias. Configurações e diagnósticos são preservados por projeto.

SHA-256: `23D28331B7A45081EA6812CCD3E66CDC2EBC22292145069DFE5845C0591834F0`.
O instalador, o App e o rescue estão sem Authenticode. O SmartScreen pode
advertir ou bloquear conforme a política local. [Microsoft Learn](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)

## Limitações

- HDR e notificações nativas foram adiados. Controles sem XInput ficam fora do watcher.
- Reconexão durante a contagem tem relato visual e carece de evidência independente.
- Narrator/NVDA completo e leitura do menu nativo da bandeja ficaram pendentes.
  A P7a validou a árvore de acessibilidade por UIAutomation.
- A morte dentro de uma chamada travada de `SetDisplayConfig` não foi reproduzida.
  A limitação foi aceita na P4.
- Endpoint tardio, papéis distintos e migração de stream existente têm evidência
  apenas com fakes.

## Validação e suporte

Builds Debug e Release passaram com 0 avisos, 0 erros e 259 testes em cada
configuração. O smoke e os testes de processo do watchdog e do rescue passaram.
Instalação, primeiro uso, bandeja, autostart, reinstalação e remoção foram
validados em conta padrão separada. Veja [evidência e procedimento de retorno](install-validation.md).

Ao reportar uma falha, inclua versão, passos para reproduzir, estado da automação
e trechos relevantes do log local em `%LOCALAPPDATA%\CouchPad\logs`. Omita
informações pessoais.
