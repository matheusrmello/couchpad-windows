# CouchPad — notas de release (rascunho)

- Versão alvo: `0.6.0-preview`
- Data do rascunho: 2026-11-11
- Estado: **não lançado**; P6/P7b pendentes
- Sem licença, ativação, assinatura ou telemetria

## Destaques

- UI Avalonia pt-BR/en com temas, cards, controles e acessibilidade.
- Simulação completa de gamepad, display e áudio.
- State machine com atrasos canceláveis, journal atômico e persistência.
- Display real com identificação por path/EDID, TvOnly/Extend/Mirror,
  verificação semântica e rollback.
- Watchdog/rescue independente para recuperar display e áudio.
- Áudio real com seleção explícita de endpoint, opt-in, troca/restore e
  recuperação.
- 194 testes, build 0 avisos/erros e smoke.

## Limitações conhecidas

- Watcher de gamepad físico e launcher real ainda não integrados.
- Rescue no logon, bandeja e autostart são P6.
- HDR adiado; não declarar suporte.
- A morte dentro de `SetDisplayConfig` não foi reproduzida; o risco foi aceito
  na P4 com limitação.
- Endpoint tardio/papéis distintos e migração de stream existente têm evidência
  apenas com fakes.
- Escrita real de display/áudio é opt-in e exige TV conectada/usuário presente.
- Instalador e assinatura ainda não existem (P7b).

## Instalação

Ainda não há instalador. Para desenvolvimento:

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"
& $dotnet build CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll
```

## Próximos passos

1. P6: watcher físico, launcher, rescue no logon, bandeja e autostart.
2. P7a: screenshots finais e revisão de a11y.
3. P7b: instalador, assinatura, regressão e tag `v1.0.0`.
