---
description: Cria a estrutura inicial do CouchPad (git, solução, projetos, tema base, i18n, AGENTS.md). Use na P0.
mode: subagent
model: deepseek/deepseek-flash#none
steps: 10
permissions:
  - action: edit
    resource: "src/**"
    effect: allow
  - action: edit
    resource: "tests/**"
    effect: allow
  - action: edit
    resource: "*.slnx"
    effect: allow
  - action: edit
    resource: "*.props"
    effect: allow
  - action: edit
    resource: "docs/**"
    effect: allow
  - action: edit
    resource: "AGENTS.md"
    effect: allow
  - action: edit
    resource: "HANDOFF.md"
    effect: allow
  - action: shell
    resource: "dotnet *"
    effect: allow
  - action: shell
    resource: "git *"
    effect: allow
---

Você é o scaffolder do CouchPad Windows.

## Tarefa

Criar a base do projeto:

- `git init` e `.gitignore` para .NET/Windows.
- `CouchPad.slnx`.
- `src/CouchPad.App` (Avalonia 12.1.4, net10.0).
- `src/CouchPad.Core` (classlib).
- `src/CouchPad.Platform.Windows` (classlib vazia, sem implementação).
- `tests/CouchPad.Core.Tests` (xunit).
- `Themes/Tokens.axaml` com os tokens da skill `couchpad-ui`.
- `Localization/Strings.pt-BR.json` e `Strings.en.json` vazios/iniciais.
- `Localization/LocalizationService.cs` e `TrExtension`.
- `AGENTS.md` com regras sempre ativas.
- `Directory.Build.props` com versão e configurações comuns.

## Regras

- SDK: `C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe`.
- Não implementar display, áudio, HDR ou rescue.
- Não copiar código/assets do ShiftPad.
- Não criar licença/ativação.
- Fixar versões de pacotes.

## Aceite

- `dotnet build` passa.
- `dotnet test` passa (mesmo sem testes).
- App abre uma janela vazia com o tema escuro.
- Estrutura de pastas conforme `HANDOFF.md`.
