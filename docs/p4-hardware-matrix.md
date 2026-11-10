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
