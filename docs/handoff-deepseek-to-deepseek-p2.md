# Handoff — P2 (DeepSeek) → P3 (DeepSeek)

- Data: 2026-11-09
- Origem: P2 — componentes reutilizáveis e estados
- Destino: P3 — serviço simulado e fluxo de ativação
- Tag da origem: `v0.3.0-components`
- Build: 0 avisos, 0 erros
- Testes: 34 aprovados

---

## 1. O que foi entregue

### Controles

`src/CouchPad.App/Controls/`:

- `Card` — `ContentControl` com `Title`, `HeaderAction` e `Tone`
  (`Normal`, `Attention`, `Error`).
- `StatusPill` — `TemplatedControl` com `Text`, `Tone`, `ShowDot`; live region
  no texto.
- `StatusRow` — `TemplatedControl` com `Label`, `Value`, `Tone`, `ShowDot`.
- `KeyChip` — `TemplatedControl` com `Text`.
- `PathPicker` — `TemplatedControl` com `PathText`, `ChooseText`,
  `ChooseCommand`.
- `ActionButton` — `Button` com `Variant` (`Primary`, `Secondary`) e `State`
  (`Idle`, `Loading`, `Success`, `Error`).

### Temas

- `Themes/Components.axaml`: `ControlTheme` implícito para os seis controles,
  mesclado em `App.axaml`.
- `Themes/Controls.axaml`: estados de `Button`/`ComboBox` (`loading`, `error`,
  `success`), `ProgressBar.inline`, toggle com pressed/focus e select.
- `Themes/Tokens.axaml`: `AccentMutedHover` (`#1B5A3A`) e
  `AccentMutedPressed` (`#0C2E1C`).

### Shell

- `MainWindow.axaml` usa `controls:Card` nos 7 cards e os demais controles no
  cabeçalho, linhas de status, chips, rodapé e seletor de pasta.
- `MainWindowViewModel` expõe:
  - `CurrentStatusTone` (`Neutral`/`Warning`);
  - `DisplayRefreshState` (`Idle`/`Loading`);
  - `ChooseLauncherFolderCommand`;
  - evento `FolderPickerRequested`.
- `MainWindow.axaml.cs` assina/desassina o evento em `OnDataContextChanged` e
  `Closed`, e trata cancelamento/indisponibilidade do seletor.

### Galeria

- `Views/ComponentGalleryWindow.axaml(.cs)`, aberta por `--gallery`.
- Cobre cards, pills, rows, ActionButton, selects, toggles, chips e
  PathPicker em todos os estados, nos dois idiomas.
- Não chama hardware nem abre o seletor de pastas.

### i18n e testes

- `gallery.*` nos dois catálogos; 116 chaves em paridade.
- `footer.phase` atualizado para P2.
- `UiLocalizationTests` verifica:
  - paridade e uso das chaves;
  - ausência de literais visíveis;
  - shell composto pelos controles;
  - `ControlTheme` de cada componente;
  - estados presentes na galeria;
  - ausência de literais em `Components.axaml`.

### Documentação

- `docs/ui-components.md` — catálogo, matriz de estados, tokens, a11y e
  limites.
- `docs/ui-spec.md` — §5 e §9 atualizados para P1–P2.
- `docs/decisions.md` — D020–D023.
- `HANDOFF.md` — ledger e seção 21.

---

## 2. Contratos que a P3 deve preservar

1. **Não reintroduzir** `Border.card`, `Border.pill`, `Border.keyChip` ou
   `Border.pathBox` no shell; use os controles de `Controls/`.
2. **ActionButton**: `State` é apresentação; quem executa a operação decide
   `IsEnabled`. Para operações assíncronas:
   ```xml
   <controls:ActionButton State="{Binding OperationState}"
                          IsEnabled="{Binding !IsBusy}"
                          Content="{Binding ActionText}"
                          Command="{Binding RunCommand}" />
   ```
   O tema mantém opacidade 1 quando `loading` e `disabled` coincidem.
3. **Status**: use `StatusPill`/`StatusRow` com `Tone`. Nunca use cor como
   único indicador; texto sempre acompanha.
4. **PathPicker**: o comando deve vir do ViewModel; diálogos continuam na
   view. Não adicione `StorageProvider` ao Core nem ao ViewModel.
5. **i18n**: toda string visível nova entra nos dois catálogos; adicione a
   chave usada no XAML para o teste de paridade. Não use literais em `Text`,
   `Content`, `Title`, `AutomationProperties.*` ou `ToolTip.Tip`.
6. **Fronteira**: DeepSeek não escreve em `src/CouchPad.Platform.Windows/**`.
   Nenhum P/Invoke/COM no App.
7. **Fases**: não avance para P4 sem o aceite do usuário; a P3 é simulação
   pura, sem hardware.

---

## 3. Próximos passos sugeridos para a P3

1. Modelar o serviço simulado de gamepad, display e áudio no Core
   (`IGamepadWatcher`, `IDisplayService`, `IAudioService` ou equivalentes).
2. Implementar fakes com estados conectado/desconectado e atrasos de
   ativação/desativação.
3. Criar a state machine (`Idle`, `Waiting`, `Applying`, `Active`, `Error`,
   `RescuePending`) no Core, com timers injetáveis e cancelamento.
4. Ligar o shell à simulação:
   - toggle de automação e gamepad simulado;
   - `StatusText`/`CurrentStatusTone` refletindo a state machine;
   - `DisplayRefreshState` e `ActionButton` para operações da simulação;
   - persistência dos toggles/atrasos/modos (settings atômicos já existem).
5. Substituir o placeholder de 700 ms por chamadas ao serviço simulado.
6. Adicionar testes puros para transições, cancelamento, timeouts e reset.
7. Atualizar `docs/decisions.md`, `HANDOFF.md`, `docs/ui-spec.md` e criar a
   seção/handoff da P3.
8. Subir `Version` para `0.4.0` e taguear `v0.4.0-sim`.

---

## 4. Verificação rápida

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"

& $dotnet build CouchPad.slnx -c Debug
& $dotnet test CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --smoke
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --gallery
```

- O smoke imprime uma linha e fecha.
- A galeria abre em `1100 × 720` e pode ser fechada por `Esc`/`Fechar`.
- Capturas de revisão em `C:\Users\Usuario\AppData\Local\Temp\opencode\`:
  `p2-gallery-pt.png`, `p2-gallery-en.png`, `p2-main-pt.png`,
  `p2-main-en.png`, `p2-main-pt-2col.png`, `p2-main-pt-640.png`,
  `p2-refresh-loading.png`.

---

## 5. Riscos e limites conhecidos

- Hover, pressed e focus são interativos; não aparecem fixos em uma captura.
  Use a galeria e o teclado.
- A galeria é ferramenta de desenvolvimento; o exemplo de `PathPicker` não
  tem comando.
- A fronteira de 1 coluna começa abaixo de 720 px de largura de cliente; com
  moldura, uma janela externa de 720 px pode render 1 coluna.
- A P3 ainda não persiste o estado da automação; isso deve entrar junto com a
  state machine.
- Nenhuma licença, ativação, assinatura ou telemetria foi adicionada.
