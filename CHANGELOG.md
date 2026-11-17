# Histórico de versões

## 1.0.14 (candidato)

- Adiciona o MVP do **HUD de desempenho**: CPU, CPU do jogo, GPU, RAM, VRAM, processo e resolução/refresh em uma janela flutuante.
- HUD sempre no topo, sem foco e click-through; posição, opacidade e escala configuráveis; opção de mostrar só durante a sessão.
- Combinação no controle LB+RB+Y (0,8 s), com presets; detecção de borda, debounce e cooldown.
- Card/modal de configuração com métricas selecionáveis, atalho, aparência e prévia ilustrativa.
- FPS, frame time, temperatura e integração RTSS/PresentMon ficam para as próximas fases.
- Build Debug/Release 0/0, **309 testes** e smoke. Validação em simulação (modal, HUD, sessão) e sampler real em produção com escritas desligadas.
- Instalação local validada sem elevação: App/rescue 1.0.14, HUD aberto no executável instalado com CPU/GPU/RAM/VRAM/processo/resolução; tasks preservadas. Instalador com 84.337.865 bytes e SHA-256 `A745068F7A6FA4D3A8025A706C8292B90C73D50F7EC31E35C53BAED89D7032E0`; publicação pendente.

## 1.0.13 (candidato)

- Adiciona diagnóstico explicável da identificação de TV, monitor e saída de áudio, com motivo, estado do dispositivo e confiança alta/média/baixa.
- O diagnóstico fica em um expander no card Display e não altera hardware; nomes, EDID, HDMI, ativo/disponível e associação do endpoint são mostrados no idioma atual.
- Build Debug/Release 0/0, 294 testes e smoke. Diagnóstico coberto por testes de confiança e conteúdo.
- Instalação local validada sem elevação: App/rescue 1.0.13, diagnóstico expandido por UIAutomation no executável instalado e estado de áudio ambíguo explicado sem escrita. Instalador com 84.283.279 bytes e SHA-256 `109DE17A9AF1E24AAA1000DB373B95EAB28B20C4D4F20BFC7EB707349722F373`; publicação pendente.

## 1.0.12 (candidato)

- Adiciona perfis de ambiente: cada perfil guarda TV, monitor interno e saída de áudio, com seletor, criação, renomeação e exclusão. O perfil ativo é persistido e espelhado nos campos de startup; configurações antigas migram para um perfil padrão.
- Trocar de perfil desliga a automação quando o hardware composto é diferente e exige **Aplicar configuração** para reiniciar com as novas escolhas.
- Build Debug/Release 0/0, 290 testes e smoke. Troca de perfil validada por UIAutomation com escritas de hardware desligadas.
- Instalação local validada sem elevação: App/rescue 1.0.12, tasks preservadas, abertura normal e troca Sala → Quarto no executável instalado. Instalador com 84.294.096 bytes e SHA-256 `E5DE69BFBFCB7CA8EC5F513B1070007C8D8B819F31E642C50C36C2523DA6F84A`; publicação pendente.

## 1.0.11 (candidato)

- Corrige a recuperação de display quando o Windows muda os LUIDs de adapter ou os source ids entre sessões. O snapshot do journal é reidratado pelas identidades estáveis de `DevicePath`/EDID; se a tela já está no baseline, o restore termina sem escrever.
- Adiciona `DisplaySnapshotRehydratorTests` cobrindo LUID antigo, `DevicePath` alterado com EDID/nome, extend com sources distintas, alvo conectado mas inativo, colapso de sources, ambiguidade, alvo indisponível, restauração sem escrita, aplicação com identidades atuais e rollback pelo snapshot ativo.
- Build Debug 0/0, 286 testes e smoke aprovados. Validação com o journal real de 15/11/2026: `STRICT=False`, `REHYDRATED=True`.
- Instalação local validada sem elevação; App e rescue 1.0.11, tasks preservadas, abertura sem banner e rescue sem journal retornando 0. Instalador com 84.286.297 bytes e SHA-256 `74605662FFD3437E78448AC2D0B300FBFA3B561D479E8A7D38F953715584A1EC`; publicação pendente.

## 1.0.10 (pré-release)

- Sugere TV, monitor e saída de áudio quando a identificação é clara. Permite revisar as escolhas e aplicar a configuração sem reabrir o App manualmente.
- Ordena as saídas disponíveis primeiro, indica a saída padrão e permite testar som na saída selecionada.
- Build Release 0/0, 275 testes e smoke aprovados. O probe concluiu o tom no AOC com os três papéis de áudio padrão preservados. Validação auditiva na Samsung e validação da aplicação da configuração com a versão instalada pendentes.
- Instalador Windows x64 sem assinatura Authenticode. SHA-256: `bd143958d458b95a6d8484721541ffd4043be7b3b5d627d383153a6000ae203c`.

## 1.0.9 (pré-release)

- Reformula a janela Sobre com ícone oficial, versão em destaque, descrição do aplicativo e licença MIT.
- Build Release 0/0, 272 testes e smoke aprovados. Janela conferida em português e inglês na build de simulação. Instalação desta versão pendente.
- Instalador Windows x64 sem assinatura Authenticode. SHA-256: `69698dd824fbe4f249f3c270870f829b14e4da37770488538ee912cc6c7c776f`.

## 1.0.8 (pré-release)

- Move o aviso de nova versão para o cabeçalho, em uma cápsula azul clicável ao lado do status do gamepad.
- Build Release 0/0, 272 testes e smoke aprovados. Prévia conferida com atualização fictícia em pt-BR a 1020 DIP e em inglês a 640 DIP. Foco por teclado verificado.
- Instalador Windows x64 sem assinatura Authenticode. SHA-256: `5943047e1647dd7c1ed6f1412e13fa174e228ec382448e394e5ee3276991a508`.

## 1.0.7 (pré-release)

- Adiciona ícones aos oito cards, incluindo uma TV para Display e um controle para Gamepad.
- Build Release sem avisos ou erros, 272 testes e smoke aprovados. Ícones conferidos na janela em simulação, em pt-BR, no layout de três colunas.
- Instalador Windows x64 sem assinatura Authenticode. SHA-256: `2ba5af7b43f81e14f84578f3ffd1238189bcbdf3f69637b56f2706d4e3048e78`.

## 1.0.6 (pré-release)

- Mostra um aviso de nova versão no rodapé com link para a release do GitHub.
- A consulta em segundo plano pode ser desligada. O canal estável é o padrão, com opção de incluir pré-releases.
- Build Release sem avisos ou erros, 272 testes e smoke aprovados. A confirmação visual do aviso na versão instalada ainda está pendente.
- Instalador Windows x64 sem assinatura Authenticode. SHA-256: `f5db9d3e7b515e77b7efbd1578f30f6ab676cc648d586145af23da632f0b07ae`.

## 1.0.5 (pré-release)

- Detecta o controle físico por padrão no Windows. A simulação fica disponível com `--simulate`.
- O botão Automação é liberado depois de selecionar TV, monitor e áudio e reabrir o CouchPad. Alterar os dispositivos pausa a automação.
- Build Release sem avisos ou erros, 269 testes e smoke aprovados. A primeira configuração e a transição com TV real ainda precisam de validação interativa.

## 1.0.4 (pré-release)

- Mostra o nome do controle 8BitDo Ultimate 2 ou Ultimate 2C quando o XInput informa um par de IDs reconhecido.
- Inclui correções de abertura e saída do Steam Big Picture e de fechamento da janela Big Picture do Hydra criada pela sessão.
- Inclui verificação antecipada das tarefas de recuperação no instalador e preserva registros de outras instalações na remoção.
- Build Release sem avisos ou erros, 267 testes e smoke aprovados. Ainda falta confirmar em hardware a exibição do nome e a saída do Big Picture nos cenários corrigidos.
- Instalador Windows x64 sem assinatura Authenticode. SHA-256: `87b205928969d6da98de6705f474dab7f7a4929bea2774c0dee14ce671fa74ed`.

## 1.0.0

- Instalador Windows x64 autocontido por usuário.
- Bandeja, autostart opcional, recuperação assistida e rescue no logon.
- Simulação por padrão, com controle XInput, tela e áudio reais por opção.
- Interface pt-BR/en com foco e nomes validados por UIAutomation.

Veja [notas e limitações](docs/release-notes-draft.md).
