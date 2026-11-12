# Validação de launchers

Data: 2026-11-12. Escopo: Steam e Hydra em Big Picture, um cenário por vez.

## Implementação e limites

- O App oferece uma opção para abrir launcher e um seletor Steam/Hydra. O caminho personalizado antigo é preservado nas configurações, mas não é selecionado nesta versão.
- Após apply e commit do guard, o Core solicita o launcher; falha é mostrada no card e não impede restore. A opção de fechar atua após restaurar tela e áudio.
- Steam usa `-gamepadui`; Hydra usa `--big-picture`, presente no [código público do projeto Hydra](https://github.com/hydralauncher/hydra/blob/main/src/main/index.ts). A presença de um processo anterior impede o CouchPad de reivindicar sua posse. Para Steam iniciado e identificado pelo CouchPad, ao restaurar é solicitado `steam://close/bigpicture`, mantendo o Steam aberto, conforme escolha do usuário; para Hydra solicita-se fechamento da janela do processo próprio. Não há kill forçado ou encerramento de árvore. O envio da solicitação não garante que a interface mudou; é preciso confirmação visual.
- O usuário adiou HDR e notificações nativas. Não modificar processos de terceiros preexistentes.

## Evidência anterior a hardware

- Build Debug: 0 avisos, 0 erros; 213 testes, smoke OK.
- Fakes: seleção e argumentos dos dois launchers, processo anterior não é fechado, fechamento de processo iniciado, launcher ausente, falhas de abertura/fechamento sem impedir restore e cancelamento durante inicialização.
- Instalações identificadas por leitura: Steam e Hydra 4.4.0; nenhum launcher real foi iniciado por esta implementação ainda.

## Fila de hardware

| Ordem | Cenário | Status |
|---|---|---|
| 1 | Pré-voo: AOC/TV, controle, journal ausente, áudio e operador | Concluído |
| 2 | Steam Big Picture: ativação, imagem, áudio e journal | Concluído |
| 3 | Steam: desligamento, restauração e saída do Big Picture conforme opção | Concluído |
| 4 | Hydra Big Picture: ativação, imagem, áudio e journal | Concluído |
| 5 | Hydra: desligamento e restauração; saída conforme opção | Concluído com ressalva de processo |
| 6 | Launcher ausente e launcher previamente aberto; restaurar sem afetar instância anterior | Pendente |

Registrar resultado, operador, topologia e áudio antes/depois antes de avançar; parar em tela preta ou falha de recuperação. Ausência de evidência mantém o cenário pendente.

### Pré-voo somente leitura, 12/11/2026

- `InternalOnly`: AOC ativo, TV inativa. Os três papéis de áudio apontam ao monitor; volume 1, mudo ligado.
- Journal ausente; consulta de processos não encontrou CouchPad, Steam ou Hydra em execução.
- Configuração persistida: automação, escrita de tela/áudio, gamepad real e launcher habilitados; opção de fechar launcher desligada. Como a skill requer início com automação desligada e launcher desativado no primeiro teste, isso precisa ser preparado antes da matriz real, com o usuário presente.
- Nenhuma tela, áudio ou processo foi alterado por este pré-voo. Presença, imagem, condições físicas e recuperação manual ainda precisam de confirmação na sessão de teste.

### Pré-voo com controle físico, 12/11/2026

- Operador: usuário presente, com AOC, Samsung e controle 8BitDo conectados. Launcher desativado para este cenário.
- Ao ligar a automação com o controle conectado, o journal ficou `active`; o probe identificou somente a TV ativa e os três papéis de áudio no mesmo endpoint Samsung. Nenhum processo Steam ou Hydra foi encontrado. O usuário confirmou imagem na Samsung e AOC apagado.
- Após desligar o controle, o usuário confirmou imagem normal no AOC e Samsung sem área de trabalho. O probe confirmou `InternalOnly`, os três papéis de áudio no endpoint original do AOC e journal ausente; nenhum Steam ou Hydra estava em execução.

### Steam Big Picture, 12/11/2026

- Operador: usuário presente. Steam não estava em execução antes do teste; abertura e fechamento ao restaurar habilitados.
- Ao ligar o controle, usuário confirmou Steam Big Picture na Samsung e AOC apagado. Probe: somente TV, três papéis de áudio na Samsung, journal `active`; Steam PID 2360 observado.
- Ao desligar o controle, usuário confirmou restauração visual, mas Steam permaneceu aberto. Probe: `InternalOnly`, três papéis de áudio no AOC e journal ausente. Steam PID 16848 permanecia ativo; a mudança de PID impede atribuir a instância restante com segurança ao processo originalmente iniciado. Fechamento não aprovado e nenhum encerramento forçado foi realizado.
- O card exibiu mensagem de falha do launcher. A implementação também passou a reportar falha quando o PID rastreado desaparece e outro processo do launcher permanece, em vez de considerar esse caso fechado. Build 0/0, 214 testes e smoke aprovados; repetição em hardware pendente.
- Repetição após correção: usuário confirmou Big Picture na Samsung e AOC apagado. Probe: journal `active`, somente TV, três papéis de áudio na Samsung; Steam PID 12760. Ao desligar o controle, usuário informou tela restaurada e Steam ainda aberto. Probe: `InternalOnly`, três papéis de áudio no AOC, journal ausente; o mesmo PID 12760 ainda ativo, sem janela principal detectada. Portanto o cenário de fechamento continua reprovado; não se trata apenas de troca de PID.
- Após o usuário escolher sair apenas do Big Picture, o teste foi repetido com Steam fechado inicialmente e controle físico. Usuário confirmou Big Picture na Samsung com AOC apagado; probe confirmou somente TV, três papéis de áudio na Samsung e journal `active`. Ao desligar o controle, usuário confirmou AOC normal, TV sem área de trabalho e saída do Big Picture com Steam no modo normal. Probe confirmou `InternalOnly`, três papéis de áudio no AOC e journal ausente; Steam PID 15176 permaneceu em execução. Cenário aprovado com a semântica escolhida.

### Hydra Big Picture, 12/11/2026

- Operador: usuário presente; Hydra não estava em execução antes do teste, opção de saída habilitada. Ao ligar o controle, usuário confirmou Hydra Big Picture na Samsung e AOC apagado; probe confirmou somente TV, três papéis de áudio na Samsung e journal `active`.
- Após desligar o controle, usuário confirmou AOC normal, TV sem área de trabalho e saída do modo de jogo ou fechamento do Hydra. Probe confirmou `InternalOnly`, três papéis de áudio no AOC e journal ausente. Processos Hydra, inclusive uma janela intitulada Hydra Launcher, continuaram presentes; não se afirma que o aplicativo encerrou por completo. Nenhum processo foi encerrado à força.
- Instância Hydra preexistente: ao ligar o controle, a Samsung mostrou Windows, mas o Hydra não veio para primeiro plano (usuário). Probe: somente TV, áudio Samsung e journal `active`. Após desligar, usuário confirmou AOC normal e Hydra ainda aberto; probe confirmou `InternalOnly`, áudio AOC e journal ausente. A preservação da instância passou; a apresentação do Hydra preexistente falhou e precisa de correção e repetição.
