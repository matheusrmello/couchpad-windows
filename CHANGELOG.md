# Histórico de versões

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
