# P5 — áudio e HDR

## Inventário somente leitura — 2026-11-11

`tools/AudioProbe` consulta os endpoints de reprodução via Core Audio, seus estados e IDs padrão para os papéis console, multimídia e comunicação. Na sessão de teste foram encontrados oito endpoints, um ativo e um endpoint padrão presente para cada papel; os três papéis apontavam para o mesmo endpoint. Nenhuma saída foi alterada. IDs permanecem na saída local do probe e não são registrados neste documento.

O probe não identifica ainda o endpoint correspondente à TV e não realiza apply ou restore. O contrato atual `AudioOutput` não permite persistir IDs distintos para os três papéis; o journal e o watchdog precisam ser ampliados antes de qualquer escrita real. HDR continua pendente. A seleção de endpoints ativos e papéis é documentada pela Microsoft em [Default Audio Endpoint Selection](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/default-audio-endpoint-selection); a documentação de CCD enumera as operações de cor avançada em [DISPLAYCONFIG_DEVICE_INFO_TYPE](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ne-wingdi-displayconfig_device_info_type).

## Revalidação de áudio somente leitura — 2026-11-11

Build do probe: zero avisos e erros. Na topologia atual foram enumerados oito endpoints de reprodução, um ativo; os três papéis tinham padrão presente, o mesmo entre si e estável em duas leituras. IDs enumerados eram únicos. A consulta PnP de endpoints presentes retornou `27G2G4 (HD Audio Driver for Display Audio)` como dispositivo presente. Isso não identifica por si só o endpoint da Samsung nem comprova a correspondência entre o nome PnP e cada ID Core Audio. Não foi alterado áudio nem testada reprodução. Troca, retry, preservação por papel, volume/mudo, recuperação e áudio físico na Samsung permanecem pendentes.

## HDR adiado por solicitação do usuário

Em 2026-11-11, o usuário solicitou ignorar o teste de HDR nesta etapa e registrar o assunto para implementações futuras. Não executar a matriz de hardware de HDR como requisito da P5 atual nem registrá-la como aprovada. A implementação e a futura validação devem considerar detecção de suporte, captura do estado anterior, reaplicação e recuperação segura, sem forçar HDR em display incompatível. A alteração não dispensa os requisitos e testes de áudio.

## Pendências de áudio — ordem sequencial

Concluir e registrar cada item antes do próximo, conforme `AGENTS.md`; continuar sem pedidos repetidos. Não confundir inventário somente leitura com troca/restauração real. Parar se uma recuperação de hardware falhar ou a tela ficar preta.

1. **Identificação da TV:** expor nome e propriedades, observar endpoint ativo e confirmar som na Samsung; seleção explícita por ID e recusa de ausência/ambiguidade. Validado para o hardware observado; vínculo automático CCD↔endpoint não foi comprovado e seleção de produção permanece por integrar.
2. **Contrato e baseline:** registrar decisão e ampliar o Core e o journal para preservar os IDs padrão por papel e os estados de volume/mudo pertinentes, com compatibilidade e testes de serialização. Captura real somente leitura validada na configuração atual; persistência e papéis distintos validados com fakes. Restauração real pertence aos itens seguintes.
3. **Recuperação independente:** vincular o baseline completo ao watchdog e restaurar áudio com segurança em morte/timeout; validar com backend falso e processos, inclusive falhas e decisões incertas. Pendente.
4. **Implementação de áudio Windows:** seleção por identidade, espera/retry limitado pelo endpoint HDMI, troca por papel, verificação e rollback fail-closed; manter escrita desabilitada por padrão. Pendente.
5. **Integração e testes puros:** coordenar display → áudio → restauração com falha/cancelamento e testes de ausência, indisponibilidade, ambiguidade e papéis distintos. Pendente.
6. **Matriz real:** capturar baseline dos papéis, volume e mudo; ativar TV e confirmar áudio audível na Samsung; desconectar e comprovar restauração anterior; verificar endpoint ausente/tardio, cancelamento, falha e recuperação independente com usuário presente. Pendente.
7. **Fechamento:** build sem avisos/erros, testes, smoke, revisão independente, documentação e commit; pedir aceite do usuário. Pendente. HDR está adiado e não é contado como aprovado.

### Item 1 — preparação

O inventário agora lê `PKEY_Device_FriendlyName` em modo somente leitura. Na topologia `InternalOnly`, foram observados dois registros Samsung com estado `not present` e um AOC ativo; escolher simplesmente por nome seria ambíguo. Foi preparado um instrumento de teste que ativa TvOnly com watchdog, aguarda, consulta nomes/estados dos endpoints e restaura o baseline por falha de áudio simulada. Este instrumento não altera diretamente o padrão de áudio. A disponibilidade e a seleção inequívoca ainda serão verificadas com a TV ativa.

### Item 1 — observação com a Samsung ativa

Com o usuário presente, o instrumento protegido aplicou `TvOnly`, consultou endpoints após três segundos e restaurou o display. Havia um endpoint ativo chamado `SAMSUNG (3- HD Audio Driver for Display Audio)` e um segundo registro Samsung `not present`; os três papéis apontavam para o endpoint ativo. Após restaurar display, a leitura imediata ainda apontava para Samsung; uma leitura posterior apontou para `27G2G4 (HD Audio Driver for Display Audio)` ativo nos três papéis. O usuário confirmou imagem normal no AOC. O instrumento não definiu endpoints; o Windows alterou os padrões ao mudar a disponibilidade. Não foram guardados IDs anteriores para comprovar igualdade exata após a mudança nem foi testada reprodução. A espera demonstra que a disponibilidade e os padrões podem mudar depois de o display voltar.

O seletor puro por nome exato recusa ausência e dois matches ativos; testes sintéticos passaram. A observação identifica um candidato único na topologia experimentada, mas ainda não prova uma associação estável por identidade entre o target CCD e o endpoint de áudio nem implementa a seleção de produção. Item 1 permanece em andamento até essa associação e sua validação serem concluídas.

Como alternativa segura para seleção pelo usuário, há também seletor puro por ID explícito: ele exige exatamente um endpoint ativo com o mesmo ID e recusa ID ausente, inativo ou duplicado; os testes sintéticos passaram. Ainda falta oferecer/integrar essa seleção e comprovar que o ID escolhido corresponde à TV sem inferência por nome.

### Item 1 — tom físico, 2026-11-11

Com TvOnly e watchdog, o endpoint `SAMSUNG (3- HD Audio Driver for Display Audio)` estava ativo e era o padrão multimídia. Um tom de diagnóstico de dois segundos foi iniciado após a troca; o usuário confirmou ouvi-lo pela Samsung e confirmou o retorno da imagem ao AOC. Os IDs padrão dos três papéis retornaram aos valores anteriores cerca de 1,9 segundo depois do retorno da tela, sem alteração explícita de endpoint pelo CouchPad. No teste anterior, o usuário não ouviu na TV o som que já estava tocando: esse fluxo preexistente não foi comprovadamente migrado. O teste de tom prova reprodução iniciada após a mudança do padrão apenas nesta configuração; associação automática universal CCD↔endpoint e seleção de produção ainda não foram comprovadas.

### Item 2 — contrato de baseline, em andamento

D046 introduz `AudioBaseline` versionado com IDs dos três papéis e volume/mudo por endpoint. O campo opcional do journal preserva compatibilidade com entradas antigas, e o hash de rescue inclui o novo baseline quando presente, mantendo o formato de hash anterior quando ausente. O coordenador captura antes do display e persiste em `pending` antes do apply; em restauração usa o baseline completo. Um serviço que forneça baseline real é recusado enquanto o guard não declarar suporte de recuperação de áudio. Testes com fakes cobrem esses fluxos e serialização com papéis distintos. A recuperação independente real ainda não existe; o guard Windows não declara suporte, e nenhuma escrita de áudio está habilitada.

Complemento: `AudioProbe --baseline` capturou na máquina os três papéis e volume/mudo do endpoint padrão; todos os papéis estavam representados, o volume estava no intervalo válido e duas leituras preservaram os mesmos IDs. A compatibilidade do hash anterior e serialização com papéis distintos passaram em testes. Isto valida captura somente leitura nesta configuração, não a restauração real nem papéis físicos distintos; o guard Windows permanece incapaz de restaurar áudio e a escrita continua bloqueada.

## Item 3 — recuperação independente, em andamento

Até o rescue implementar áudio, tanto `WindowsRescueGuard` quanto o executável rescue recusam um journal com baseline real de áudio. Teste do guard confirma recusa antes de iniciar processo e preservação do journal. Isso impede tratar como recuperado um baseline que o processo atual não conseguiria restaurar; ainda não implementa restore de áudio nem substitui os testes de processo/crash do item 3.

Em 2026-11-11, a sequência de restauração foi exercitada com backend falso: validação do baseline, papéis distintos, volume/mudo e verificação; endpoint indisponível ou schema inválido impediram a escrita. No processo rescue compilado exclusivamente para teste, a quebra do pipe acionou restauração simulada de display e áudio; uma falha simulada de áudio preservou a decisão `RecoveryRequested`, sem declarar recuperação. Passaram também os cenários de commit, abort, timeout, abandono e morte do pai. A suíte teve 178 testes aprovados. Esses resultados não demonstram recuperação real de áudio: o executável de produção e o guard continuam recusando baseline de áudio, e a escrita real permanece desabilitada. O item 3 segue pendente até existir backend Windows e serem comprovados os mesmos comportamentos com ele.

### Limite identificado na API de dispositivo padrão

A documentação da Microsoft encontrada descreve [`GetDefaultAudioEndpoint`](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nf-mmdeviceapi-immdeviceenumerator-getdefaultaudioendpoint) para consultar o endpoint, e a [seleção automática](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/default-audio-endpoint-selection) quando a disponibilidade muda. Ela não demonstrou uma API pública documentada equivalente para definir o padrão do sistema por papel. A referência anterior em `docs/decisions.md` sugere `IPolicyConfig` como fallback, mas não estabelece documentação pública ou compatibilidade garantida para essa interface. A mudança automática observada neste computador não garante restauração dos três papéis em outras configurações; por isso o gate permanece fechado.

O usuário autorizou a exceção `IPolicyConfig`, registrada em D047. Existe agora um backend Windows isolado, com escrita desabilitada por padrão, ainda não integrado ao rescue de produção. O teste do gate prova recusa antes de acessar dispositivos; não prova a compatibilidade da interface não documentada, nem substitui recuperação de áudio real. O item 3 continua pendente.

Probe somente leitura, 11/11/2026: `--policy-probe` ativou e liberou a interface COM nesta máquina, sem chamar método de escrita; `--backend-read` conferiu os três papéis e volume/mudo contra o baseline capturado. A ativação não testa a assinatura ou a execução da operação de escrita, tampouco sua compatibilidade em outras versões do Windows.

A restauração simulada agora espera por tempo limitado os endpoints do baseline reaparecerem antes de qualquer escrita. Testes com backend falso cobrem reaparecimento e timeout sem escrita; o rescue de teste usa essa sequência. Isso é relevante porque o AOC só retomou o endpoint cerca de dois segundos depois da restauração da tela nesta máquina.

O caminho de áudio no rescue de teste também adquiriu o lock compartilhado de mutação e executou a restauração sob a decisão `RecoveryRequested`. A suíte de processo passou novamente com sucesso e falha de áudio falsos, e a suíte automatizada passou com 181 testes. Outro processo manteve o lock após o display simulado e confirmou que o rescue aguardou antes de restaurar o áudio; após liberar o lock, a recuperação simulada terminou. O backend real ainda precisa de validação; o gate de produção segue fechado.
