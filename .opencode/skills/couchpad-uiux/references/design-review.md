# Revisão de UI/UX — checklist

Use antes de handoff, release ou mudança visual relevante. A revisão é baseada
em evidência: código, capturas nos dois idiomas e três larguras, build/testes e
navegação por teclado.

## Evidência mínima

- [ ] Build Debug e Release com 0 avisos/0 erros.
- [ ] `dotnet test` e `--smoke` aprovados.
- [ ] Capturas em 1020, 780 e 640 px, em pt-BR e inglês.
- [ ] Pelo menos uma captura em 200% DPI ou inspeção equivalente.
- [ ] Teclado: Tab percorre na ordem visual; foco é visível.
- [ ] Strings novas presentes nos dois catálogos.

## Hierarquia e conteúdo

- [ ] A ação primária de cada card é óbvia em 3 segundos.
- [ ] O card HUD aparece inteiro na primeira tela em 1020×780.
- [ ] Estado global fica no cabeçalho, não repetido em cada card.
- [ ] Nenhum dado falso é apresentado como ao vivo.
- [ ] Textos explicam consequência antes de ação que escreve hardware.

## Layout

- [ ] 3 colunas ≥ 1000 px; 2 em 720–999; 1 abaixo de 720.
- [ ] Sem scroll horizontal; nada cortado à direita em 640 px.
- [ ] Cards parecidos em altura ficam na mesma linha.
- [ ] Cabeçalho e rodapé fixos; conteúdo rola.
- [ ] Ordem de Tab igual à ordem visual em cada breakpoint.
- [ ] 100–200% DPI sem sobreposição nem corte.

## Cor, tipo e espaçamento

- [ ] Só tokens `CouchPad.*`; nenhum hex novo na View.
- [ ] Tamanhos de fonte do catálogo (20/15/13/11/12).
- [ ] Padding 16 no card, 24 na janela, gap 10.
- [ ] Contraste ≥ 4.5:1 texto e ≥ 3:1 componentes.
- [ ] Estado não depende só de cor.
- [ ] GPU/VRAM em `Info`; CPU/RAM em `Accent`; trilho em `HudTrack`.

## Componentes e estados

- [ ] Reutilizou o catálogo antes de criar controle.
- [ ] Normal, hover, pressed, focus-visible e disabled presentes.
- [ ] Loading e erro quando a ação é assíncrona.
- [ ] Desabilitado explica o motivo (`HelpText`/tooltip).
- [ ] Chips são somente leitura; não parecem botões.
- [ ] Modal devolve foco e fecha sem prender a janela.

## Copy e i18n

- [ ] Chaves nos dois JSON; paridade preservada.
- [ ] Nenhuma string hardcoded em XAML/CS.
- [ ] Sem concatenação; placeholders quando necessário.
- [ ] Unidades e separadores corretos em pt-BR e inglês.
- [ ] Texto mais longo não quebra layout nem botão.
- [ ] Tom direto, sem exclamação e sem culpar o usuário.

## Acessibilidade

- [ ] `AutomationProperties.Name` em toggle, combo, botão de ícone e chip clicável.
- [ ] `HelpText` em ação destrutiva ou desabilitada.
- [ ] Status importante com `LiveSetting=Polite`.
- [ ] Alvos ≥ 32 px; foco visível.
- [ ] HUD não recebe foco; modal recebe e devolve.
- [ ] Narrator/NVDA: leitura básica verificada quando o fluxo mudou.

## HUD

- [ ] Prévia rotulada como ilustrativa.
- [ ] 0% vazio; > 0 com no mínimo 2 px.
- [ ] Indisponível mostra `—`.
- [ ] GPU/VRAM em `Info`; sem token inexistente.
- [ ] Opacidade, escala, posição e sessão persistem.
- [ ] Chord exige botões exatos por 800 ms.
- [ ] Overlay continua click-through e sem foco.

## Arquitetura e higiene

- [ ] Nenhum P/Invoke/COM na `App`; nenhuma regra de produto na plataforma.
- [ ] View sem lógica de negócio; ViewModel sem controle visual.
- [ ] Nenhum asset/texto/layout copiado do ShiftPad.
- [ ] Sem dependência nova sem decisão registrada.
- [ ] `HANDOFF.md`/`docs/decisions.md` atualizados quando aplicável.

## Severidade

| Nível | Critério | Ação |
|---|---|---|
| Blocker | string hardcoded, token inexistente, corte/sobreposição, controle inacessível, dado falso sem rótulo, lógica de negócio na View, chamada de hardware na UI | não aprovar |
| Major | estado ausente, disabled sem motivo, contraste < 4.5:1, ordem de foco errada, i18n truncado | corrigir antes do handoff |
| Minor | espaçamento inconsistente, ícone desalinhado, texto redundante, sombra/realce fora do padrão | registrar e corrigir se barato |

## Formato do relatório

| ID | Severidade | Local | Problema | Evidência | Correção |
|---|---|---|---|---|---|
| R1 | Blocker | `Views/X.axaml:42` | texto fixo "OK" | captura 640 px | `{loc:Tr ...}` |

Feche com:

- **Aprovado** — sem blockers/majors.
- **Aprovado com ressalvas** — só minors, listados.
- **Reprovado** — ao menos um blocker.

Anexe as capturas e a saída de build/testes à evidência do handoff.
