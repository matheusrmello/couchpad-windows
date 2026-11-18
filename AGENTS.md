# AGENTS.md — CouchPad Windows

Regras ativas para **todos** os agentes do projeto, inclusive subagentes.

## Leitura obrigatória antes de planejar

1. `.opencode/codebase-map.md` — stack, estrutura, comandos e convenções.
2. Este `AGENTS.md` — invariantes, limites e gate de qualidade.
3. `HANDOFF.md` e `docs/decisions.md` — estado e decisões vigentes.

O `orchestrator` **não pode delegar** antes de ler os itens 1 e 2. O `worker`
recebe no brief os caminhos e as regras aplicáveis e não replaneja o escopo.
Estrutura, dependências e comandos ficam no mapa; não os duplique aqui.

## Papéis

- `orchestrator`: decompõe, delega ao `worker`, avalia retornos e sintetiza.
  Não edita arquivos nem executa shell.
- `worker`: implementa, lê, busca e roda build/testes. Devolve arquivos
  alterados, comandos executados e resultado.
- Demais subagentes (`ui-builder`, `sim-engineer`, `scaffolder`, `reviewer`,
  `docs-writer`) seguem estas mesmas regras.

## Skills

- `couchpad-uiux`: especialista UI/UX — direção visual, fluxos, a11y e revisão.
- `couchpad-ui`: implementação Avalonia — XAML, MVVM, tokens, componentes e i18n.
- `couchpad-hardware`, `couchpad-rea`, `couchpad-release`, `couchpad-handoff`:
  fases específicas de hardware, análise, release e fechamento.

## Missão

Recriar de forma limpa e 100% gratuita o comportamento útil do ShiftPad 2.2.0:
trocar a tela para a TV, ajustar o áudio, lidar com HDR e oferecer recuperação
segura (rescue).

## Invariantes

1. **100% free**: sem licença, trial, ativação, assinatura ou telemetria.
2. **Avalonia 12.1.4** com **.NET 10** (`net10.0`).
3. **pt-BR + inglês**; nenhuma string visível fora dos dois dicionários.
4. UI **inspirada** na referência; **nunca copiar** ícone, texto, imagem, XAML
   ou código do ShiftPad.
5. **Clean-room** e **REA somente estático**: não executar o ShiftPad nem
   contornar proteções.
6. **Fail-closed**: nenhuma operação de hardware pode deixar a máquina sem
   display utilizável. Capturar, aplicar, verificar e reverter em falha.
7. **Sem admin por padrão**; dados por usuário em `%LOCALAPPDATA%\CouchPad`.
8. **Sem telemetria**; logs e diagnóstico ficam locais.
9. Não encerrar, atualizar ou modificar processos de terceiros.
10. Não adicionar dependência, pacote ou mudança de arquitetura sem decisão
    registrada em `docs/decisions.md`.

## Fronteira de camadas

| Camada | Pode | Não pode |
|---|---|---|
| `src/CouchPad.App` | Views, ViewModels, tema, i18n, navegação | P/Invoke, COM, `SetDisplayConfig`, Core Audio, Task Scheduler |
| `src/CouchPad.Core` | Modelos, interfaces, simulação, state machine, journal, testes puros | Depender de Avalonia, Win32 ou hardware |
| `src/CouchPad.Platform.Windows` | Implementar interfaces do Core com APIs do Windows | Regra de produto, texto de UI, decisão de fluxo |
| `tests/*` | Testes puros, fakes, journal, state machine | Hardware real, tela, áudio |

Mudança que cruze a fronteira exige decisão em `docs/decisions.md`.

## Comandos

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"

& $dotnet build CouchPad.slnx -c Debug
& $dotnet build CouchPad.slnx -c Release
& $dotnet test CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --smoke
```

- **Lint:** não existe `dotnet format`, script de lint nem CI. O gate é build
  Debug **e** Release com **0 avisos/0 erros**, testes e smoke. `.editorconfig`
  é referência de estilo.
- Instalador: `packaging\Build-Installer.ps1 -Dotnet $dotnet -Iscc <ISCC.exe> -Version X.Y.Z`.
- `--smoke` abre a janela, imprime uma linha e fecha; exige sessão gráfica.

## Convenções

- MVVM com `[ObservableProperty]`/`[RelayCommand]`; ViewModel não conhece
  controle visual. DI manual em `App.axaml.cs`.
- Interface no Core + `Simulated*`/fake + implementação em `Platform.Windows`.
- Records para modelos; namespaces file-scoped; `sealed` por padrão; campos
  `_camelCase`.
- i18n obrigatório: adicione a chave nos **dois** JSON antes de usar
  `{loc:Tr chave}`. Nunca concatenar frases; use placeholders.
- Views usam tokens de `Themes/Tokens.axaml`; sem cor literal nova. Controles
  reutilizam o catálogo de `Themes/Components.axaml`.
- Fail-closed: journal `pending` antes do apply, `active` depois, rollback e
  `_recoveryUncertain` quando o watchdog se perde.
- Testes puros, determinísticos e sem hardware; use `TimeProvider` e fakes.

## Gotchas

- Use o SDK absoluto acima; o `dotnet` do PATH não tem SDK.
- Buildar só o App em árvore limpa não copia `CouchPad.Rescue.exe`; builde a
  solução.
- Smoke e simulação usam settings isolados em `%TEMP%`; não confunda com o
  estado real em `%LOCALAPPDATA%\CouchPad`.
- Journal `active` pendente bloqueia autostart/rescue; conclua a recuperação.
- Escritas de display/áudio são opt-in e exigem os runbooks (`docs/p4-runbook.md`,
  `docs/p5-audio-hdr.md`, `docs/p6-recovery-matrix.md`). Falha de rollback ou
  tela preta: pare imediatamente.
- Não rode harnesses `tools/*Hardware*` sem TV, usuário presente e runbook.
- Instalador exige árvore limpa, Inno 6.7.4 e `-p:CouchPadInstallerPublish=true`.
- Codex está descontinuado (D079); `HANDOFF.md` e `docs/decisions.md` são a
  fonte vigente.

## Definição de pronto

1. Build Debug e Release 0/0, testes e smoke aprovados.
2. Nenhuma string fora dos dois dicionários.
3. Fronteira de camadas respeitada; nenhum P/Invoke fora de `Platform.Windows`.
4. Nenhum código/asset copiado do ShiftPad.
5. `HANDOFF.md` e `docs/decisions.md` atualizados.
6. Revisão independente antes do handoff.

## Handoff e publicação

Ao fechar uma fase: atualize o ledger, registre decisões, rode build/testes,
crie a tag e escreva `docs/handoff-<origem>-to-<destino>.md`; depois pare para
aceite. Ao publicar instalador: copie o EXE para Downloads, registre o SHA-256,
faça commit/tag e publique o EXE + SHA-256 na release. Binários ficam fora do
histórico Git; use pré-release quando a validação estiver pendente.
