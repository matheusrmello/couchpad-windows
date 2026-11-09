# Handoff DeepSeek → DeepSeek (P1 → P2)

## Identidade

- Tag: `v0.2.0-shell` (a ser criada após o commit da P1)
- Data: 2026-11-09
- Fases concluídas: P0, P1
- Próxima fase: P2 — componentes reutilizáveis e estados

## Como rodar

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"
& $dotnet build CouchPad.slnx -c Debug
& $dotnet test CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --smoke
```

## O que está pronto

- Shell com cabeçalho, 7 cards, rodapé e diálogo Sobre (8 áreas).
- Layout responsivo em 3/2/1 colunas (D017), com `ScrollViewer` e mínimo
  `640×640`.
- i18n pt-BR/en com troca em tempo real e persistência; 78 chaves.
- Tema escuro com tokens de cor, tipografia, espaçamento e raio.
- Componentes visuais em `Themes/Controls.axaml`: card, pill, dot, chip,
  path box, botões, select e toggle.
- Ações da P1: alternar automação, refresh de displays (placeholder 700 ms),
  redefinir, escolher pasta do launcher, abrir Sobre e trocar idioma.
- Testes: 29 aprovados, incluindo paridade de catálogos e uso de i18n no XAML.

## O que é simulado

- Todo o hardware. Não há detecção de gamepad, CCD, Core Audio ou HDR.
- Valores de display/áudio são ilustrativos (`Monitor principal · 1920×1080`,
  `TV · HDMI`, `Saída padrão`).
- Persistência dos toggles/atrasos fica em memória até a P3; só o idioma é
  salvo em `%LOCALAPPDATA%\CouchPad\settings.json`.

## Contratos atuais

`MainWindowViewModel`:

- Estado: `AutomationEnabled`, `CloseLauncherOnActivate`, `StartWithWindows`,
  `MinimizeToTray`, `ActivationDelayIndex`, `DeactivationDelayIndex`,
  `DisplayModeIndex`, `AudioModeIndex`, `SteamEnabled`, `CustomLauncherPath`,
  `IsRefreshing`.
- Derivados: `StatusText`, `StatusIsWaiting`, `DisplayRefreshText`,
  `CustomLauncherPathText`, `LauncherSummary`, `ChooseFolderDialogTitle`,
  `VersionText`, `LanguageIndex`.
- Comandos: `RefreshDisplaysCommand`, `ResetCommand`.

Estilos e tokens:

- `Border.card`, `Border.pill` (+ `accent`/`waiting`/`danger`),
  `Ellipse.dot` (+ `active`/`waiting`/`error`), `Border.keyChip`,
  `Border.pathBox`, `Button.secondary`/`primary`/`link`, `ComboBox` e
  `ToggleSwitch`.
- Tokens em `Themes/Tokens.axaml`, incluindo `SurfaceSunken`, `WarningMuted`
  e `DangerMuted`.

## TODOs para P2

- [ ] Extrair `Card`, `Pill`, `StatusDot`, `KeyChip`, `PathPicker` e
      `ToggleSwitch` como controles/templates reutilizáveis (hoje são estilos
      e XAML repetido).
- [ ] Cobrir todos os estados obrigatórios: toggle on/off/disabled/focus;
      select normal/aberto/disabled/carregando; card normal/atenção/erro;
      ação idle/carregando/sucesso/erro.
- [ ] Adicionar pseudolocalização ou revisão visual com strings longas.
- [ ] Capturar foco de teclado e contraste real dos estados.
- [ ] Avaliar extrair a matriz de layout para um planejador puro testável.
- [ ] Manter os contratos do Core; não introduzir P/Invoke no App.

## Riscos e desconhecidos

- Valores estáticos de display podem parecer leitura real; reavaliar na P4.
- ToggleSwitch usa template próprio: arrasto do knob desabilitado e
  `OnContent`/`OffContent` não renderizados.
- Botões desabilitados saem da ordem de `Tab`; o motivo aparece em tooltip.
- Se um catálogo faltar no build, a UI mostra a chave crua; não há tela de
  erro de i18n.
- A faixa de 1 coluna vai de 640 a 719 px; validar se é confortável.

## Perguntas abertas

1. A P2 deve manter os cards em XAML direto ou criar `UserControl` por card?
2. O toggle deve virar `ToggleButton` estilizado para eliminar as partes
   internas obrigatórias do `ToggleSwitch`?
3. Quais estados de erro/carregamento entram na P2 antes do serviço simulado
   da P3?

## Evidências

- `docs/ui-spec.md` — breakpoints, áreas, tokens, estados e a11y.
- `HANDOFF.md` seção 20 — evidência da P1.
- `docs/decisions.md` — D017, D018, D019.
- Capturas de revisão em `C:\Users\Usuario\AppData\Local\Temp\opencode\`
  (`p1-3col.png`, `p1-2col.png`, `p1-1col.png`, `p1-en.png`,
  `p1-en-waiting.png`, `p1-screen.png`).
