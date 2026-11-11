# P5 — áudio e HDR

## Inventário somente leitura — 2026-11-11

`tools/AudioProbe` consulta os endpoints de reprodução via Core Audio, seus estados e IDs padrão para os papéis console, multimídia e comunicação. Na sessão de teste foram encontrados oito endpoints, um ativo e um endpoint padrão presente para cada papel; os três papéis apontavam para o mesmo endpoint. Nenhuma saída foi alterada. IDs permanecem na saída local do probe e não são registrados neste documento.

O probe não identifica ainda o endpoint correspondente à TV e não realiza apply ou restore. O contrato atual `AudioOutput` não permite persistir IDs distintos para os três papéis; o journal e o watchdog precisam ser ampliados antes de qualquer escrita real. HDR continua pendente. A seleção de endpoints ativos e papéis é documentada pela Microsoft em [Default Audio Endpoint Selection](https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/default-audio-endpoint-selection); a documentação de CCD enumera as operações de cor avançada em [DISPLAYCONFIG_DEVICE_INFO_TYPE](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ne-wingdi-displayconfig_device_info_type).

## HDR adiado por solicitação do usuário

Em 2026-11-11, o usuário solicitou ignorar o teste de HDR nesta etapa e registrar o assunto para implementações futuras. Não executar a matriz de hardware de HDR como requisito da P5 atual nem registrá-la como aprovada. A implementação e a futura validação devem considerar detecção de suporte, captura do estado anterior, reaplicação e recuperação segura, sem forçar HDR em display incompatível. A alteração não dispensa os requisitos e testes de áudio.
