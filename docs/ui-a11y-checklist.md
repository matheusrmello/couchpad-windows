# Checklist de acessibilidade — P7a

- Data: 2026-11-11
- Escopo: shell Avalonia, cards, controles, i18n e estados de erro
- Base: WCAG 2.2 AA (alvos principais), teclado, leitor de tela e contraste
- Limite: auditoria estática/manual; não substitui teste com todos os leitores
  de tela do mercado

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

## 2. Teclado e foco

- [x] `Tab` percorre cards e controles na ordem visual.
- [x] `Enter`/`Espaço` ativam botões e toggles.
- [x] `Esc` fecha Sobre e Galeria.
- [x] Focus visível no `ActionButton` (borda `TextPrimary` 2 px).
- [x] Focus visível no `ToggleSwitch` (anel de acento).
- [x] `ComboBox` usa o foco do Fluent, com borda de acento.
- [x] Links do rodapé mudam fundo/cor no foco.
- [x] Nenhum controle depende de hover para funcionar.

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

## 4. Alvos e layout

- [x] Botões de 32/36 px; links ≥ 28 px.
- [x] Toggle 44×24 px; atende ao mínimo 24×24 da WCAG 2.2 AA.
- [x] Layout 3/2/1 colunas; conteúdo rolável.
- [x] Janela mínima 640×640.
- [x] Textos longos usam `TextWrapping`; caminhos usam ellipsis.
- [x] Nenhum texto é cortado no breakpoint de 1 coluna na captura atual.

## 5. i18n e conteúdo

- [x] pt-BR e inglês em paridade (138 chaves).
- [x] Nenhuma string visível fora dos catálogos.
- [x] Fallback inglês.
- [x] Erros e dicas traduzidos.
- [x] Nomes de hardware (Samsung/AOC) são dados, não strings de UI.

## 6. Movimento e estado

- [x] Sem animação obrigatória para entender estado.
- [x] Barra de progresso é complementar ao texto.
- [x] Estados normal/hover/pressed/focus/disabled/loading/erro cobertos.
- [x] `recoveryUncertain` bloqueia nova automação até reiniciar.

## 7. Limitações conhecidas

- Tema claro não existe; apenas escuro.
- Teste com NVDA/Narrator completo ainda não foi registrado.
- A bandeja/autostart da P6 podem alterar a ordem de foco; revisar depois.
- As capturas oficiais serão refeitas após a P6.
- HDR adiado não entra neste checklist.

## 8. Verificação

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"

& $dotnet build CouchPad.slnx -c Debug
& $dotnet test CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --smoke
```

- Build 0 avisos/0 erros.
- 194 testes.
- Smoke OK.
- Capturas em `docs/screenshots/` nos 3 breakpoints × 2 idiomas.
