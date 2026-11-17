# AGENTS.md — CouchPad Windows

Regras sempre ativas para o OpenCode, único agente de desenvolvimento do projeto.

## Missão

Recriar de forma limpa e 100% gratuita o comportamento útil do ShiftPad 2.2.0:
trocar a tela para a TV, ajustar o áudio, lidar com HDR e oferecer recuperação
segura (rescue).

## Invariantes

1. **100% free**: sem licença, trial, ativação, assinatura ou telemetria.
   Nenhum contato com servidor de licença.
2. **Avalonia 12.1.4** com **.NET 10** (`net10.0`).
3. **pt-BR + inglês** desde a primeira versão. Nenhuma string hardcoded.
4. UI **inspirada** na referência visual; **nunca copiar** ícone, texto, imagem,
   XAML ou código do ShiftPad.
5. **Clean-room**: implementar a partir de comportamento observado, APIs
   públicas e documentação. O binário do ShiftPad serve apenas como evidência
   estática.
6. **REA somente estático**: não executar o ShiftPad, não gerar tráfego de
   ativação, não contornar proteções.
7. **Fail-closed**: nenhuma operação de hardware pode deixar a máquina sem
   display utilizável. Capturar antes, aplicar, verificar e reverter em falha.
8. **Sem admin por padrão**. Per-user em `%LOCALAPPDATA%\CouchPad`.
9. **Sem telemetria**. Logs e diagnóstico ficam locais.
10. Não encerrar, atualizar ou modificar processos de terceiros.

## Fronteira de camadas

| Camada | Pode | Não pode |
|---|---|---|
| `src/CouchPad.App` | Views, ViewModels, tema, i18n, navegação | P/Invoke, COM, `SetDisplayConfig`, Core Audio, Task Scheduler |
| `src/CouchPad.Core` | Modelos, interfaces, simulação, state machine, journal, testes puros | Depender de Avalonia, Win32 ou hardware |
| `src/CouchPad.Platform.Windows` | Implementar interfaces do Core com APIs do Windows | Regra de produto, texto de UI, decisão de fluxo |
| `tests/CouchPad.Core.Tests` | Testes puros, fakes, journal, state machine | Hardware real, tela, áudio |

Uma mudança que cruza a fronteira precisa de decisão registrada em
`docs/decisions.md`.

## Fases e donos

| Fase | Dono | Escopo |
|---|---|---|
| P0 bootstrap | OpenCode | repo, solução, tema, i18n, settings |
| P1 shell | OpenCode | header, cards, footer, layout |
| P2 componentes | OpenCode | catálogo de controles e estados |
| P3 simulação | OpenCode | modelos, fakes, state machine, journal |
| REA 2.2.0 | OpenCode | análise estática do binário |
| P4 display | OpenCode | CCD, identificação de TV, rollback |
| P5 áudio/HDR | OpenCode | endpoint padrão, HDR |
| P6 rescue/tray | OpenCode | rescue, bandeja, autostart, tarefa |
| P7a docs/a11y | OpenCode | docs, a11y, screenshots |
| P7b release | OpenCode | correções, instalador, assinatura |

O OpenCode pode atuar em todas as camadas. A fronteira da seção anterior
continua valendo: `App` não faz P/Invoke/COM, `Core` não depende de Avalonia nem
Win32, e `Platform.Windows` implementa interfaces sem regra de produto.

Mudanças em `Platform.Windows` exigem build/testes e, quando houver hardware
envolvido, a validação correspondente. Subagentes do OpenCode podem ser usados
para implementação, revisão e testes; a responsabilidade final é do OpenCode.

O Codex foi descontinuado em 2026-11-17. Perfis, papéis e handoffs antigos em
`docs/agents/codex/` e `docs/handoff-*codex*.md` são somente históricos.

## Agents e skills

- OpenCode: `.opencode/agents/*.md` (6 agentes).
- Skills: `.opencode/skills/couchpad-*`.
- Codex: descontinuado; documentação histórica em `docs/agents/codex/`.

## Comandos

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"

& $dotnet build CouchPad.slnx -c Debug
& $dotnet test CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --smoke
```

`--smoke` abre a janela, imprime uma linha de diagnóstico e fecha.

## Definição de pronto

1. `dotnet build` e `dotnet test` passam.
2. Nenhuma string fora dos dois dicionários.
3. Estados normal, hover, pressed, focus, disabled, loading e erro.
4. Nenhum P/Invoke fora de `Platform.Windows`.
5. Nenhum código/asset copiado do ShiftPad.
6. `HANDOFF.md` e `docs/decisions.md` atualizados.
7. Revisão independente feita antes do handoff.

## Validação sequencial

Liste as pendências e execute as validações uma por vez: termine cada cenário,
verifique o resultado e registre evidência antes de iniciar o próximo. Continue
automaticamente sem exigir que o usuário peça cada teste. Se faltar uma ação
física indispensável, solicite-a e aguarde; se houver falha de rollback, tela
preta ou falha do watchdog em restaurar, pare imediatamente e reporte o estado.
Não declare aprovado um cenário sem a evidência exigida.

## Handoff

### Publicação de instaladores

Ao gerar uma nova versão do instalador, copie o EXE para Downloads, registre
o SHA-256 e os resultados da validação, faça commit e envie a branch e a tag
para o GitHub. Publique o EXE e o arquivo SHA-256 na release da mesma tag.
Use pré-release para candidatos com validação pendente. Mantenha os binários
fora do histórico Git.

Ao terminar uma fase:

1. Atualize o ledger em `HANDOFF.md`.
2. Registre decisões novas em `docs/decisions.md`.
3. Rode build e testes.
4. Crie a tag da fase.
5. Escreva `docs/handoff-<origem>-to-<destino>.md`.
6. Pare para aceite do usuário.
