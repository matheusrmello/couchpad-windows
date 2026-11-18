---
name: couchpad-ui
description: Use ao criar ou revisar telas, componentes, tema, i18n ou acessibilidade do CouchPad Windows em Avalonia. Cobre estrutura MVVM, tokens, catálogo de componentes, estados, screenshots e critérios de aceite.
---

# CouchPad UI (Avalonia)

> Decisões de design, UX e revisão: use `couchpad-uiux`. Esta skill cobre
> a mecânica de implementação em Avalonia.

## Quando usar

- P0 (bootstrap visual), P1 (shell), P2 (componentes), P7a (docs/a11y/screenshots).
- Adicionar ou alterar uma tela, card, controle, tema, string i18n ou estado visual.
- Revisar a UI antes de um handoff.

## Quando NÃO usar

- P4–P6 (display, áudio, HDR, rescue): use `couchpad-hardware`.
- Qualquer código P/Invoke, COM, `SetDisplayConfig`, Core Audio ou Task Scheduler.
- Análise do binário ShiftPad: use `couchpad-rea`.

## Invariantes

- **Avalonia 12.1.4** e **.NET 10** (`net10.0`).
- SDK correto: `C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe`.
- **pt-BR + inglês**; nenhuma string hardcoded.
- App **100% free**: sem licença, trial, ativação ou telemetria.
- UI **inspirada** no ShiftPad 2.2.0; **não copiar** ícone, textos, imagens, XAML ou código.
- Não escrever em `src/CouchPad.Platform.Windows/**`.

## Mapa do projeto

```text
src/CouchPad.App/
  App.axaml
  Themes/Tokens.axaml
  Themes/Components.axaml
  Themes/Controls.axaml
  Localization/Strings.pt-BR.json
  Localization/Strings.en.json
  Localization/LocalizationService.cs
  Localization/TrExtension.cs
  Views/
  ViewModels/
  Controls/
  Performance/
src/CouchPad.Core/
  Automation/
  Audio/
  Display/
  Devices/
  Performance/
  Settings/
  Storage/
tests/CouchPad.Core.Tests/
```

## MVVM

- `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`).
- ViewModel não conhece controle visual; View não contém lógica de negócio.
- Serviços injetados por interface; modo simulado selecionável.
- Toda operação assíncrona recebe `CancellationToken`.
- Estados explícitos: `Idle`, `Loading`, `Success`, `Error`, `Disabled`.
- Nenhuma chamada de hardware no construtor.

## Tema

- Tema escuro como padrão; tokens em `Themes/Tokens.axaml`.
- Use `DynamicResource` para cores, raios e tipografia.
- Não use cores literais nas Views.
- Detalhes em `references/tokens.md`.

## Layout

- Janela padrão 1020×780; mínimo 640×640.
- `>= 1000 px`: 3 colunas.
- `720–999 px`: 2 colunas.
- `< 720 px`: 1 coluna, na ordem de leitura.
- Cabeçalho com logo, regra de acento e pill de estado.
- Rodapé com versão, “Redefinir”, “Sobre” e idioma.
- Wireframe completo em `docs/ui-spec.md`.

## Componentes

Controles próprios: `Card`, `ActionButton`, `StatusPill`, `StatusRow`,
`KeyChip`, `PathPicker`. Estilos utilitários: `Border.chip`,
`TextBlock.chipLabel`, `TextBlock.value.mono`.

Antes de criar um controle novo, verifique se um dos existentes resolve.
Detalhes de anatomia, propriedades e estados em `references/components.md`.

## Estados

Todo componente interativo deve cobrir:

- normal, hover, pressed, focus-visible, disabled;
- carregando e erro quando aplicável;
- texto e contraste em ambos os idiomas.

Status do serviço: `Ativo`, `Aguardando gamepad`, `Aplicando`, `Erro`,
`Recuperação pendente`. Estados da automação: `Idle`, `WaitingForGamepad`,
`Pending`, `Applying`, `Active`, `Error`, `RescuePending`.

## i18n

- Chaves planas com ponto: `automation.title`, `errors.display.apply_failed`.
- Adicione a string nos **dois** dicionários antes de usar.
- Use `TrExtension` ou o indexador do `LocalizationService`.
- Troca de idioma em tempo real, persistida nas configurações.
- Fallback: inglês.
- Teste de pseudo-localização para detectar corte.
- Nunca concatenar frases; use placeholders `{0}`, `{1}`.

## Acessibilidade

- Contraste ≥ 4.5:1 (texto) e ≥ 3:1 (componentes).
- Navegação completa por teclado; foco visível.
- `AutomationProperties.Name` e `HelpText` em toggles, selects e ícones.
- Status importante com `LiveSetting`.
- DPI 100–200% sem corte; alvos ≥ 32 px.
- Checklist em `references/a11y.md`.

## Screenshots

- 3 breakpoints × 2 idiomas, a partir do executável instalado.
- Nome atual: `docs/screenshots/p7a-{3col,2col,1col}-{pt,en}.png`.
- Sem dados pessoais, IP, serial ou caminho de usuário.
- Atualize o índice em `docs/screenshots/README.md` após gerar.

## Definição de pronto

1. `dotnet build` e `dotnet test` passam.
2. Nenhuma string fora dos dicionários.
3. Todos os estados visuais implementados.
4. Layout ok nos 3 breakpoints e nos 2 idiomas.
5. Checklist de a11y aplicado.
6. Screenshots gerados.
7. Ledger e decisões atualizados.

## Erros comuns

- Copiar cor/tamanho da screenshot em vez de usar tokens.
- Criar controle novo sem necessidade.
- Esquecer o estado `disabled` ou `error`.
- Texto hardcoded em XAML.
- Lógica de display/áudio no ViewModel da UI.
- Tela que “funciona” só no idioma padrão.

## Referências

- `references/tokens.md` — cores, tipografia, espaçamento, raios.
- `references/components.md` — catálogo e estados.
- `references/a11y.md` — acessibilidade e testes.
- `docs/ui-spec.md` — spec visual completa.
- `docs/ui-a11y-checklist.md` — checklist de aceite.
