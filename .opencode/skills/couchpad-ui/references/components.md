# Catálogo de componentes

## Card

**Anatomia:** `Border` com `Background=Surface`, `BorderBrush=Border`,
`CornerRadius=Card`, `Padding=20`.

**Estados:** normal, atenção (borda `Warning`), erro (borda `Danger`).

**Regra:** todo card tem `CardHeader` ou um título acessível.

## CardHeader

**Anatomia:** ícone 20 px + título `CardTitle` + ação opcional à direita.

**A11y:** o título é o `AutomationProperties.Name` do card.

## StatusDot

**Anatomia:** elipse 8 px.

**Cores:** `Accent` (ativo), `TextSecondary` (desconectado),
`Warning` (aguardando), `Danger` (erro).

**A11y:** nunca depender só da cor; incluir texto ao lado.

## StatusRow

**Anatomia:** `StatusDot` + label `Caption` + valor `BodyStrong`.

**Exemplo:** `Gamepad  Flydigi VADER3 — desconectado`.

**Regra:** valor sempre textual, não só ícone.

## ToggleSwitch

**Anatomia:** `ToggleSwitch` do Avalonia, estilizado com `Accent`.

**Estados:** on, off, disabled, focus-visible.

**A11y:** `AutomationProperties.Name` obrigatório; `HelpText` para explicar o
efeito.

**Regra:** mudança de toggle deve ser otimista na UI, mas com rollback visual se
o serviço falhar.

## Select

**Anatomia:** `ComboBox` com `SurfaceRaised`, borda `Border`, altura 36.

**Estados:** normal, aberto, disabled, carregando (placeholder “Carregando…”),
erro (borda `Danger`).

**A11y:** label associado; anúncio do valor selecionado.

## IconButton

**Anatomia:** botão 32×32, ícone 16–18, raio `Control`.

**Uso:** refresh, abrir pasta, limpar.

**A11y:** `AutomationProperties.Name` e tooltip com o mesmo texto.

## KeyChip

**Anatomia:** chip 28×28, raio `Control`, fonte mono, borda `Border`.

**Uso:** combinação de ativação (`1`, `L`, `R`, `BACK`).

**Estados:** vazio, preenchido, gravando, inválido.

**A11y:** anunciar a sequência completa, não só cada chip.

## PathPicker

**Anatomia:** `TextBox` somente leitura + `IconButton` de pasta.

**Estados:** vazio (“Caminho não especificado”), preenchido, inválido.

**Regra:** validar existência e extensão antes de salvar; não bloquear a UI.

## Pill

**Anatomia:** borda arredondada 999, fundo `AccentMuted`, texto `Accent`.

**Uso:** estado do serviço, “Grátis” (nunca “Licença ativa”).

**Estados:** ativo, inativo, atenção, erro.

## FooterLink

**Anatomia:** botão texto `TextSecondary`; hover `TextPrimary`.

**Uso:** “Redefinir”, “Sobre”, seletor de idioma.

## ConfirmDialog

**Anatomia:** título, mensagem, ação primária e cancelar.

**Uso:** redefinir configurações, fechar launcher, aplicar mudança arriscada.

**A11y:** foco inicial no cancelar; `Esc` fecha.

## Composição da tela

1. Header: marca + pill.
2. `AutomationCard` (herói, 2 colunas): toggle + resumo.
3. `DisplayCard`: display atual, destino, refresh.
4. `GamepadCard`: controle principal + reset.
5. `ComboCard`: chips + alterar.
6. `SettingsCard`: toggles + atrasos.
7. `TransitionCard`: modo de display + áudio.
8. `LaunchersCard`: Steam + custom.
9. Footer: status + ações + idioma.

## Regras gerais

- Um componente, uma responsabilidade.
- Sem lógica de serviço no controle.
- Estados vazios e de erro são obrigatórios.
- Textos sempre via i18n.
- Ícones de biblioteca aberta; não copiar ícones do ShiftPad.
