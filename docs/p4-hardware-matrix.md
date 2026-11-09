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
