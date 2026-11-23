# Screenshots oficiais — P7a

- Data: 2026-11-23
- Versão exibida: `1.0.24`
- Estado: **capturas refeitas na 1.0.24**; aceite final pendente
- Shell: 9 cards em masonry, com card TV separado do card Display
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

- 9 cards: Automação, Display, TV, Gamepad, Combinação, Contagem regressiva,
  Transição, Launchers e Parâmetros. Resolução, taxa e escala da TV ficam no
  card **TV**, separado do card Display.
- O shell usa **masonry**: três colunas independentes (`StackPanel`), cada uma
  empilhando cards com altura natural. Um card curto não estica para igualar o
  vizinho, então não sobram corpos vazios.
- Em 3 colunas: coluna 0 com Automação, Contagem e Launchers; coluna 1 com
  Display, Gamepad e Combinação; coluna 2 com TV, Transição e Parâmetros.
- Em 2 colunas: coluna 0 com Display, Automação, Contagem e Launchers; coluna 1
  com TV, Gamepad, Combinação, Transição e Parâmetros.
- Em 1 coluna: Automação, Display, TV, Gamepad, Combinação, Contagem,
  Transição, Launchers e Parâmetros, na ordem de leitura.
- As larguras de cliente de 1004, 764 e 640 px correspondem a 3, 2 e 1 coluna
  de cards, respectivamente.
- Em 1020×780 o conteúdo usa a barra de rolagem; as capturas mostram o topo da
  tela.

## Notas

- As capturas usam settings seguros de simulação; `AllowDisplayWrites` e
  `AllowAudioWrites` ficam desligados, com automação desativada.
- A seção **Perfis de ambiente** aparece no card Display, com perfil padrão,
  criação, renomeação e exclusão. O perfil ativo é o mesmo usado pelo startup.
- O card **TV** mostra resolução, taxa de atualização e escala do Windows do
  perfil ativo, além dos seletores de destino da TV e do monitor interno.
- O expander **Diagnóstico da identificação** mostra a decisão de TV, monitor
  e áudio, com motivo e confiança. As capturas o deixam recolhido; o conteúdo
  completo é validado por UIAutomation.
- O card **Parâmetros** organiza os toggles em duas colunas internas.
- A opção **Iniciar com o Windows** pode refletir a tarefa real da máquina; ela
  não executa escrita de display/áudio durante a captura.
- O tema é escuro; não há captura de tema claro.
- O ícone/janela e os assets são originais do CouchPad.
- A largura é a da janela configurada; a imagem inclui a moldura do Windows.
- As capturas usam o executável instalado de `1.0.24`; a publicação ainda está
  pendente.
- A validação com Narrator/NVDA não foi executada nesta máquina.

## Concept de redesign (não implementado)

- `couchpad-modern-concept.png` e `couchpad-modern-concept.html`: exploração
  visual com cards de vidro, tipografia mais forte, prévia da TV e diagnóstico
  em destaque. **Não representa a UI atual** e não foi implementado no App.
- `couchpad-layout-redesign.png` e `couchpad-layout-redesign.html`: proposta
  estática do shell em masonry com card TV separado, usada para revisar a
  direção visual da 1.0.24. A implementação real está nas capturas `p7a-*`.
