# Handoff da P6 para a P7a

P6 validada e aceita pelo usuário em 13/11/2026. Referência de código e evidências na tag `v0.6.0-hardware`.

## O que foi validado

- Bandeja Windows com minimizar, abrir, desativar a automação e sair. A desativação de sessão real restaurou tela e áudio.
- Rescue independente no logon e recuperação assistida com baselines reais de tela e áudio. O journal `pending` do teste de logon foi gravado antes de qualquer apply. A primeira tentativa preservou o journal após uma falha Win32 cuja causa não foi reproduzida. A repetição recuperou e removeu o journal.
- Autostart migrou da entrada Run para tarefa por usuário que inicia o rescue antes do App. Às 08:18:20 as duas tarefas retornaram 0 e o App abriu automaticamente com o journal ausente.
- Regressão final com build Debug 0/0, 259 testes, smoke e testes de processo com backend falso. Evidências em [matriz da P6](p6-recovery-matrix.md).

## P7a

Refazer as seis capturas em três larguras, pt-BR e inglês. Revisar contraste, foco, navegação por teclado, leitores de tela e localização, incluindo a bandeja, o autostart e a recuperação assistida. Usar como referência [checklist](ui-a11y-checklist.md) e [fila de validação](validation-queue.md).

HDR e notificações nativas permanecem adiados. A reconexão do watcher durante a contagem tem relato visual e ainda carece de evidência independente. Instalador, assinatura e regressão de distribuição pertencem à P7b.
