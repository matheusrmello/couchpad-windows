# CouchPad

Troca de tela, áudio e HDR para Windows, com recuperação segura. **100% gratuito**:
sem licença, ativação, assinatura ou telemetria.

Aplicativo inspirado no comportamento do ShiftPad, implementado de forma
independente (clean-room) em Avalonia + .NET 10.

## Status

**P0 — bootstrap** concluído:

- solução, projetos e testes;
- tema escuro com tokens;
- i18n pt-BR + inglês com troca em tempo real;
- configurações em `%LOCALAPPDATA%\CouchPad\settings.json`;
- janela inicial com smoke test.

## Requisitos

- Windows 10 1903+ ou Windows 11, x64.
- .NET 10 SDK para desenvolvimento.
- Avalonia 12.1.4.

## Estrutura

```text
src/CouchPad.App/                # UI Avalonia, ViewModels, tema, i18n
src/CouchPad.Core/               # modelos, interfaces, simulação, journal
src/CouchPad.Platform.Windows/   # P/Invoke/COM (P4–P6; vazio no P0)
tests/CouchPad.Core.Tests/       # xunit
docs/                            # decisões, spec de UI, agentes
.opencode/                       # agentes e skills do OpenCode
```

## Build e testes

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"

& $dotnet build CouchPad.slnx -c Debug
& $dotnet test CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --smoke
```

## Agentes

- **OpenCode**: selecione `orchestrator` em `/agents` para as fases P0–P3/P7a.
- **Codex**: use `codex -p rea`, `codex -p hardware`, `codex -p release` ou
  `codex -p review`. Os papéis ficam em `docs/agents/codex/roles/`.
- **Skills**: `.opencode/skills/couchpad-*`.

## Escopo

- **Tela**: identificar a TV, aplicar o modo escolhido e reverter em falha.
- **Áudio**: selecionar o endpoint correto.
- **HDR**: capturar, aplicar e restaurar.
- **Rescue**: recuperar a sessão após falha ou logoff.

Fora do escopo: licença, ativação, trial, telemetria, cópia de código/assets de
terceiros.

## Licença

MIT. Ver [LICENSE](LICENSE).
