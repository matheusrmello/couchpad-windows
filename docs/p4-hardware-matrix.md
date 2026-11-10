# Matriz de hardware — P4

Data: 2026-11-09. A P4 não está implementada nem aceita. Nenhum teste desta matriz foi considerado aprovado pela mera presença de monitores.

Consulta WMI somente leitura detectou dois monitores ativos, IDs de produto `SAM7167` e `AOC2702`. Isso não confirma a identidade de TV, conexão, modo ou capacidade dos dispositivos. O serviço real ainda não está disponível para executar a matriz.

| Cenário | Verificação obrigatória | Resultado |
|---|---|---|
| Identificação | target path e IDs EDID válidos; fallback por nome inequívoco; ambiguidade recusa | Pendente |
| TvOnly | Somente alvo escolhido ativo, saída visível e baseline restaurado | Não executado |
| Extend | Alvos esperados ativos e área estendida; baseline restaurado | Não executado |
| Mirror | Alvos esperados compartilham origem e imagem visível; baseline restaurado | Não executado |
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
