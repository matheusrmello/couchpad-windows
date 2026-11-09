# JSON de diagnóstico

## Objetivo

Dar ao agente um resumo pequeno e verificável de cada operação de hardware, sem
despejar logs inteiros no contexto.

## Schema

```json
{
  "ts": "2026-11-15T14:03:22.123Z",
  "phase": "display.apply",
  "api": "SetDisplayConfig",
  "args": { "flags": "SDC_APPLY|SDC_USE_SUPPLIED_DISPLAY_CONFIG" },
  "result": "ok",
  "errorCode": 0,
  "durationMs": 412,
  "stateBefore": {},
  "stateAfter": {},
  "rollback": "not_needed",
  "notes": ""
}
```

## Campos

| Campo | Tipo | Regra |
|---|---|---|
| `ts` | ISO 8601 UTC | sempre |
| `phase` | string | `display.capture`, `display.apply`, `audio.capture`, `hdr.reapply`, `rescue.restore` |
| `api` | string | nome da API/COM |
| `args` | objeto | somente flags/parâmetros relevantes |
| `result` | `ok`/`error`/`skipped` | obrigatório |
| `errorCode` | número | código Win32/HRESULT quando houver |
| `durationMs` | número | medição real |
| `stateBefore` | objeto | resumo, não dump |
| `stateAfter` | objeto | resumo, não dump |
| `rollback` | `not_needed`/`ok`/`failed` | obrigatório em erro |
| `notes` | string | curto, sem PII |

## Exemplos de estado

```json
{
  "topology": "extend",
  "primary": "DISPLAY1",
  "targets": ["DISPLAY1", "DISPLAY2"],
  "hdr": { "DISPLAY2": false },
  "audio": { "endpoint": "HDMI-1", "volume": 100, "mute": false }
}
```

## Regras de privacidade

- Não incluir serial, EDID bruto, IP, usuário ou caminho completo.
- Nome de display genérico (`DISPLAY1`, `TV`).
- Nome de endpoint sem ID de hardware sensível.
- Não incluir token, chave ou header.

## Uso pelo agente

- Ler o JSON, não o log completo.
- Correlacionar `stateBefore`/`stateAfter`.
- Em erro, verificar `rollback`.
- Se `rollback = failed`, parar e pedir intervenção.
- Anexar o JSON ao handoff da fase.

## Retenção

- Manter os JSONs em `docs/hardware-tests/<data>/`.
- Não apagar falhas; elas são evidência.
- Revisar antes de anexar a release.
