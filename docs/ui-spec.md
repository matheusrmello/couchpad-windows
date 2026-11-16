# CouchPad — Especificação de UI (P1–P7a)

- Data: 2026-11-16
- Fases: P1–P7 implementadas; UI atual em 1.0.13 com perfis de ambiente
- Base visual: referência do ShiftPad 2.2.0, recriada com identidade própria
- Plataforma: Avalonia 12.1.4 / .NET 10 (`net10.0`)

> Esta spec descreve o shell, a automação, o display/áudio reais e a superfície
> P6. Ela não copia ícones, textos, imagens, XAML ou código do ShiftPad. Todos
> os textos visíveis vêm de `Strings.pt-BR.json` e `Strings.en.json`.

---

## 1. Princípios

1. Tema escuro com cards arredondados, acento verde e superfícies neutras.
2. Nenhuma string visível fora dos dois catálogos de idioma.
3. Estados nunca dependem só de cor: sempre há texto ou forma ao lado.
4. Layout responsivo em três breakpoints, com rolagem vertical quando o
   conteúdo não couber.
5. Nenhuma chamada real de tela, áudio, HDR ou gamepad nas P1–P3; a P3 usa
   serviços simulados no Core. Na P4, a leitura/seleção é real, mas a escrita
   depende de opt-in explícito e do watchdog.
6. Toda ação destrutiva ou incerta fica desabilitada até a fase que a
   implementa.

---

## 2. Layout responsivo

| Largura da janela | Colunas | Organização |
|---|---|---|
| `>= 1000 px` | 3 | Automação + Display; Gamepad + Combinação + Parâmetros; Transição + Launchers |
| `720–999 px` | 2 | Automação + Display; Gamepad + Combinação; Transição + Launchers; Parâmetros em linha inteira |
| `< 720 px` | 1 | Cards empilhados na ordem de leitura |

- Janela padrão: `1020 × 780`.
- Mínimo: `640 × 640`.
- O conteúdo fica em `ScrollViewer`; cabeçalho e rodapé permanecem fixos.
- O reposicionamento dos cards é feito em `MainWindow.axaml.cs`
  (`ApplyLayout`), mantendo o XAML declarativo.
- O espaçamento entre cards é `10 px`; o padding dos cards é `16 px`.

### 2.1 Mapa de cards

| Card | 3 colunas | 2 colunas | 1 coluna |
|---|---|---|---|
| Automação | (0,0) | (0,0) | linha 0 |
| Display | (0,1) span 2 | (0,1) | linha 1 |
| Gamepad | (1,0) | (1,0) | linha 2 |
| Combinação | (1,1) | (1,1) | linha 3 |
| Parâmetros | (1,2) span 2 linhas | (3,0) span 2 | linha 6 |
| Transição | (2,0) | (2,0) | linha 4 |
| Launchers | (2,1) | (2,1) | linha 5 |

Na configuração de 3 colunas, `Parâmetros` ocupa `(1,2)` com `RowSpan=2` e a
célula `(2,2)` fica vazia por desenho (o card alto equilibra a coluna direita).

---

## 3. Áreas do shell

### 3.1 Cabeçalho

- Marca textual `C` em bloco com `AccentMuted`.
- Nome (`app.name`) e tagline (`app.tagline`).
- Pill `Grátis` (`status.free`) em acento.
- Pill de estado com `StatusDot` + texto, dirigida pela state machine:
  - `Idle` → `status.idle` (neutro);
  - `WaitingForGamepad` → `status.waiting` (âmbar);
  - `Pending` → `status.pending` (âmbar, atraso em andamento);
  - `Applying` → `status.applying` (acento);
  - `Active` → `status.active` (acento);
  - `Error` → `status.error` (perigo).
- A pill de estado usa `AutomationProperties.LiveSetting=Polite` na P2/P3.

### 3.2 Card Automação

- Título `section.automation`.
- `ToggleSwitch` ligado/desligado (`automation.toggle`), com `HelpText`.
- Texto de ajuda (`automation.help`).
- Resumo em quatro linhas: gamepad, tela, áudio e launcher.
- Gamepad: `gamepad.none` ou `gamepad.simulated`, com tom
  neutro/acento.
- Tela: `DisplaySummary` (modo aplicado quando `Active`; senão o valor atual).
- Áudio: `AudioSummary` (`audio.active.hdmi` quando aplicado; senão
  `automation.default_audio`).
- Launcher: Steam/nenhum/caminho customizado.

### 3.3 Card Display

- Título `section.display`.
- Botão `display.refresh`; durante a atualização mostra
  `display.refreshing` e fica desabilitado por 700 ms (placeholder até a P4).
- Colunas `Atual` e `Destino`:
  - `Atual` usa `DisplaySummary` (modo aplicado quando `Active`);
  - `Destino` usa `display.target_value` (TV · HDMI, em acento).
- Seletores P4:
  - `display.tv_target` lista os targets CCD descobertos;
  - `display.internal_target` lista o monitor interno;
  - `display.targets_refresh` atualiza a descoberta;
  - `display.target_hint` lembra que é preciso reiniciar para aplicar;
  - erro de descoberta usa `display.targets_error`.
- A descoberta é read-only; nada muda até `AllowDisplayWrites` e os seletores
  serem salvos e o app reiniciado.

### 3.4 Card Gamepad

- Título `section.gamepad`.
- Linha `Controle principal` com `gamepad.none`/`gamepad.simulated` e tom
  neutro/acento.
- Dica `gamepad.simulated_help` (“Simulação local: nenhum controle real é
  usado.”).
- Botão `ActionButton` com `gamepad.connect`/`gamepad.disconnect`; alterna o
  `SimulatedGamepadWatcher` e dispara a state machine.
- O watcher real de gamepad entra na P4/P6.

### 3.5 Card Combinação

- Título `section.combo`.
- Ajuda `combo.help`.
- Quatro `KeyChip`: `1`, `LB`, `RB`, `A`, todos vindos do catálogo.
- Botão `combo.change` desabilitado até a captura real de botões.

### 3.6 Card Parâmetros

- Título `section.settings`.
- Toggles: fechar launcher ao ativar, iniciar com o Windows, minimizar para a
  bandeja e **permitir escrita de display** (`settings.allow_display_writes`,
  padrão desligado).
- `settings.allow_display_writes_help` avisa que é avançado, habilita CCD +
  watchdog e exige reinício.
- Selects: atraso ao ativar e ao desativar (Imediato, 1 s, 2 s, 3 s, 5 s).
- Ajuda `settings.help`.
- A P3 persiste todos esses valores em `%LOCALAPPDATA%\CouchPad\settings.json`.

### 3.7 Card Transição

- Título `section.transition`.
- Select de modo de tela: Somente TV, Estender, Espelhar.
- Select de modo de áudio: Mover para HDMI, Manter atual.
- Padrões: Somente TV e Mover para HDMI.

### 3.8 Card Launchers

- Título `section.launchers`.
- Linha Steam com `ToggleSwitch`.
- Launcher personalizado com `PathPicker`: caminho atual ou
  `launchers.no_path`, e botão `launchers.choose`.
- O seletor usa `StorageProvider.OpenFolderPickerAsync` (sem admin).
- O caminho escolhido aparece truncado com `…` quando não couber.

### 3.9 Rodapé

- `StatusDot` + texto de estado (mesmo estado do cabeçalho).
- Versão (`footer.version`, via `AssemblyInformationalVersion`).
- `Redefinir` (`footer.reset`) restaura os padrões, desliga a automação e
  salva a configuração.
- `Sobre` (`footer.about`) abre `AboutWindow` como diálogo modal.
- Seletor de idioma com `language.ptbr` e `language.en`.

### 3.10 Sobre

- Janela modal `440 × 340`, centralizada no dono, sem botão na barra de
  tarefas.
- Marca, tagline, aviso 100% gratuito, licença MIT, versão e fase.
- Botão `about.close` (`Esc`/`Enter` fecham).

### 3.11 Adições P6/P7a

- Card **Contagem regressiva**: atrasos de ativação/desativação; o texto da
  contagem aparece na status pill.
- Card **Parâmetros**: Iniciar com o Windows (tarefa por usuário), watcher
  XInput opt-in, minimizar para bandeja, permitir escrita de display.
- Card **Transição**: seleção de endpoint de áudio da TV, opt-in de áudio,
  modo de tela/áudio, retorno do modo de jogo.
- Card **Launchers**: Steam/Hydra Big Picture, abrir launcher, sair do modo de
  jogo ao restaurar, erro anunciado.
- **Bandeja**: abrir, ativar/desativar automação e sair; tooltip localizado.
- **Recuperação assistida**: botão `Tentar recuperar` quando o estado é
  incerto; erro e ajuda com live region.
- **Autostart**: toggle reflete a tarefa por usuário; erro reverte e anuncia.
- **Acessibilidade**: ordem de foco alinhada à ordem visual nos 3 breakpoints.
- **Perfis de ambiente (1.0.12+)**: seletor no card Display com nome editável,
  criação, exclusão e ajuda; cada perfil guarda TV, monitor interno e saída de
  áudio. O perfil ativo é o mesmo usado na composição de startup.
- **Diagnóstico da identificação (1.0.13)**: expander read-only com perfil,
  TV, monitor, áudio, EDID, HDMI, estado, motivo e confiança alta/média/baixa.

---

## 4. Tokens

### 4.1 Cores

| Token | Valor | Uso |
|---|---|---|
| `Background` | `#0A0A0B` | fundo da janela |
| `Surface` | `#17181A` | cards |
| `SurfaceRaised` | `#1F2023` | botões, selects |
| `SurfaceSunken` | `#111214` | chips e caixa de caminho |
| `Border` | `#2A2B2F` | borda padrão |
| `BorderStrong` | `#3A3B40` | borda de controle |
| `TextPrimary` | `#F2F3F5` | texto principal |
| `TextSecondary` | `#9AA0A6` | legendas |
| `TextDisabled` | `#5F6368` | desabilitado |
| `Accent` | `#2FD07A` | ação e estado ativo |
| `AccentMuted` | `#123D28` | fundo de destaque |
| `Info` | `#2F80ED` | reservado |
| `Warning` | `#F2C94C` | aguardando |
| `WarningMuted` | `#3A3117` | fundo de aviso |
| `Danger` | `#EB5757` | erro |
| `DangerMuted` | `#3A1D1E` | fundo de erro |

### 4.2 Tipografia

| Token | Valor |
|---|---|
| Família | Segoe UI Variable, Inter, Segoe UI |
| Família mono | Cascadia Mono, Consolas, Segoe UI |
| Display | 28 |
| H1 | 20 |
| CardTitle | 15 |
| Body | 13 |
| Caption | 11 |
| Mono | 12 |

### 4.3 Espaçamento e raios

- Padding da janela: `24`.
- Padding do card: `16`.
- Gap entre cards: `10`.
- Altura de controle: `36`; controle pequeno: `32`.
- Raio de controle: `8`; card: `12–14`; pill: `999`.

---

## 5. Componentes e estados

A partir da P2, os componentes são controles próprios com `ControlTheme` em
`Themes/Components.axaml`. O catálogo detalhado (propriedades, estados,
acessibilidade e galeria) está em `docs/ui-components.md`.

| Componente | Implementação P2 | Estados cobertos |
|---|---|---|
| `Card` | `controls:Card` | normal, atenção, erro, disabled |
| `StatusPill` | `controls:StatusPill` | neutro, acento, aviso, erro; dot opcional |
| `StatusRow` | `controls:StatusRow` | neutro, acento, aviso, erro |
| `KeyChip` | `controls:KeyChip` | preenchido |
| `PathPicker` | `controls:PathPicker` | vazio, preenchido, disabled |
| `ActionButton` | `controls:ActionButton` | idle, hover, pressed, focus, disabled, loading, success, error |
| `ToggleSwitch` | template próprio | on, off, hover, pressed, focus, disabled |
| `Select` | `ComboBox` estilizado | normal, hover, focus, disabled, loading, error |
| `Button` | `secondary`, `primary`, `link` | normal, hover, pressed, focus, disabled, loading, success, error |

O shell usa `Card`, `StatusPill`, `StatusRow`, `KeyChip`, `PathPicker` e
`ActionButton`. O botão de atualizar displays exercita o estado `Loading`
enquanto o placeholder de 700 ms está ativo.

O template do toggle desenha o visual próprio; o arrasto do knob fica
desabilitado nesta fase e `OnContent`/`OffContent` não são renderizados.
Os rótulos ficam sempre fora do controle, com `AutomationProperties.Name`.

Regras:

- Toggle sempre com `AutomationProperties.Name`; quando necessário,
  `HelpText`.
- Ícone ou controle sem texto visível sempre com nome de acessibilidade.
- Botão desabilitado sempre com opacidade reduzida; `Alterar` continua
  desabilitado até a captura real de botões.

---

## 6. i18n

- Catálogos: `src/CouchPad.App/Localization/Strings.pt-BR.json` e
  `Strings.en.json`.
- Uso em XAML: `{loc:Tr chave}`; reage à troca de idioma via `Revision`.
- Valores calculados no ViewModel usam `LocalizationService` e notificam
  `PropertyChanged` na troca de idioma.
- Fallback: inglês; depois a própria chave.
- Paridade de chaves e valores não vazios cobertos por teste.
- Nenhum `Text`, `Content`, `Title`, `Watermark` ou nome de acessibilidade
  literal: coberto por `UiLocalizationTests`.

Grupos de chaves: `app.*`, `status.*`, `section.*`, `automation.*`,
`display.*`, `gamepad.*`, `combo.*`, `settings.*`, `delay.*`,
`transition.*`, `display_mode.*`, `audio_mode.*`, `audio.*`, `launchers.*`,
`footer.*`, `language.*`, `about.*`, `gallery.*`.

---

## 7. Acessibilidade

- Contraste: texto normal `TextPrimary/Background` e
  `TextSecondary/Surface` acima de 4.5:1; acento sobre `AccentMuted` acima de
  3:1.
- Alvos de toque: controles com pelo menos 28–32 px de altura.
- Teclado: `Tab` percorre todos os controles; `Enter`/`Espaço` ativam;
  `Esc` fecha o Sobre.
- Foco visível: anel de acento no toggle; borda `TextPrimary` de 2 px nos
  `ActionButton`; foco padrão do Fluent nos demais.
- Status anunciado com `LiveSetting=Polite`.
- Recuperação incerta mostra `recovery.help` no card Automação, em perigo, e
  desabilita o toggle até reiniciar.
- Nunca usar só cor: todo dot tem texto ao lado.
- Textos longos usam `TextWrapping`/`TextTrimming`.

---

## 8. Comportamento da P3

| Ação | Efeito |
|---|---|
| Alternar automação | State machine liga/desliga; sem gamepad vai para `Aguardando gamepad` |
| Conectar controle simulado | Com automação ligada inicia o atraso de ativação; depois aplica display/áudio |
| Desconectar controle | Cancela ativação pendente ou agenda a desativação; restaura o baseline |
| Atraso ao ativar/desativar | Estado `Aguardando atraso`; nova transição cancela o atraso |
| Trocar modo com `Active` | Reaplica display/áudio preservando o baseline original |
| Atualizar displays | Texto vira `Atualizando…`; `ActionButton` em `Loading` por 700 ms; nenhum CCD |
| Redefinir | Desliga a automação, restaura o baseline e volta aos padrões salvos |
| Escolher pasta | Seletor nativo de pasta; grava o caminho em memória e no settings |
| Sobre | Abre diálogo modal com versão e licença |
| Idioma | Troca todos os textos em tempo real e persiste a preferência |

Persistência: toggles, atrasos, modos, Steam, caminho do launcher, idioma e
automação vão para `%LOCALAPPDATA%\CouchPad\settings.json`. O journal de
sessão fica em `state\session.json`.

Fora do escopo da P3: display/áudio/HDR reais, watcher real de gamepad,
captura de combinação, bandeja, autostart e rescue no logon.

---

## 9. Verificação

- Build: 0 avisos, 0 erros.
- Testes: 294 aprovados — catálogos/i18n, composição do shell, temas,
  estados da galeria, state machine, atrasos/cancelamento, journal, settings,
  composição Windows, guard, áudio, launcher e P6.
- Smoke: janela configurada `1020 × 780` (limitada pela área de trabalho em
  telas 720p), troca pt/en, título `CouchPad`.
- Galeria: `CouchPad.dll --gallery` abre em `1100 × 720`; todos os estados
  visíveis e navegáveis por teclado.
- Capturas oficiais atualizadas (16/11/2026, versão 1.0.13):
  `docs/screenshots/p7a-3col-pt.png`, `p7a-2col-pt.png`, `p7a-1col-pt.png`,
  `p7a-3col-en.png`, `p7a-2col-en.png`, `p7a-1col-en.png`.
- Checklist de acessibilidade: `docs/ui-a11y-checklist.md`; UIAutomation
  confirmou ordem/nomes nos 3 breakpoints; Narrator/NVDA manual pendente.

### Checklist de aceite P1

- [x] 8 áreas visíveis: cabeçalho, 7 cards e rodapé.
- [x] Layout em 3, 2 e 1 coluna, com rolagem quando necessário.
- [x] Nenhuma string visível fora dos catálogos.
- [x] Toggle, select, botão, pill e dot com estados visuais.
- [x] Nome de acessibilidade nos controles sem texto.
- [x] Diálogo Sobre funcional e fechável por `Esc`.

### Checklist de aceite P2

- [x] `Card`, `StatusPill`, `StatusRow`, `KeyChip`, `PathPicker` e
  `ActionButton` com `ControlTheme` próprio.
- [x] Shell reutiliza os componentes, sem `Border.card`/`pill`/`keyChip`/
  `pathBox` locais.
- [x] Estados normal, hover, pressed, focus, disabled, loading, success e erro.
- [x] Foco visível e distinto do variant primário.
- [x] Loading bloqueia teclado e ponteiro nos estilos de classe; o
  `ActionButton` do shell é desabilitado enquanto atualiza.
- [x] Galeria `--gallery` nos dois idiomas, sem hardware.
- [x] Nenhum P/Invoke, COM ou chamada de hardware novo.
- [ ] Screenshots oficiais versionadas (P7a).
- [x] Aceite do usuário para avançar à P3.

### Checklist de aceite P3

- [x] Gamepad simulado conecta/desconecta e muda o estado.
- [x] Atraso de ativação: `Pending` → `Applying` → `Active`.
- [x] Desconectar durante o atraso cancela sem aplicar.
- [x] Desconectar após ativo espera o atraso de desativação e restaura.
- [x] Reconectar durante a desativação cancela a restauração.
- [x] Falha de aplicação vira `Error`, restaura e não grava journal.
- [x] Trocar modo com `Active` reaplica preservando o baseline.
- [x] Journal atômico grava `active` e é limpo ao restaurar.
- [x] Settings persistem toggles, atrasos, modos, launcher e idioma.
- [x] Nenhum hardware real, P/Invoke ou COM novo.
- [x] Aceite do usuário para avançar à P4/REA.

### Checklist da superfície P4 (DeepSeek)

- [x] Seletores de TV e monitor interno com descoberta read-only.
- [x] Seletores e `AllowDisplayWrites` persistidos em settings.
- [x] App só compõe display real + rescue com opt-in e seletores completos.
- [x] Aviso de recuperação incerta e toggle de automação bloqueado.
- [x] Testes de composição/settings sem hardware.
- [ ] Matriz real de TvOnly/Extend/Mirror + rollback (Codex/usuário).
