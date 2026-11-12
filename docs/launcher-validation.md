# Validação de launchers

Data: 2026-11-12. Escopo: Steam e Hydra em Big Picture, um cenário por vez.

## Implementação e limites

- O App oferece uma opção para abrir launcher e um seletor Steam/Hydra. O caminho personalizado antigo é preservado nas configurações, mas não é selecionado nesta versão.
- Após apply e commit do guard, o Core solicita o launcher; falha é mostrada no card e não impede restore. A opção de fechar atua após restaurar tela e áudio.
- Steam usa `-gamepadui`; Hydra usa `--big-picture`, presente no [código público do projeto Hydra](https://github.com/hydralauncher/hydra/blob/main/src/main/index.ts). A presença de um processo anterior impede o CouchPad de reivindicar sua posse. Somente o processo iniciado pelo CouchPad, com PID, horário de início e executável correspondentes, recebe solicitação de fechamento de janela; não há kill forçado ou encerramento de árvore. O fechamento não é garantido quando o launcher transfere a execução para outra instância ou fica na bandeja.
- O usuário adiou HDR e notificações nativas. Não modificar processos de terceiros preexistentes.

## Evidência anterior a hardware

- Build Debug: 0 avisos, 0 erros; 213 testes, smoke OK.
- Fakes: seleção e argumentos dos dois launchers, processo anterior não é fechado, fechamento de processo iniciado, launcher ausente, falhas de abertura/fechamento sem impedir restore e cancelamento durante inicialização.
- Instalações identificadas por leitura: Steam e Hydra 4.4.0; nenhum launcher real foi iniciado por esta implementação ainda.

## Fila de hardware

| Ordem | Cenário | Status |
|---|---|---|
| 1 | Pré-voo: AOC/TV, controle, journal ausente, áudio e operador | Pendente |
| 2 | Steam Big Picture: ativação, imagem, áudio e journal | Pendente |
| 3 | Steam: desligamento e restauração; fechamento conforme opção | Pendente |
| 4 | Hydra Big Picture: ativação, imagem, áudio e journal | Pendente |
| 5 | Hydra: desligamento e restauração; fechamento conforme opção | Pendente |
| 6 | Launcher ausente e launcher previamente aberto; restaurar sem afetar instância anterior | Pendente |

Registrar resultado, operador, topologia e áudio antes/depois antes de avançar; parar em tela preta ou falha de recuperação. Ausência de evidência mantém o cenário pendente.
