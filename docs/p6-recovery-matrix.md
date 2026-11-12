# P6 — recuperação no logon

## Estado em 12/11/2026

- Implementado comando separado `CouchPad.Rescue.exe --startup-recovery`; a tarefa de logon ainda não está configurada, e o comando não é chamado automaticamente.
- O rescue adquire a posse exclusiva da sessão e o mesmo lock do watchdog antes de inspecionar o journal. Recusa journal ilegível, estado desconhecido e baseline sem topologia ou áudio completo. A restauração usa a decisão transacional e a verificação existentes. O journal é limpo somente após restauração bem-sucedida e confirmação de que seu conteúdo não mudou.
- A exigência de baseline completo significa que journals antigos sem o snapshot de áudio não são restaurados automaticamente; eles são preservados para recuperação assistida.
- Processo real compilado com backend falso em diretório isolado: `pending` e `active` restaurados e journal limpo; falha de áudio preservou journal; recuperação concorrente foi bloqueada; interrupção preservou journal e tentativa posterior terminou. O backend falso não acessa hardware.
- Verificação adicional de processo: journal ausente não iniciou restore; journal corrompido foi preservado sem restore; posse do lock do watchdog bloqueou a recuperação concorrente. Testes unitários também recusaram estado desconhecido e topologia inválida.
- Falha de display injetada no processo fake impediu a etapa de áudio, preservou o journal e permitiu uma tentativa posterior bem-sucedida. Testes unitários também verificaram erro ao limpar o journal e journal que se torna ilegível após restore. Nenhuma dessas falhas foi injetada no backend de hardware.
- Produção, journal ausente: comando encerrou com código 0 e journal ausente; a opção interna de teste foi recusada com código 2. Nenhuma recuperação real foi acionada nesta observação.
- Build Debug 0 avisos/0 erros, 227 testes e smoke aprovados. Nenhuma mudança de tela ou áudio foi executada por estes testes. Em 12/11/2026 o usuário informou que a TV está em uso; a validação física fica pendente até poder acompanhá-la.

## Validações restantes deste item

1. Restaurar um journal `active` real após encerramento controlado do processo principal, com usuário presente, evidência de tela e áudio antes/depois e journal final.
2. Validar `pending` real e reinício/logon, incluindo coordenação com o watchdog, sem inventar estado do journal.
3. Validar falha de recuperação e retenção de journal em cenário seguro, além da evidência com backend falso.
4. Revisar concorrência e integridade, registrar resultados e fazer commit antes de iniciar a integração do agendamento de logon.

## Preparação da validação física

- Confirmar presença do usuário, AOC e Samsung conectados, controle desligado, journal ausente e baseline completo de tela e áudio. Não iniciar se o watchdog ou outra instância mantiver os locks.
- Com a proteção existente ativa, iniciar uma sessão real e registrar journal `active`, topologia e áudio; confirmar imagem com o operador. Encerrar somente o processo de teste identificado e acionar o rescue de produção imediatamente.
- Verificar retorno semântico da topologia, três papéis de áudio, volume/mudo e ausência do journal; obter confirmação visual do operador. Parar imediatamente se a imagem não voltar, a recuperação falhar ou o resultado for incerto.
- A prova de reinício/logon e a prova de `pending` são cenários separados. Não alterar o conteúdo do journal para fabricá-los nem declarar logon validado pelo comando manual.
