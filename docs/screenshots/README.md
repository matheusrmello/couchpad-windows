# Screenshots oficiais — P7a

- Data: 2026-11-18
- Versão exibida: `1.0.20`
- Estado: **capturas refeitas na 1.0.20**; aceite final pendente
- Shell: 8 cards, sem card de monitoramento de desempenho (removido na 1.0.20)
- Simulação: display/áudio sem escrita real; seletores vazios de propósito
- Leitor de tela: validação manual ainda pendente (ver checklist)

| Arquivo | Breakpoint | Idioma |
|---|---|---|
| `p7a-3col-pt.png` | 1020 DIP, 3 colunas | pt-BR |
| `p7a-2col-pt.png` | 780 DIP, 2 colunas | pt-BR |
| `p7a-1col-pt.png` | 640 DIP, 1 coluna | pt-BR |
| `p7a-3col-en.png` | 1020 DIP, 3 colunas | inglês |
| `p7a-2col-en.png` | 780 DIP, 2 colunas | inglês |
| `p7a-1col-en.png` | 640 DIP, 1 coluna | inglês |

## Layout

- 8 cards: Automação, Display, Gamepad, Combinação, Contagem regressiva,
  Parâmetros, Transição e Launchers. Não há card, overlay nem modal de
  monitoramento de desempenho.
- As larguras de cliente de 1004, 764 e 640 px correspondem a 3, 2 e 1 coluna
  de cards, respectivamente.
- Em 1020×780 o conteúdo usa a barra de rolagem; as capturas mostram o topo da
  tela.

## Notas

- As capturas usam settings seguros de simulação; `AllowDisplayWrites` e
  `AllowAudioWrites` ficam desligados, com automação desativada.
- A seção **Perfis de ambiente** aparece no card Display, com perfil padrão,
  criação, renomeação e exclusão. O perfil ativo é o mesmo usado pelo startup.
- O expander **Diagnóstico da identificação** mostra a decisão de TV, monitor
  e áudio, com motivo e confiança. As capturas o deixam recolhido; o conteúdo
  completo é validado por UIAutomation.
- A opção **Iniciar com o Windows** pode refletir a tarefa real da máquina; ela
  não executa escrita de display/áudio durante a captura.
- O tema é escuro; não há captura de tema claro.
- O ícone/janela e os assets são originais do CouchPad.
- A largura é a da janela configurada; a imagem inclui a moldura do Windows.
- As capturas usam o executável instalado de `1.0.20`; a publicação ainda está
  pendente.
- A validação com Narrator/NVDA não foi executada nesta máquina.

## Concept de redesign (não implementado)

- `couchpad-modern-concept.png` e `couchpad-modern-concept.html`: exploração
  visual com cards de vidro, tipografia mais forte, prévia da TV e diagnóstico
  em destaque. **Não representa a UI atual** e não foi implementado no App.
