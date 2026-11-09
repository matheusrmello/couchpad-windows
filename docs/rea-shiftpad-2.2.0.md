# REA estática — ShiftPad 2.2.0 (parcial)

Data: 2026-11-09. Escopo: leitura estática, sem executar o produto ou o rescue, sem tráfego e sem examinar mecanismos de ativação. Este relatório ainda não responde a todas as perguntas necessárias para P4–P6.

## Identidade e método

O índice do bundle do executável instalado foi lido diretamente; `ShiftPad.dll` ocupa 397.312 bytes a partir do offset 48.074.752. Sua extração não executou o binário. A inspeção REA identificou SHA-256 `27436e5e4e3fdf4e83635407ecfc34af87b81a321e1460e263ae25ec70783ff1`, MVID `2b9ae673-62d2-439f-95ce-74d270de95b9`, versão de assembly 2.2.0.0 e alvo .NET 8 (evidência `ev_38b31dfc93dd51af9b9ba613bd39b3e7f422ca2b143666d8154b84ea9bdae102`). Os tokens citados são locais a essa identidade.

O executável completo excedeu o limite REA de snapshot de 128 MiB. O membro `ShiftPad.r2r.dll` extraído tem 133.779.456 bytes, SHA-256 `7eb0458a36e4c3790838c43cbc2d3f1652072d53165f744b5bda4afff0e8ad7a`; seu metadado inspecionado é um manifesto ReadyToRun, não o inventário de tipos principal (`ev_67df992eb02ad0c85db164abdd07d3a560b807c310279dbb6fc2475010bbeaef`).

## Observado

- O assembly principal declara P/Invokes em user32 para `GetDisplayConfigBufferSizes`, `QueryDisplayConfig`, `SetDisplayConfig`, `DisplayConfigGetDeviceInfo`, `DisplayConfigSetDeviceInfo`, `EnumDisplayDevices` e `EnumDisplaySettings` (`ev_5b740afae2d242f38e98d2c52ef274d198e7c8fd00928a4da77e5b93652d2c59`). Declaração não comprova chamada em execução, parâmetros ou resultado.
- O inventário contém tipos de serviço de display e áudio, estruturas nomeadas para informações e estado de cor avançada e máquinas assíncronas com nomes relativos a troca e rescue de display (`ev_ca3c4c8c317c5f2222ea02e96e352f334d4b9b4922167f4e5200e71ef71830b8`, visão `ev_37a21e94bc5c4b1186fbb089eb39ea595c7e44ca36c1b6c89a480c4ea72f8ffe`). Nomes não demonstram o fluxo nem suporte real a HDR.
- O rescue instalado tem SHA-256 `219df12da0f873baf0f3ee745ee3d9ca3d174ac12c3f2ae4a01a82a80cb4f100`. A inspeção de suas tabelas PE teve cobertura completa das importações declaradas (`ev_fe7768dc6136715c5106cf41736bca75cd83525892d136eb1332291e1e93cb07`). Isso não estabelece alcançabilidade ou execução das funções importadas.

## Inferências e lacunas

As declarações são compatíveis com uma implementação baseada em CCD e cor avançada, mas não estabelecem a seleção da TV, EDID, flags usadas, ordem de operações, verificação ou política de rollback. Identificação de endpoints de áudio, semântica HDR, journal e gatilho do rescue continuam desconhecidos. O inventário de strings de usuário teve cobertura parcial; não se deve inferir ausência a partir dele. Nenhum comportamento em hardware foi observado.

Para concluir a REA, examinar arestas e métodos selecionados, incluindo máquinas assíncronas, e correlacionar com o rescue; preservar a separação entre evidência estática e teste de hardware. A implementação CouchPad deve derivar de documentação pública e requisitos próprios, sem copiar código ou assets.

### Inventário adicional de display

A visão de métodos (`ev_ca3c4c8c317c5f2222ea02e96e352f334d4b9b4922167f4e5200e71ef71830b8`, linhas 283–315) contém métodos nomeados `SaveCcdSnapshot`, `SaveHdrStates`, `RestoreHdrStates`, `TryParsePathTarget`, `GetWmiMonitorData` e `GetDisplayConfigFriendlyNames`. Isso é evidência de metadados e de corpos presentes, não prova da ordem de chamadas, dos dados persistidos ou de restauração exata. A investigação das arestas e das máquinas assíncronas continua pendente.

## Conclusões e hipóteses

| Questão | Evidência estática | Hipótese ou desconhecido |
|---|---|---|
| Identificação da TV | Há métodos com nomes relativos a parsing de target path, WMI e nomes amigáveis CCD (`ev_ca3c4c8c317c5f2222ea02e96e352f334d4b9b4922167f4e5200e71ef71830b8`). | A regra efetiva, uso de EDID, precedência e fallback são desconhecidos; os nomes não demonstram identificação inequívoca. |
| Modos consultados/aplicados | Declarações CCD e métodos com nomes relativos a mudança de alvo e configuração estão presentes. | Os modos efetivamente consultados e aplicados são desconhecidos. |
| Flags CCD | Há declaração de `SetDisplayConfig` (`ev_5b740afae2d242f38e98d2c52ef274d198e7c8fd00928a4da77e5b93652d2c59`). | Valores e combinações efetivos são desconhecidos. |
| Captura → apply → verificação | Existem métodos nomeados para captura CCD e troca. | Ordem, verificação posterior e tratamento de falha são desconhecidos. |
| Rollback | Há métodos nomeados para salvar e restaurar configuração e estados HDR. | Condições, exatidão e garantia de rollback são desconhecidas. |
| Rescue lê/grava | A tabela de importações do executável rescue foi inspecionada (`ev_fe7768dc6136715c5106cf41736bca75cd83525892d136eb1332291e1e93cb07`). | Arquivos, formato e operações de leitura/gravação do rescue são desconhecidos. |

Hipótese limitada: os nomes e declarações são compatíveis com consulta de monitores e captura CCD, mas não demonstram que uma execução específica faça essas operações. O desenho do CouchPad é independente dessas hipóteses.
