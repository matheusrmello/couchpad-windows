# CouchPad

Troca de tela, áudio e HDR para Windows, com recuperação segura. **100% gratuito**:
sem licença, ativação, assinatura ou telemetria.

Aplicativo inspirado no comportamento do ShiftPad, implementado de forma
independente (clean-room) em Avalonia + .NET 10.

## Status

- **P0–P3 aceitas:** shell, componentes, simulação, state machine, journal e
  persistência.
- **P4 display aceita com limitação:** TvOnly/Extend, rollback, falha,
  cancelamento e watchdog em hardware; a morte dentro de `SetDisplayConfig`
  não foi comprovada e a recuperação no logon fica na P6.
- **P5 áudio aceita com limitações:** seleção e troca de endpoint, restore,
  falha/cancelamento, endpoint ausente e rescue em hardware; HDR adiado;
  endpoint tardio/papéis distintos só com fakes.
- **P6 aceita:** bandeja, autostart por tarefa, rescue no logon, recuperação
  assistida e instância única validados; tag `v0.6.0-hardware`.
- **P7a em andamento:** a11y, i18n, screenshots oficiais e release notes;
  aceite pendente.
- **P7b pendente:** instalador, assinatura e release v1.0.0.
- **Adiados:** HDR e notificações nativas; a reconexão do watcher durante a
  contagem ainda carece de evidência independente.

Build atual: 0 avisos/0 erros; **259 testes**; smoke OK. Versão de
desenvolvimento: `0.6.0`.

## Requisitos

- Windows 10 1903+ ou Windows 11, x64.
- .NET 10 SDK para desenvolvimento.
- Avalonia 12.1.4.

## Estrutura

```text
src/CouchPad.App/                # UI Avalonia, ViewModels, tema, i18n
src/CouchPad.Core/               # modelos, interfaces, simulação, journal
src/CouchPad.Platform.Windows/   # P/Invoke/COM de display/áudio
src/CouchPad.Rescue/             # rescue/watchdog independente
tests/CouchPad.Core.Tests/       # xunit
tests/CouchPad.App.Tests/        # composição/UI sem hardware
tools/                           # probes e instrumentos de hardware
docs/                            # decisões, specs, matrizes, screenshots
.opencode/                       # agentes e skills do OpenCode
```

## Build e testes

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"

& $dotnet build CouchPad.slnx -c Debug
& $dotnet test CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --smoke
```

A escrita real de display/áudio é **opt-in** e desligada por padrão. Use apenas
com a TV conectada e o usuário presente; leia `docs/p4-runbook.md` e
`docs/p5-audio-hdr.md`.

## Funcionalidades

- UI pt-BR/en com cards, controles, estados e acessibilidade.
- Simulação de display, áudio e gamepad.
- State machine com atrasos canceláveis, journal atômico e persistência.
- Display real: identificação por path/EDID, TvOnly/Extend/Mirror, rollback e
  watchdog independente.
- Áudio real: seleção explícita de endpoint, troca/restore e recuperação.
- Watcher XInput opt-in, launcher Steam/Hydra Big Picture, bandeja, autostart,
  rescue no logon e recuperação assistida.
- HDR e notificações nativas: adiados; não declarar suportados.

## Lacunas conhecidas

- A reconexão do watcher durante a contagem tem relato visual, mas falta
  evidência independente com timestamps/eventos.
- Dispositivos HID sem XInput estão fora do watcher atual.
- HDR e notificações nativas adiados.
- Instalador/assinatura são P7b.
- `SetDisplayConfig` travado e certas normalizações de driver não foram
  reproduzidos; ver limitações nas matrizes.
- Narrator/NVDA completo ainda não foi executado; UIAutomation foi validado.

## Agentes

- **OpenCode/DeepSeek:** App/Core, UI, i18n, testes, docs e P7a.
- **Codex:** REA, `Platform.Windows`, P4–P6 e P7b (instalador/release).
- Skills: `.opencode/skills/couchpad-*`.

## Licença

MIT. Ver [LICENSE](LICENSE).
