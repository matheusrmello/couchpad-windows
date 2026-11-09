# CouchPad — Catálogo de componentes (P2)

- Data: 2026-11-09
- Fase: P2 — componentes reutilizáveis e estados
- Plataforma: Avalonia 12.1.4 / .NET 10 (`net10.0`)
- Escopo: UI estática e simulação visual; nenhum hardware é acessado

> Este catálogo descreve os controles próprios do CouchPad e os estados que
> cada um cobre. A aparência é original; nada foi copiado do ShiftPad.

---

## 1. Onde ficam

| Item | Caminho |
|---|---|
| Controles | `src/CouchPad.App/Controls/` |
| `ControlTheme` dos controles | `src/CouchPad.App/Themes/Components.axaml` |
| Estilos de botão, select e toggle | `src/CouchPad.App/Themes/Controls.axaml` |
| Tokens de cor/tipografia/espaço | `src/CouchPad.App/Themes/Tokens.axaml` |
| Galeria de estados | `src/CouchPad.App/Views/ComponentGalleryWindow.axaml` |
| Shell que usa os controles | `src/CouchPad.App/Views/MainWindow.axaml` |

Os temas implícitos são registrados em `App.axaml`:

```xml
<ResourceInclude Source="avares://CouchPad/Themes/Tokens.axaml" />
<ResourceInclude Source="avares://CouchPad/Themes/Components.axaml" />
```

---

## 2. Catálogo

| Controle | Base | Propriedades | Estados | Observações |
|---|---|---|---|---|
| `Card` | `ContentControl` | `Title`, `HeaderAction`, `Tone` | normal, atenção, erro, disabled | Título com `TextWrapping`; ação opcional no cabeçalho |
| `StatusPill` | `TemplatedControl` | `Text`, `Tone`, `ShowDot` | neutro, acento, aviso, erro | Texto é live region (`Polite`); dot decorativo |
| `StatusRow` | `TemplatedControl` | `Label`, `Value`, `Tone`, `ShowDot` | neutro, acento, aviso, erro | Valor sempre textual; label secundário |
| `KeyChip` | `TemplatedControl` | `Text` | preenchido | Fonte mono, 30×30 |
| `PathPicker` | `TemplatedControl` | `PathText`, `ChooseText`, `ChooseCommand` | normal, disabled | Caminho truncado com `…`; botão usa `Command` |
| `ActionButton` | `Button` | `Variant`, `State`, `Content`, `Command` | idle, hover, pressed, focus, disabled, loading, success, error | Ícone + texto; cor nunca é o único indicador |
| `ToggleSwitch` | `ToggleSwitch` | `IsChecked` | on, off, hover, pressed, focus, disabled | Template próprio; rótulo fica fora |
| `Select` | `ComboBox` | `SelectedIndex` | normal, hover, focus, disabled, loading, error | `loading`/`error` são classes |
| `Button` | `Button` | `Content`, `Command` | secondary, primary, link + loading/error/success | Usado no Sobre e nos links do rodapé |

### 2.1 `Card`

```xml
<controls:Card Title="{loc:Tr section.display}" Tone="Normal">
  <controls:Card.HeaderAction>
    <controls:ActionButton Content="{loc:Tr display.refresh}" ... />
  </controls:Card.HeaderAction>
  <!-- conteúdo -->
</controls:Card>
```

- `Tone=Attention` usa `Warning` na borda; `Tone=Error` usa `Danger`.
- `HeaderAction` é um `object`; qualquer controle serve.
- `IsEnabled=false` reduz a opacidade do card.

### 2.2 `StatusPill` e `StatusRow`

```xml
<controls:StatusPill Text="{Binding StatusText}"
                     Tone="{Binding CurrentStatusTone}" />

<controls:StatusRow Label="{loc:Tr automation.gamepad}"
                    Value="{loc:Tr gamepad.none}" />
```

- Tons: `Neutral`, `Accent`, `Warning`, `Danger`.
- `ShowDot=false` remove o ponto sem remover o texto.
- `StatusPill` aplica `AutomationProperties.LiveSetting=Polite` no texto do
  template, então mudanças de estado são anunciadas.

### 2.3 `KeyChip`

```xml
<controls:KeyChip Text="{loc:Tr combo.key.lb}" />
```

- O texto vem sempre do catálogo (inclusive `1`, `LB`, `RB`, `A`, `Esc`).

### 2.4 `PathPicker`

```xml
<controls:PathPicker PathText="{Binding CustomLauncherPathText}"
                     ChooseText="{loc:Tr launchers.choose}"
                     ChooseCommand="{Binding ChooseLauncherFolderCommand}" />
```

- O controle não abre diálogo; apenas expõe o comando.
- `MainWindow` assina `FolderPickerRequested` e usa
  `StorageProvider.OpenFolderPickerAsync`.
- Caminho vazio mostra `launchers.no_path`; caminho longo é truncado.
- Em `IsEnabled=false`, o caminho e o botão ficam esmaecidos.

### 2.5 `ActionButton`

```xml
<controls:ActionButton Variant="Primary"
                       State="Loading"
                       Content="{loc:Tr display.refreshing}"
                       Command="{Binding RefreshDisplaysCommand}" />
```

| Estado | Visual | Comportamento |
|---|---|---|
| `Idle` | borda neutra (secondary) ou acento (primary) | normal |
| hover | fundo mais claro; primary usa `AccentMutedHover` | interativo |
| pressed | fundo mais escuro; primary usa `AccentMutedPressed` | interativo |
| focus | borda `TextPrimary` de 2 px | visível dentro dos limites |
| disabled | opacidade 0.45 | sem interação |
| `Loading` | barra de progresso indeterminada + texto | não altera `IsEnabled` por si só |
| `Success` | check + borda/texto de acento | apresentação |
| `Error` | exclamação + borda/texto de perigo | apresentação |

Regras:

- `Variant=Primary` é o padrão do enum; o construtor já aplica as
  pseudo-classes para o valor default.
- O consumidor decide se desabilita durante a operação. No shell, o botão de
  atualizar displays usa `State="{Binding DisplayRefreshState}"` e
  `IsEnabled="{Binding !IsRefreshing}"`; o tema mantém opacidade 1 quando
  `loading` e `disabled` coincidem.
- O foco usa `TextPrimary` (não o acento) para não se confundir com a borda
  do variant primário.

---

## 3. Matriz de estados

| Estado | Card | Pill/Row | ActionButton | Toggle | Select | Button/PathPicker |
|---|---|---|---|---|---|---|
| normal | sim | sim | sim | sim | sim | sim |
| hover | — | — | sim | sim | sim | sim |
| pressed | — | — | sim | sim | — | sim |
| focus | — | — | sim | sim | sim | sim (Fluent) |
| disabled | sim | — | sim | sim | sim | sim |
| loading | — | — | sim | — | sim (classe) | sim (classe) |
| success | — | — | sim | — | — | sim (classe) |
| error | sim (borda) | sim | sim | — | sim (classe) | sim (classe) |

- Hover, pressed e focus são interativos; a galeria permite experimentá-los.
- `Button.loading` e `ComboBox.loading` definem `IsEnabled=false` no estilo,
  bloqueando ponteiro e teclado. `ActionButton` mantém a decisão no consumidor.
- `StatusPill`/`StatusRow` não usam apenas cor: sempre há texto.

---

## 4. Galeria de estados

Executar:

```powershell
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --gallery
```

- Janela `1100 × 720` (mínimo `720 × 560`), rolável.
- Seções: Cards, Pills de status, Botões de ação, Seletores, Toggles e chips,
  Seletor de pasta.
- Todos os textos vêm de `gallery.*` nos dois catálogos.
- A galeria não chama display, áudio, HDR, gamepad nem o seletor de pastas.
- Capturas de revisão (fora do repositório):
  `C:\Users\Usuario\AppData\Local\Temp\opencode\p2-gallery-pt.png`,
  `p2-gallery-en.png`, `p2-main-pt.png`, `p2-main-en.png`,
  `p2-main-pt-2col.png`, `p2-main-pt-640.png`, `p2-refresh-loading.png`.
- As capturas oficiais entram em `docs/screenshots/` na P7a.

---

## 5. Tokens usados

| Token | Uso |
|---|---|
| `Surface`, `SurfaceRaised`, `SurfaceSunken` | fundos de card, botão e campo |
| `Border`, `BorderStrong` | bordas normais e de hover/pressed |
| `TextPrimary`, `TextSecondary` | texto e legendas |
| `Accent`, `AccentMuted` | ação primária e estado ativo |
| `AccentMutedHover` (`#1B5A3A`) | hover do botão primário |
| `AccentMutedPressed` (`#0C2E1C`) | pressed do botão primário |
| `Warning`, `WarningMuted` | atenção/aguardando |
| `Danger`, `DangerMuted` | erro |
| `FontSize.Caption/Body/CardTitle` | rótulos, valores e títulos |
| `Radius.Control/CardLarge/Pill` | formas |
| `ControlHeight`, `ControlHeight.Small` | alturas |

O anel de foco do toggle usa `Accent`; o do `ActionButton` usa `TextPrimary`
com 2 px. O contraste do foco é garantido pela espessura e pelo texto ao lado.

---

## 6. Acessibilidade

- `StatusPill` anuncia mudanças com `LiveSetting=Polite`.
- Todo dot de status tem texto ao lado.
- Toggles, selects e ActionButtons têm `AutomationProperties.Name` ou conteúdo
  textual.
- A ordem de tabulação segue a ordem visual; a galeria é navegável por `Tab`.
- `PathPicker` mantém o botão focável; o comando é a única ação.
- Botões desabilitados continuam legíveis, com opacidade reduzida.

---

## 7. Limites e próximos passos

- A galeria é uma ferramenta de desenvolvimento; não faz parte do fluxo
  principal. A P3 usa os mesmos componentes no shell com a state machine
  simulada.
- O exemplo de `PathPicker` na galeria é visual e não tem comando.
- Hover/pressed/focus não podem ser fixados em uma captura; use a galeria.
- Persistência de estado, atrasos e fluxo de ativação foram implementados na P3
  com serviços simulados.
- Nenhum P/Invoke, COM ou chamada de hardware foi introduzido nas P2/P3.
