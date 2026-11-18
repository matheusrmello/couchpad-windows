# Checklist de acessibilidade — P7a

- Data: 2026-11-16
- Aceite do usuário: 2026-11-13 (P7a); perfis adicionados em 2026-11-16
- Escopo: shell Avalonia, cards, controles, bandeja, autostart, recuperação
  assistida, i18n e estados de erro
- Base: WCAG 2.2 AA (alvos principais), teclado, leitor de tela e contraste
- Método: inspeção estática + UIAutomation (nomes/ordem de foco) + testes
  automatizados; **Narrator/NVDA não executados** nesta máquina

---

## 1. Contraste

Cálculo WCAG sobre os tokens atuais:

| Combinação | Contraste | Resultado |
|---|---:|---|
| `TextPrimary` / `Background` | 17,82:1 | AA/AAA |
| `TextSecondary` / `Surface` | 6,73:1 | AA/AAA |
| `TextPrimary` / `Surface` | 16,00:1 | AA/AAA |
| `Accent` / `AccentMuted` | 6,05:1 | AA/AAA |
| `Warning` / `WarningMuted` | 8,11:1 | AA/AAA |
| `Danger` / `DangerMuted` | 4,62:1 | AA (após ajuste) |
| `Accent` / `Background` | 9,82:1 | AA/AAA |

Ajuste: `DangerMuted` passou de `#3A1D1E` (4,39:1) para `#35191B`
(4,62:1), garantindo AA para texto de perigo em 11 px.

- [x] Texto normal ≥ 4,5:1.
- [x] Texto de estado (pill/erro) ≥ 4,5:1.
- [x] Cor nunca é o único indicador; sempre há texto.
- [x] Disabled usa opacidade e texto, com isenção de contraste da WCAG.
- [x] Erros de startup, launcher e recuperação usam `Danger` + texto.

## 2. Teclado e foco

- [x] `Tab` percorre cards e controles na ordem visual.
- [x] `Enter`/`Espaço` ativam botões e toggles.
- [x] `Esc` fecha Sobre e Galeria.
- [x] Focus visível no `ActionButton` (borda `TextPrimary` 2 px).
- [x] Focus visível no `ToggleSwitch` (anel de acento).
- [x] `ComboBox` usa o foco do Fluent, com borda de acento.
- [x] Links do rodapé mudam fundo/cor no foco.
- [x] Nenhum controle depende de hover para funcionar.
- [x] Ordem de foco alinhada à ordem visual (3/2/1 colunas) via reordenação do
  visual tree no `ApplyLayout`.
- [x] UIAutomation confirmou ordem e nomes em 3 colunas (pt-BR), 2 colunas
  (inglês) e 1 coluna (pt-BR), sem controles focáveis sem nome.
- [ ] Teste manual completo com Narrator/NVDA (pendente).

## 3. Nomes e leitores de tela

- [x] Toggles com `AutomationProperties.Name`.
- [x] Selects de atraso, modo, áudio e idioma com nome.
- [x] Seletores de TV, monitor interno e endpoint de áudio com nome.
- [x] Botões “Atualizar alvos”, “Atualizar displays” e “Escolher pasta” com nome.
- [x] `StatusPill` anuncia mudanças com `LiveSetting=Polite`.
- [x] Aviso de recuperação incerta com `LiveSetting=Polite`.
- [x] Erro de descoberta de display com `LiveSetting=Polite`.
- [x] `ActionButton` de sucesso/erro mantém texto ao lado do ícone.
- [x] `KeyChip` e `StatusRow` têm texto visível.
- [x] Autostart: toggle com nome/ajuda e erro com live region.
- [x] Recuperação assistida: `Tentar recuperar` com ajuda; ajuda/erro com
  `LiveSetting=Polite` e contraste de perigo.
- [x] Launcher: erro com live region e contraste de perigo.
- [x] Bandeja: itens com cabeçalhos localizados (`Abrir`, `Ativar/Desativar`,
  `Sair`) e tooltip usando `app.name`.
- [x] Perfis: combo com nome/ajuda, botões Novo/Excluir nomeados e campo de
  nome rotulado; Excluir fica desabilitado quando só há um perfil.
- [x] Diagnóstico: expander com nome/ajuda e texto somente leitura no idioma
  atual; não altera hardware.
- [x] HUD: modal com labels, checkboxes, combo de atalho e sliders; HUD não
  recebe foco e é click-through.
- [x] Card HUD: toggle e botão com `AutomationProperties.Name`, checkbox de
  sessão, estado com live region e chips somente leitura na ordem de foco.
- [ ] Chord físico do HUD e anúncio do modal por Narrator/NVDA (manual
  pendente).
- [ ] Títulos dos cards não são expostos como peers de texto no UIA; revisar
  template do `Card` (a11y).
- [ ] Navegação por teclado/leitor de tela dentro do menu nativo da bandeja
  (depende do shell do Windows; não automatizada).

## 4. Alvos e layout

- [x] Botões de 32/36 px; links ≥ 28 px.
- [x] Toggle 44×24 px; atende ao mínimo 24×24 da WCAG 2.2 AA.
- [x] Layout 3/2/1 colunas; conteúdo rolável.
- [x] Janela mínima 640×640.
- [x] Textos longos usam `TextWrapping`; caminhos usam ellipsis.
- [x] Nenhum texto é cortado no breakpoint de 1 coluna na captura atual.

## 5. i18n e conteúdo

- [x] pt-BR e inglês em paridade (258 chaves).
- [x] Nenhuma string visível fora dos catálogos.
- [x] Fallback inglês.
- [x] Erros e dicas traduzidos.
- [x] Nomes de hardware (Samsung/AOC) são dados, não strings de UI.
- [x] Itens da bandeja e tooltip vêm do catálogo.

## 6. Movimento e estado

- [x] Sem animação obrigatória para entender estado.
- [x] Barra de progresso é complementar ao texto.
- [x] Estados normal/hover/pressed/focus/disabled/loading/erro cobertos.
- [x] `recoveryUncertain` bloqueia nova automação até reiniciar.
- [x] Contagem regressiva é textual na status pill; o anúncio por leitor de
  tela não foi medido e pode ser verboso em atrasos de 5 s.

## 6.1 Bandeja, autostart e recuperação

- [x] Bandeja validada funcionalmente na P6 (abrir, ativar/desativar, sair).
- [x] Autostart por tarefa por usuário validado na P6; toggle reflete o estado.
- [x] Recuperação assistida validada na P6; botão inicia o rescue separado.
- [x] Falha de autostart reverte o toggle e exibe erro anunciado.
- [ ] Interação da bandeja com Narrator/NVDA (manual pendente).
- [ ] Navegação completa por teclado no menu nativo da bandeja (shell Windows).

## 7. Limitações conhecidas

- Tema claro não existe; apenas escuro.
- Teste com NVDA/Narrator completo ainda não foi registrado; a árvore UIA e os
  nomes foram validados por UIAutomation.
- A bandeja é nativa do Windows; o menu não foi percorrido por leitor de tela.
- O anúncio da contagem regressiva pode ser frequente em atrasos de 5 s.
- HDR e notificações nativas adiados não entram neste checklist.

## 8. Verificação

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"

& $dotnet build CouchPad.slnx -c Debug
& $dotnet test CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --smoke
```

- Build 0 avisos/0 erros.
- 309 testes.
- Smoke OK.
- Capturas oficiais em `docs/screenshots/` nos 3 breakpoints × 2 idiomas.
- UIAutomation: ordem de foco e nomes em 3/2/1 colunas.
- Narrator/NVDA: pendente de teste manual.
