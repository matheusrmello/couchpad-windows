# Revisão do protocolo do watchdog — P4

Data: 2026-11-10. Escopo: leitura do código, testes de processo com backend falso previamente registrados e publicação Release `win-x64` self-contained. Nenhum SDC_APPLY foi executado.

## Resultados

- O guard usa pipe nomeado com `CurrentUserOnly` e nome aleatório. O filho verifica journal, baseline e identidade do processo-pai antes de `READY`. O filho usa lock exclusivo por usuário e caminho do journal. Os cenários fake já cobrem commit, abort, timeout, pipe quebrado e morte do pai, mas não provam comportamento do driver nem esgotam corridas.
- A primeira publicação de teste incluía o apphost do rescue, mas omitia `CouchPad.Rescue.dll`. A inclusão explícita corrigiu a publicação testada: exe, DLL, deps e runtimeconfig estavam presentes; executar o rescue sem argumentos retornou 2. Este teste não valida instalador nem publicação single-file.

## Pendências impeditivas para escrita real

1. **Morte do watchdog após READY:** o pai não acompanha a vida do filho durante `ApplyAsync`. Se o filho morrer depois do handshake, a alteração pode começar sem rescue independente; a falha só é percebida ao finalizar o lease. É necessário monitoramento e um protocolo que impeça ou reverta a transição com segurança.
2. **Timeout concorrente com apply:** ao expirar, o filho restaura imediatamente, enquanto o pai pode ainda estar dentro de `SetDisplayConfig`. Não existe exclusão mútua ou fencing entre as operações. O teste fake não cobre o driver bloqueado nem prova que uma restauração permaneça final.
3. **Commit com ACK incerto:** o token do chamador pode cancelar escrita/leitura após o filho receber COMMIT; o filho pode encerrar enquanto o coordenador interpreta falha e inicia rollback. Se o envio de ACK falhar, o catch genérico do filho retorna erro sem garantir restore. É preciso definir o ponto de conclusão e testar cancelamento, quebra e morte em cada etapa.
4. **Falha do rescue:** o filho retorna código 1 sem registrar diagnóstico local durável; após morte do pai, não há observador que apresente a falha ou garanta nova tentativa. A recuperação no logon pertence à P6 e ainda não está implementada.

Não habilitar `AllowWrites`/`allowRecovery` na composição enquanto essas pendências não forem resolvidas e exercitadas com backend falso. Depois, realizar a matriz com operador presente e recuperação independente comprovada. A comparação semântica de restore também precisa ser confrontada com normalizações reais do driver.
