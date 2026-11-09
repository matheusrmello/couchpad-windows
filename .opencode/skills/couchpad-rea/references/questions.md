# Perguntas de REA

## Tela

1. Quais APIs: `SetDisplayConfig`, `QueryDisplayConfig`,
   `DisplayConfigGetDeviceInfo`, `ChangeDisplaySettingsEx`,
   `EnumDisplayDevices`, `WindowsDisplayAPI`?
2. Como o TV é identificado: nome amigável, EDID, target device path,
   `DISPLAYCONFIG_TARGET_DEVICE_NAME`?
3. Modos: target-only, extend, clone; flags `SDC_TOPOLOGY_*`, `SDC_APPLY`,
   `SDC_SAVE_TO_DATABASE`.
4. O que é capturado antes: topologia, fonte primária, resolução, escala/DPI.
5. Ordem das operações e tratamento de falha por etapa.
6. Rollback: o que restaura, em que ordem, com qual timeout.
7. Comportamento sem TV, com TV desconectada no meio, com mais de dois monitores.
8. HDR: captura e reaplicação antes/depois da topologia.

**Evidência esperada:** P/Invoke, strings, call edges, comparação com a fonte.

## Áudio

1. `AudioSwitcher.AudioApi.CoreAudio` ou COM direto (`IMMDeviceEnumerator`,
   `IAudioEndpointVolume`)?
2. Usa `IPolicyConfig` (CLSID não documentado) para definir endpoint padrão?
3. Como casa o HDMI: nome, device ID, form factor, endpoint form factor?
4. Retry: intervalo, número de tentativas, timeout, o que faz se não aparecer.
5. Captura/restauração de volume, mute e endpoint anterior.
6. Comportamento com múltiplos endpoints HDMI/DisplayPort.
7. Restauração no disconnect, no exit e no crash.

**Evidência esperada:** P/Invoke/COM, strings de CLSID, nomes de endpoint,
call edges.

## HDR

1. Usa `DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO` e
   `DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE`?
2. Captura por target antes do `SetDisplayConfig`.
3. Reaplica em qual momento; verifica se aplicou?
4. O que faz se o display não suporta HDR ou se a TV está off?
5. Persistência entre reconexões.

**Evidência esperada:** P/Invoke, estruturas, strings, call edges.

## Rescue

1. O que `ShiftPadRescue.exe` lê/grava; caminho e formato do journal.
2. Quando considera a sessão “pendente”.
3. O que restaura: topologia, HDR, áudio, launcher.
4. Gatilho da tarefa: logon, atraso, condição, múltiplas instâncias.
5. Privilégios necessários; Interactive/Limited é suficiente?
6. Concorrência com o app principal; lock?
7. Comportamento se o journal estiver corrompido ou ausente.
8. Como evita loop de recuperação.

**Evidência esperada:** XML da tarefa, imports/strings do rescue NativeAOT,
strings do app principal, relatório.

## Fonte pública × 2.2.0

1. Tipos/métodos presentes no binário e ausentes na fonte.
2. Placeholders da fonte que viraram implementação.
3. Diferença de dependências e versões.
4. Strings de UI/erros que revelam funcionalidades não documentadas.

**Evidência esperada:** `compare_managed_members`, MVID, contagens, strings.

## Regra

Se a evidência não responder, marcar **desconhecido**. Não preencher lacuna com
suposição.
