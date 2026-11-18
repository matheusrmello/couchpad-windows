# Fluxos de UX — CouchPad

Cada fluxo lista gatilho, caminho feliz, falhas e regras de interface. A UI
nunca deve prometer o que a state machine ou o hardware não garantem.

## 1. Primeira execução e setup de hardware

**Gatilho:** app abre sem TV, monitor interno ou saída de áudio completos.

1. O card Automação mostra `ShowHardwareSetup` e um texto explicando o que
   falta, item por item.
2. O usuário escolhe TV, monitor interno e endpoint no card Display/Transição.
3. "Sugerir dispositivos" preenche uma combinação válida; "Aplicar
   configuração" fica desabilitado até a seleção estar completa.
4. Aplicar salva, espera a automação ficar idle e reinicia o app; a automação
   volta desligada.

**Falhas:** dispositivo indisponível, TV e monitor iguais, áudio ambíguo.
Mostre o motivo em texto, não só desabilite. Nunca escreva hardware sem opt-in.

**Regras:** uma instrução por vez; nada de jargão de EDID na superfície
principal (detalhes ficam no diagnóstico).

## 2. Ativar a automação

**Gatilho:** usuário liga o toggle do card Automação com hardware pronto.

1. Sem gamepad: `WaitingForGamepad`; a pill explica o que falta.
2. Gamepad conectado: `Pending` com contagem regressiva configurada.
3. `Applying`: ação primária desabilitada; nada de duplo clique.
4. `Active`: pill em acento, resumo de tela/áudio/launcher atualizado.

**Falhas:** apply falha → `Error` com causa e próximo passo; se o rollback não
for confirmado → `Recovery uncertain`, que bloqueia novas ativações.

**Regras:** o usuário sempre pode desligar; o texto do toggle diz o efeito, não
"ativar serviço". A contagem é visível e cancelável.

## 3. Desconectar e restaurar

**Gatilho:** gamepad desconecta ou usuário desliga a automação.

1. `Pending` de desativação com o mesmo padrão visual da ativação.
2. Tela, áudio e launcher voltam à baseline; journal fecha.
3. `Idle`; resumos voltam ao estado atual do sistema.

**Falhas:** restauração parcial → erro com instrução de retry; nunca declarar
"restaurado" sem confirmação. Se o app for fechado no meio, o journal e o rescue
assumem no próximo start.

## 4. Recuperação incerta e rescue

**Gatilho:** watchdog perdido, falha de rollback ou journal inconsistente.

1. Banner/card em `Danger` explica que a recuperação é incerta.
2. A automação fica bloqueada; "Tentar recuperar" é a ação primária.
3. O rescue separado pode concluir no próximo start; o usuário vê o progresso.
4. Só depois de confirmar é que o estado volta a permitir ativar.

**Regras:** nunca esconder o risco atrás de um expander. Não ofereça "ignorar"
como saída fácil. Falha de tela preta ou rollback: parar e reportar.

## 5. Erros, vazio e indisponível

- Erro sempre tem causa + próximo passo; não use "algo deu errado".
- Lista vazia explica o que fazer (ex.: "Atualizar alvos"), não só "nenhum".
- Ação desabilitada tem motivo visível ou em `HelpText`.
- `Loading` só depois de ~300 ms; mantenha o layout estável.
- Após erro recuperável, a ação primária volta a ficar disponível.

## 6. Atualizações e launchers

- Aviso de atualização é informativo, no cabeçalho; não interrompe a sessão.
- Abrir launcher é ação explícita; falha mostra caminho/configuração esperada.
- Nunca feche processos de terceiros sem ação do usuário.

## Regras transversais

1. Toda ação que muda hardware mostra o antes/depois no resumo.
2. Toda falha preserva a possibilidade de reverter.
3. O usuário nunca precisa adivinhar o estado: pill + card + rodapé contam a
   mesma história.
4. Se o fluxo exige ação física (controle, TV), diga isso claramente.
