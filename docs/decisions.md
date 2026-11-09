# CouchPad — Registro de decisões

> Fonte canônica para o projeto. Copiar para `docs/decisions.md` no repositório.
> Novas decisões entram no fim, com data e ID. Não apagar histórico.

## Confirmadas

| ID | Data | Decisão | Motivo |
|---|---|---|---|
| D001 | 2026-11-09 | Avalonia 12.1.4 + .NET 10 (`net10.0`) | Prova Avalonia existente; XAML/MVVM |
| D002 | 2026-11-09 | pt-BR + inglês | Público-alvo e i18n |
| D003 | 2026-11-09 | App 100% free | Sem licença/ativação/trial/telemetria |
| D004 | 2026-11-09 | UI inspirada na referência, não copiada | Direitos autorais e identidade própria |
| D005 | 2026-11-09 | REA somente estático | Segurança e escopo |
| D006 | 2026-11-09 | Escopo: tela, áudio, HDR e rescue | Foco funcional |
| D007 | 2026-11-09 | Serviço simulado primeiro | Evitar risco de hardware |
| D008 | 2026-11-09 | Fail-closed em hardware | Não deixar a máquina sem display |
| D009 | 2026-11-09 | Journal com escrita atômica | Recuperação confiável |
| D010 | 2026-11-09 | Nome **CouchPad**; repo `couchpad-windows`; identidade original | Marca própria e independência |
| D011 | 2026-11-09 | Licença **MIT**, Copyright (c) 2026 Matheus Mello | Consistência com o projeto `couchpad` existente |
| D012 | 2026-11-09 | Windows 10 1903+ e Windows 11, x64; ARM64 via emulação no v1 | Cobertura realista |
| D013 | 2026-11-09 | Distribuição self-contained, x64 | Sem dependência de runtime |
| D014 | 2026-11-09 | Idioma padrão = cultura do sistema; pt-BR + inglês; fallback inglês | Primeira experiência |
| D015 | 2026-11-09 | Config em `%LOCALAPPDATA%\CouchPad\settings.json`; journal em `state\session.json`; logs em `logs\` | Per-user, sem admin |
| D016 | 2026-11-09 | Primeiro uso com automação desligada e configuração mínima guiada | Evitar mudança acidental |
| D017 | 2026-11-09 | Layout do shell reposicionado em code-behind com 3 breakpoints (`≥1000`, `720–999`, `<720`); janela mínima `640×640`; conteúdo rolável | Alcançar os 3 layouts sem duplicar XAML |
| D018 | 2026-11-09 | P1 é somente UI: toggle de automação alterna `Inativo`/`Aguardando gamepad`; refresh de displays é placeholder de 700 ms; nada de CCD/áudio/HDR | Validar o shell sem risco de hardware |
| D019 | 2026-11-09 | Até glifos curtos (chips `1`/`LB`/`RB`/`A` e unidades `1 s`) vêm dos catálogos; teste de XAML bloqueia texto visível literal | Cumprir "nenhuma string fora dos dicionários" |

## Pendentes

### Antes de P4/P5

1. Identidade do TV (target path + EDID; fallback nome).
2. Modos de display suportados (target-only, estender, espelhar; padrão target-only).
3. API de display (CCD).
4. API de áudio e endpoint padrão (documented APIs; `IPolicyConfig` como fallback).
5. Política de HDR (capturar/reaplicar; não forçar).
6. Acesso a hardware de teste.
7. Privilégios (per-user; admin só se inevitável).
8. Matriz mínima de teste.

### Antes de P6

9. Rescue separado (`CouchPadRescue.exe`) ou flag.
10. Formato/caminho do journal do CouchPad.
11. Gatilho e atraso da tarefa.
12. Lock, concorrência e anti-loop.
13. Bandeja e autostart.
14. Instância única e IPC.

### Antes de P7

15. Instalador (Inno Setup per-user).
16. Assinatura (certificado, self-signed ou nenhuma documentada).
17. Atualização (manual no v1).
18. Canal de release e suporte.
19. Licenças de dependências (`NOTICE`).
20. Definição de v1.0.
21. CI (GitHub Actions).
22. Crash reporting (sem telemetria).
23. Metas de performance.

### Pode esperar

- Tema claro.
- Mais idiomas.
- Modo portátil.
- CLI.
- Auto-update.
- Winget.
- ARM64 nativo.
- Estatísticas locais.

## Formato de nova decisão

```md
## D0XX — Título curto

- Data: AAAA-MM-DD
- Decisão:
- Motivo:
- Impacto:
- Evidência:
```
