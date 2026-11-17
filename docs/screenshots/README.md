# Screenshots oficiais — P7a

- Data: 2026-11-16
- Estado: **capturas atualizadas após a 1.0.16**; aceite final pendente
- Versão exibida: `1.0.16`
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

## Notas

- As capturas usam settings seguros de simulação; `AllowDisplayWrites` e
  `AllowAudioWrites` ficam desligados.
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
- Em 1020×780 o conteúdo usa a barra de rolagem; as capturas mostram o topo da
  tela. A ordem de foco foi validada via UIAutomation em 3/2/1 colunas.
- A validação com Narrator/NVDA não foi executada nesta máquina.
- As capturas usam o executável instalado de `1.0.16`; a publicação ainda está pendente.

## Concept de redesign (não implementado)

- `couchpad-modern-concept.png` e `couchpad-modern-concept.html`: exploração
  visual com cards de vidro, tipografia mais forte, prévia da TV e diagnóstico
  em destaque. **Não representa a UI atual** e não foi implementado no App.
- `couchpad-performance-hud-concept.png` e
  `couchpad-performance-hud-concept.html`: concept do card **HUD de
  desempenho**, com métricas selecionáveis, atalho LB+RB+Y e prévia do HUD.
  Aprovado para implementação em 2026-11-17; implementado na 1.0.14. Plano em
  `docs/roadmap.md`.

## HUD de desempenho — implementação 1.0.14/1.0.15

- `hud-fase1-overlay.png`: HUD instalado em simulação, com CPU, GPU, RAM, VRAM,
  processo e resolução; barras de GPU/VRAM corrigidas na 1.0.15.
- Capturas `p7a-*` da 1.0.16 mostram o novo shell, com o card HUD em linha
  inteira na primeira tela, nos três breakpoints e nos dois idiomas.
- `hud-fase1-settings.png`: modal de configuração com métricas, atalho,
  aparência e prévia.
- `couchpad-performance-hud-concept.png` e `.html`: concept original aprovado;
  a implementação mantém a mesma direção visual.
