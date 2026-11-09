# CouchPad — Especificação de UI (P1)

- Data: 2026-11-09
- Fase: P1 — shell estático, sem hardware
- Base visual: referência do ShiftPad 2.2.0, recriada com identidade própria
- Plataforma: Avalonia 12.1.4 / .NET 10 (`net10.0`)

> Esta spec descreve o shell entregue na P1. Ela não copia ícones, textos,
> imagens, XAML ou código do ShiftPad. Todos os textos visíveis vêm de
> `Strings.pt-BR.json` e `Strings.en.json`.

---

## 1. Princípios

1. Tema escuro com cards arredondados, acento verde e superfícies neutras.
2. Nenhuma string visível fora dos dois catálogos de idioma.
3. Estados nunca dependem só de cor: sempre há texto ou forma ao lado.
4. Layout responsivo em três breakpoints, com rolagem vertical quando o
   conteúdo não couber.
5. Nenhuma chamada de tela, áudio, HDR ou gamepad na P1.
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
- Pill de estado com `StatusDot` + texto:
  - automação desligada → `status.idle` (neutro);
  - automação ligada → `status.waiting` (âmbar, aguardando gamepad).
- A pill de estado usa `AutomationProperties.LiveSetting=Polite`.

### 3.2 Card Automação

- Título `section.automation`.
- `ToggleSwitch` ligado/desligado (`automation.toggle`), com `HelpText`.
- Texto de ajuda (`automation.help`).
- Resumo em quatro linhas: gamepad, tela, áudio e launcher.
- Valores da P1: nenhum controle, monitor principal, saída padrão e
  Steam/nenhum, conforme o toggle de Steam.

### 3.3 Card Display

- Título `section.display`.
- Botão `display.refresh`; durante a atualização mostra
  `display.refreshing` e fica desabilitado por 700 ms (placeholder local).
- Colunas `Atual` e `Destino` com valores estáticos:
  `display.current_value` e `display.target_value` (este em acento).
- Nenhuma chamada CCD na P1.

### 3.4 Card Gamepad

- Título `section.gamepad`.
- Linha `Controle principal` com `StatusDot` neutro e `gamepad.none`.
- Botão `gamepad.reset` desabilitado até a P3/P6.

### 3.5 Card Combinação

- Título `section.combo`.
- Ajuda `combo.help`.
- Quatro `KeyChip`: `1`, `LB`, `RB`, `A`, todos vindos do catálogo.
- Botão `combo.change` desabilitado até a captura real de botões.

### 3.6 Card Parâmetros

- Título `section.settings`.
- Toggles: fechar launcher ao ativar, iniciar com o Windows, minimizar para a
  bandeja.
- Selects: atraso ao ativar e ao desativar (Imediato, 1 s, 2 s, 3 s, 5 s).
- Ajuda `settings.help`.
- Na P1 os valores existem só em memória; persistência entra na P3.

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
- `Redefinir` (`footer.reset`) restaura os padrões da P1.
- `Sobre` (`footer.about`) abre `AboutWindow` como diálogo modal.
- Seletor de idioma com `language.ptbr` e `language.en`.

### 3.10 Sobre

- Janela modal `440 × 340`, centralizada no dono, sem botão na barra de
  tarefas.
- Marca, tagline, aviso 100% gratuito, licença MIT, versão e fase.
- Botão `about.close` (`Esc`/`Enter` fecham).

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

| Componente | Implementação | Estados cobertos na P1 |
|---|---|---|
| `Card` | `Border.card` | normal, atenção, erro |
| `Pill` | `Border.pill` | neutro, acento, aviso, erro |
| `StatusDot` | `Ellipse.dot` | neutro, ativo, aviso, erro |
| `ToggleSwitch` | template próprio | on, off, disabled, focus |
| `Select` | `ComboBox` estilizado | normal, hover, focus, disabled |
| `Button` | `secondary`, `primary`, `link` | normal, hover, pressed, focus, disabled |
| `KeyChip` | `Border.keyChip` | preenchido |
| `PathPicker` | `Border.pathBox` + botão | vazio, preenchido |
| `FooterLink` | `Button.link` | normal, hover, pressed |

O template do toggle desenha o visual próprio; o arrasto do knob fica
desabilitado nesta fase e `OnContent`/`OffContent` não são renderizados.
Os rótulos ficam sempre fora do controle, com `AutomationProperties.Name`.

Regras:

- Toggle sempre com `AutomationProperties.Name`; quando necessário,
  `HelpText`.
- Ícone ou controle sem texto visível sempre com nome de acessibilidade.
- Botão desabilitado sempre com opacidade reduzida; na P1, `Redefinir
  controle` e `Alterar` mostram o motivo via `ToolTip.Tip`/`HelpText`.

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
`transition.*`, `display_mode.*`, `audio_mode.*`, `launchers.*`, `footer.*`,
`language.*`, `about.*`.

---

## 7. Acessibilidade

- Contraste: texto normal `TextPrimary/Background` e
  `TextSecondary/Surface` acima de 4.5:1; acento sobre `AccentMuted` acima de
  3:1.
- Alvos de toque: controles com pelo menos 28–32 px de altura.
- Teclado: `Tab` percorre todos os controles; `Enter`/`Espaço` ativam;
  `Esc` fecha o Sobre.
- Foco visível: anel de acento no toggle e foco padrão do Fluent nos demais.
- Status anunciado com `LiveSetting=Polite`.
- Nunca usar só cor: todo dot tem texto ao lado.
- Textos longos usam `TextWrapping`/`TextTrimming`.

---

## 8. Comportamento da P1

| Ação | Efeito |
|---|---|
| Alternar automação | Estado vai de `Inativo` para `Aguardando gamepad` (sem hardware real) |
| Atualizar displays | Texto vira `Atualizando…` por 700 ms; nenhum CCD |
| Redefinir | Restaura toggles, atrasos, modos e caminho do launcher |
| Escolher pasta | Seletor nativo de pasta; grava o caminho em memória |
| Sobre | Abre diálogo modal com versão e licença |
| Idioma | Troca todos os textos em tempo real e persiste a preferência |

Fora do escopo da P1: detecção de gamepad, captura de combinação, leitura real
de displays/áudio/HDR, bandeja, autostart, rescue e persistência dos cards.

---

## 9. Verificação

- Build: 0 avisos, 0 erros.
- Testes: catálogos + uso de i18n no XAML.
- Smoke: janela `1020 × 780`, troca pt/en, título `CouchPad`.
- Capturas de revisão em `C:\Users\Usuario\AppData\Local\Temp\opencode\`:
  - `p1-3col.png` (1020×780, pt-BR);
  - `p1-2col.png` (860×780, pt-BR);
  - `p1-1col.png` (660×780, pt-BR);
  - `p1-en.png`, `p1-en-waiting.png` (inglês, idle e aguardando);
  - `p1-screen.png` (diálogo Sobre aberto).
- As capturas definitivas dos 3 breakpoints × 2 idiomas serão versionadas na
  P7a em `docs/screenshots/`.

### Checklist de aceite

- [x] 8 áreas visíveis: cabeçalho, 7 cards e rodapé.
- [x] Layout em 3, 2 e 1 coluna, com rolagem quando necessário.
- [x] Nenhuma string visível fora dos catálogos.
- [x] Toggle, select, botão, pill e dot com estados visuais.
- [x] Nome de acessibilidade nos controles sem texto.
- [x] Diálogo Sobre funcional e fechável por `Esc`.
- [ ] Screenshots oficiais versionadas (P7a).
- [ ] Aceite do usuário para avançar à P2.
