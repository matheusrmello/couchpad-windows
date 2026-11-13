# CouchPad — notas de release (rascunho)

- Versão alvo: `0.6.0-preview`
- Data do rascunho: 2026-11-13
- Estado: **P7a em aceite**; P7b pendente; sem release v1.0.0
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
- 259 testes, build 0 avisos/erros e smoke.

## Limitações conhecidas

- Reconexão do watcher durante a contagem sem evidência independente.
- Dispositivos HID sem XInput estão fora do watcher atual.
- HDR e notificações nativas adiados.
- A morte dentro de `SetDisplayConfig` não foi reproduzida; o risco foi aceito
  na P4 com limitação.
- Endpoint tardio/papéis distintos e migração de stream existente têm evidência
  apenas com fakes.
- Narrator/NVDA completo ainda não foi executado; UIAutomation foi validado.
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
