---
name: couchpad-release
description: Use ao preparar build, instalador, assinatura, smoke tests, notas de release e rollback do CouchPad Windows (P7b).
---

# CouchPad Release

## Quando usar

- P7b: correções finais, instalador, assinatura, release.
- Antes de criar a tag `v1.0.0`.
- Ao preparar uma release de teste para o usuário.

## Quando NÃO usar

- Durante P4–P6, antes do aceite de hardware.
- Para mudanças de UI (use `couchpad-ui`).

## Versionamento

- SemVer: `MAJOR.MINOR.PATCH`.
- `v1.0.0` só após P7a aceita e smoke tests completos.
- Tags: `v0.3.0-sim`, `v0.6.0-hardware`, `v0.9.0-rc`, `v1.0.0`.
- Changelog em `CHANGELOG.md`.

## Build limpo

```powershell
$env:DOTNET = 'C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe'
& $env:DOTNET clean
& $env:DOTNET restore --locked-mode
& $env:DOTNET build -c Release
& $env:DOTNET test -c Release
```

- Fixar versões de pacotes (`packages.lock.json`).
- Sem avisos novos.
- Build reproduzível.

## Instalador

Decisão a registrar em `docs/decisions.md`:

| Opção | Prós | Contras |
|---|---|---|
| Inno Setup (per-user) | simples, sem admin, start menu | assinatura/updater manual |
| MSIX | assinatura/updater nativos | empacotamento mais complexo |
| WiX/MSI | padrão corporativo | mais cerimônia |

Requisitos:

- Instalar em `%LOCALAPPDATA%\Programs\CouchPad` (per-user) por padrão.
- Criar atalho no Start Menu.
- Registrar a tarefa `CouchPadRescue` no logon (Interactive/Limited).
- Não exigir admin sem necessidade.
- Desinstalação remove tarefa, atalhos e deixa config/journal preservados
  (ou pergunta).
- Instalador idempotente e com upgrade.

## Assinatura

- Authenticode é decisão separada; registrar.
- Sem certificado, documentar o aviso do SmartScreen.
- Nunca incluir chave privada no repositório.
- Verificar assinatura do `.exe` e do instalador.

## Smoke tests

Em VM limpa ou máquina de teste:

1. Instalar.
2. Primeiro uso com automação desligada.
3. Configurar TV, áudio e gamepad.
4. Conectar/desconectar gamepad.
5. Verificar tela, áudio e HDR.
6. Reiniciar e testar rescue.
7. Desinstalar e verificar limpeza.

Checklist completo em `references/release-checklist.md`.

## Notas de release

- O que mudou.
- O que é suportado (Windows 10 1903+/11, x64).
- Limitações conhecidas.
- Como reportar problema.
- Como reverter.

## Rollback

- Manter o instalador da versão anterior.
- Backup de config/journal antes de atualizar.
- Se a release falhar: desinstalar, reinstalar versão anterior, restaurar estado.
- Documentar o procedimento.

## Definição de pronto

1. Build/teste limpos.
2. Instalador testado em máquina limpa.
3. Tarefa de rescue criada e testada.
4. Smoke tests completos.
5. Notas de release publicadas.
6. Rollback documentado.
7. Tag `v1.0.0`.
8. Ledger atualizado.

## Erros comuns

- Release sem teste em máquina limpa.
- Instalador que exige admin sem motivo.
- Esquecer a tarefa de rescue.
- Não preservar config no upgrade.
- Publicar sem rollback.
- Assinar com chave de teste.

## Referências

- `references/release-checklist.md` — checklist detalhado.
- `couchpad-hardware` — matriz de teste.
- `docs/decisions.md` — decisão de instalador/assinatura.
