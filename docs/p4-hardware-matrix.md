# Matriz de hardware — P4

Data: 2026-11-09. A P4 não está implementada nem aceita. Nenhum teste desta matriz foi considerado aprovado pela mera presença de monitores.

Consulta WMI somente leitura detectou dois monitores ativos, IDs de produto `SAM7167` e `AOC2702`. Isso não confirma a identidade de TV, conexão, modo ou capacidade dos dispositivos. O serviço real ainda não está disponível para executar a matriz.

| Cenário | Verificação obrigatória | Resultado |
|---|---|---|
| Identificação | target path e IDs EDID válidos; fallback por nome inequívoco; ambiguidade recusa | Pendente |
| TvOnly | Somente alvo escolhido ativo, saída visível e baseline restaurado | Não executado |
| Extend | Alvos esperados ativos e área estendida; baseline restaurado | Não executado |
| Mirror | Removido do escopo por decisão do usuário; snapshots espelhados continuam restauráveis | Não aplicável |
| Modo indisponível | Validação recusa antes da mudança | Não executado |
| Sem TV / alvo ambíguo | Nenhuma alteração | Não executado |
| Falha ou cancelamento após alteração | Rollback e confirmação da topologia anterior | Não executado |
| Desconexão durante transição | Recuperação e pelo menos uma saída utilizável; registrar limitações | Não executado |
| Reinício com journal pendente | Snapshot completo recuperável pela futura P6 | Não executado |

Registrar para cada execução hardware, conexões, topologia inicial, resultado de cada API, verificação visual, baseline final e operador. Uma falha de rollback não pode ser tratada como aprovação.

## Probe CCD somente leitura — 2026-11-09

Executado `dotnet tools/DisplayProbe/bin/Debug/net10.0/DisplayProbe.dll` na sessão disponível: QueryDisplayConfig com QDC_ONLY_ACTIVE_PATHS retornou 1 path e 2 registros de modo; o target reportou nome `27G2G4`, EDID fabricante 58117 e produto 9986. Isto não identifica uma TV nem estabelece a configuração de outras sessões. O JSON emitido contém schema 1, flags, bytes de paths/modes e caminho e identificadores de target. Não houve chamada SetDisplayConfig.

O slice é incompleto: o estado abstrato Mode/PrimaryActive/TvActive ainda não é mapeado a partir da topologia e não deve ser usado para automação; ApplyAsync e RestoreAsync sempre recusam escrita. Falta gate configurável, validação, aplicação, verificação e rollback. A matriz de alterações continua não executada.

## Probe QDC_ALL_PATHS — 2026-11-10

Consulta somente leitura: baseline `QDC_ONLY_ACTIVE_PATHS` contém um path ativo para AOC 27G2G4, target 24646. A consulta separada `QDC_ALL_PATHS` encontrou a Samsung `SAMSUNG`, target 41015, fabricante EDID 11596, produto 29031 e o caminho SAM7167 informado. A seleção por caminho e EDID encontrou um único target, apesar de múltiplos paths candidatos. A Samsung não apareceu no baseline ativo. Nenhuma topologia foi modificada.

O serviço permanece **sem integração à automação**: os campos abstratos do `DisplayState` ainda são placeholders e não devem ser consumidos como estado real. Escritas são sempre recusadas. A implementação futura precisa construir e validar paths que ativem o target inativo, verificar a configuração e restaurar o baseline; a matriz de mudança requer sessão e autorização separadas.

## Mapeamento de baseline — 2026-11-10

O probe somente leitura, configurado com path/EDID da Samsung, retornou `InternalOnly`, `PrimaryActive=true`, `TvActive=false`, com Samsung target 41015 selecionada. Testes sintéticos cobrem os quatro resumos e recusa de mais de um target não TV. O mapeamento trata o único target ativo diferente da TV como interno; mais de um é recusado. O snapshot ativo continua sendo a fonte serializada de topologia.

**Pendente:** implementação de apply e restore, validação de modos, ativação do target inativo, verificação pós-apply e rollback. `AllowWrites` é falso por padrão; mesmo se verdadeiro, os métodos atualmente recusam a operação. Nenhuma alteração de topologia foi executada. A matriz de mudanças permanece pendente e exige autorização separada.

## Planejamento CCD — 2026-11-10

`DisplayTopologyPlanner` constrói, sem chamar APIs Windows, uma proposta de paths para `TvOnly`, `Extend` ou `Mirror` a partir do baseline ativo e dos paths enumerados. Exige exatamente um target não TV ativo; seleciona o primeiro path CCD disponível para a TV com source igual (clone/TV-only) ou diferente (extensão), preservando a ordem enumerada. Índices de modos são marcados inválidos para que uma etapa posterior possa pedir ao Windows que determine os modos. A proposta não foi validada, aplicada ou testada em hardware; `AllowWrites` continua sem habilitar escrita. A validação, verificação e rollback permanecem pendentes.

## Validação CCD sem alteração — 2026-11-10

Na sessão local, baseline `InternalOnly`: AOC 27G2G4 ativo; Samsung SAM7167 selecionada por path/EDID e inativa. `SDC_VALIDATE | SDC_USE_SUPPLIED_DISPLAY_CONFIG` com índices de modo inválidos retornou `ERROR_SUCCESS (0)` para cada proposta: `TvOnly`, `Extend` e `Mirror`. Nenhum `SDC_APPLY` foi chamado; validação não comprova imagem visível ou rollback. Operador: Codex; conexões físicas e verificação visual ainda não observadas. Build sem avisos/erros; 77 testes e smoke aprovados.

## Implementação inicial de escrita — 2026-11-10

`ApplyAsync` valida a proposta CCD, captura o baseline, aplica somente com `AllowWrites=true`, consulta o estado resultante e tenta restaurar o snapshot em falha ou cancelamento. `RestoreAsync` valida e aplica o snapshot; falhas também tentam retornar ao estado anterior. Erros do rollback são preservados junto ao erro original. A verificação de restore compara os bytes de paths e modes integralmente; o driver pode retornar dados equivalentes com diferenças, portanto há risco de falso negativo. O planejador ainda exige um display não TV ativo; transições a partir de `TvOnly` podem ser recusadas. Não há watchdog independente nem rescue de logon implementado. Nenhuma mudança de topologia foi realizada nesta etapa. Testes unitários cobrem a sequência de transação, não o driver nem a matriz de hardware.

### P4 — backend falso, 2026-11-10

Testes com backend CCD falso verificam que falha de validação impede apply e que falha de verificação ou cancelamento após apply tenta restaurar o baseline. Build 0 avisos/erros; 84 testes e smoke aprovados. São testes de fluxo: não comprovam rollback no driver ou imagem visível. Recuperação independente em falha de processo e matriz real permanecem pendentes; não houve alteração de topologia.

## Revalidação sem apply — 2026-11-10

Build do HEAD `cc4d6c3`: 0 avisos/0 erros; 84 testes aprovados; smoke OK. O probe `SDC_VALIDATE` retornou 0 para TvOnly, Extend e Mirror; o target selecionado foi Samsung 41015 com EDID 11596/29031, e o baseline consultado era InternalOnly. O probe usa o conjunto read-only/validador, sem exercitar o caminho `ApplyAsync` do HEAD. Nenhuma alteração de topologia foi realizada. A matriz de apply, imagem visível e rollback permanece não executada; sem recuperação independente do processo, não há evidência suficiente para considerar o teste destrutivo seguro ou P4 aceita.

### P4 — planejador com seletor explícito, 2026-11-10

O planejador recebe targets explícitos de TV e monitor interno e considera paths disponíveis mesmo quando o monitor interno está inativo. Doze combinações sintéticas cobrem os quatro estados de origem e os três destinos. Build 0 avisos/erros, 96 testes e smoke aprovados. Estas propostas não foram validadas novamente com o driver. O watchdog independente, verificação semântica e matriz real continuam pendentes; nenhuma topologia foi modificada.

### P4 — comparação semântica inicial, 2026-11-10

A restauração compara identities de sources/targets, orientação, escala e campos dos modos explicitamente referenciados; ignora ordem dos arrays e campos de status dos paths. Snapshot inválido ou índice de modo ausente é rejeitado. Testes sintéticos cobrem status alterado e alteração de resolução/identidade; ainda não há evidência de normalizações reais do driver. Build 0 avisos/erros, 97 testes e smoke passaram. Watchdog independente e sua integração permanecem pendentes. Nenhum apply real foi executado.

### P4 — contrato do rescue guard, 2026-11-10

Core/App no commit `0281b7d`: contrato de guard e lease com testes simulados; 104 testes aprovados no estado atual. A implementação do watchdog Windows ainda não foi concluída. O comparador semântico não comprova normalizações reais. Nenhuma alteração de topologia foi executada.

### P4 — laço puro do watchdog

Testes simulados do laço de decisão passaram (110 testes totais, build 0 avisos/erros). Processo, IPC, journal e crash real ainda não foram testados ou implementados. Nenhum SDC_APPLY foi executado.

### Watchdog em implementação, 2026-11-10

Processo separado e guard em rascunho, sem testes de processo, crash ou integração. A última revisão ainda não foi compilada: a revisão automática de permissão atingiu o limite de uso. Nenhum SDC_APPLY foi executado. Matriz real permanece pendente.

### P4 — testes de processo do watchdog, 2026-11-10

O harness `tools/RescueProcessTests` executou processos separados com filho falso e com o código do executável rescue compilado em `tools/RescueDryRun` usando backend de restauração falso (`RESCUE_TEST_BACKEND`). Passaram: commit, abort, abandon, timeout, dispose sem sinal imediato, quebra do pipe e morte de processo-pai criado pelo teste antes e após um apply **simulado**. No código rescue com backend falso, passou também a recusa de segunda instância. O teste encerra apenas o processo de teste que ele próprio criou; nenhum SDC_APPLY foi chamado.

Estes resultados não demonstram restauração no driver, kill durante apply real, nem integração do guard ao App; o watchdog ainda não está pronto para hardware. Build completo: 0 avisos/erros; 110 testes unitários e smoke passaram. Implementação e testes estão sem commit.

### P4 — composição do guard, 2026-11-10

Composição opt-in cria serviço Windows e guard em conjunto, ambos sem permissão de escrita/recuperação. O guard desabilitado recusa iniciar processo, evitando um restore involuntário após um apply recusado. Build 0 avisos/erros; 114 testes unitários e smoke passaram. O rescue inicia da saída Debug; empacotamento e matriz real ainda não validados. Nenhum SDC_APPLY foi executado.

### P4 — revisão do watchdog e publicação, 2026-11-10

Revisão em `docs/p4-watchdog-review.md`: há corridas impeditivas na vida do watchdog, timeout e ACK. A publicação win-x64 self-contained foi corrigida e testada com o rescue presente. Build 0 avisos/erros, 114 testes e smoke passaram. Nenhum apply de hardware foi executado; matriz real pendente.

### P4 — acompanhamento sem apply, 2026-11-10

Testes fake: cancelamento após envio e quebra de pipe antes do ACK passaram. Duas capturas do estado `InternalOnly` atual foram consideradas semanticamente iguais; não houve restore nem mudança de topologia. Concorrência timeout/apply e morte do watchdog após READY seguem pendentes. Build 0 avisos/erros; 115 testes e smoke passaram.


### P4 — monitoração do watchdog, 2026-11-10

Com backend falso, perda do watchdog antes/durante/depois de apply provocou erro e preservou o journal; uma chamada falsa não cancelável terminou antes do rollback. Um processo rescue de teste saiu após READY e sua perda foi sinalizada; liberações normais não foram classificadas como falha. Build 0 avisos/erros, 120 testes e smoke passaram. Sem apply real; matriz de hardware e corridas timeout/ACK pendentes.

### P4 — testes de processo e isolamento, 2026-11-10

Com o código do rescue em processo separado e backend falso, passaram commit, abort, abandon, timeout, dispose, perda de ACK, pipe quebrado, morte do pai antes/depois do apply simulado, saída após READY, instância única e timeout aguardando lock de mutação. Testes unitários cobrem barreira entre decisão e chamada, journal prévio/ilegível, falha de limpeza e revogação de escrita; 162 testes passaram, build sem avisos/erros e smoke OK. A revisão independente detectou conflito entre instâncias simulada e real pelo journal; ambas agora obtêm o mesmo lock antes da inspeção. Nenhum SDC_APPLY real foi executado nesta rodada. A chamada CCD é síncrona e, se travar, pode impedir o watchdog de obter o lock até o processo encerrar; timeout não garante recuperação em prazo fixo. A matriz real e confirmação visual pelo operador continuam pendentes.

Após revisão, o Core também evita restore quando a plataforma comprova que o apply falhou antes de tentar mutação, inclusive na recusa de validação (D042); 163 testes passaram. Em processos separados, o timeout esperou uma chamada falsa liberar as travas da decisão e de mutação; o rescue com backend falso restaurou depois. Isso complementa o teste que retém somente o lock de mutação. A matriz real permanece pendente.

### P4 — primeira transição real, 2026-11-10

Operadores: Codex na interface e usuário na observação física. A partir de `InternalOnly` (AOC 27G2G4 ativo, Samsung SAM7167 inativa), a automação ativou `TvOnly`; o App exibiu `Ativo` e a consulta CCD retornou `PrimaryActive=false`, `TvActive=true`. O usuário confirmou imagem na Samsung e AOC sem imagem. O controle de computador foi então interrompido por Escape; não foi comandado restore pelo agente. Ao retomar, a consulta CCD encontrou `InternalOnly`, porém o App ainda indicava `Ativo`/TV e o journal real permanecia `active` com baseline. A causa da mudança externa de topologia é desconhecida; isso não comprova restore pelo CouchPad. Às 16:42 UTC-3, a matriz foi interrompida por divergência de estado. Extend, Mirror, falhas e kill real não foram executados. Não considerar P4 aceita nem descartar o journal sem reconciliação.

### P4 — retomada e erro de Mirror, 2026-11-10

O usuário esclareceu que a mudança anterior para o AOC foi feita por configurações do Windows/Win+P. Em seguida, o controle simulado foi desconectado pelo CouchPad: o App voltou a aguardar gamepad, o journal foi removido e o CCD confirmou `InternalOnly`. Como o Windows já havia retornado ao AOC, isso não comprova restore a partir de `TvOnly`.

Na sequência, `Extend` partiu de `InternalOnly`; CCD reportou `Extend` com AOC e Samsung ativos, e o usuário confirmou imagens em áreas diferentes. Ao desconectar no CouchPad, o CCD voltou a `InternalOnly` e o journal foi limpo. Em `Mirror`, partindo novamente de `InternalOnly`, o App mostrou **Erro**. A causa e a etapa da falha não foram identificadas; não há prova de que o modo espelhado tenha sido aplicado nem de rollback pós-apply. Após o erro, CCD reportou `InternalOnly`, journal ausente e o usuário confirmou imagem normal no AOC. Matriz interrompida às 16:52 UTC-3; não executar novos cenários até apurar o erro. Sem evidência de Mirror, kill durante apply ou demais falhas reais, P4 permanece não aceita.

### Alteração de escopo — 2026-11-10

O usuário retirou a função de espelhamento. D043 remove a opção e recusa sua aplicação antes de qualquer escrita, mantendo leitura e restore de baseline espelhado. A falha observada fica registrada como evidência histórica; os cenários restantes da P4 são TvOnly com restore direto, falhas/cancelamento, alvo indisponível ou ambíguo e crash com watchdog. Não foram executadas novas alterações de topologia nesta alteração de escopo.

### P4 — TvOnly e restauração direta, 2026-11-11

Operação pelo Codex e confirmação visual pelo usuário. Com AOC 27G2G4 ativo, Samsung SAM7167 conectada e inativa e journal ausente, o App executou `TvOnly` após a contagem configurada. A consulta CCD registrou `TvOnly`, `PrimaryActive=false`, `TvActive=true`; o usuário confirmou imagem na Samsung e AOC sem imagem. O controle de computador foi interrompido por Escape antes da restauração; ao retomar, CCD ainda registrava `TvOnly` e o journal `active` continha o baseline. O agente acionou **Desconectar** no CouchPad, sem usar Win+P. O App retornou ao monitor principal e à espera por controle; o journal deixou de existir, e consulta CCD às 07:20 UTC-3 registrou `InternalOnly`, `PrimaryActive=true`, `TvActive=false`. O usuário confirmou imagem normal no AOC e ausência da área de trabalho na TV. Duas consultas finais foram semanticamente equivalentes. O App não exibiu erro; os códigos individuais das APIs e tempos de apply/restore não foram capturados. Controle e áudio permanecem simulados. Esta evidência não cobre falha, cancelamento ou kill real durante apply.
