# CouchPad Windows — Handoff e Bootstrap

**1.0.7 (pré-release):** oito ícones nos títulos dos cards da tela principal. Decisão D072. Build Release 0/0, 272 testes, smoke e conferência visual em simulação a 1020 DIP em pt-BR. A conferência visual desta versão cobriu esse layout.

**Aviso de atualização validado no Windows:** a 1.0.6 instalada tinha uma lista em cache anterior à publicação da 1.0.7. Após confirmar a 1.0.7 estável no GitHub, guardar o cache anterior e reabrir o App, o rodapé mostrou o botão de atualização para 1.0.7. Conferido visualmente e por UIAutomation com o canal estável e a automação desligada.

**1.0.6 (pré-release):** aviso de atualização no rodapé, com consulta pública ao GitHub e preferências de canal em Parâmetros. Decisão D071. Build Release 0/0, 272 testes e smoke aprovados. A exibição do aviso com uma release mais nova disponível ainda requer confirmação visual na versão instalada.

**P7a aceita, 13/11/2026:** a11y/i18n revisados; contraste AA, foco visível, ordem de foco alinhada à ordem visual nos 3 breakpoints (UIAutomation), nomes acessíveis e live regions nos fluxos de autostart, launcher e recuperação assistida; bandeja com itens/tooltip localizados. Seis capturas oficiais em `docs/screenshots/` (pt/en × 3 larguras, simulação) na versão 0.6.0. Build 0 avisos/erros, **259 testes**, smoke OK. Narrator/NVDA manual e interação do menu nativo da bandeja com leitor de tela permanecem pendentes; HDR/notificações adiados; reconexão do watcher ainda carece de evidência independente. Detalhes em `docs/ui-a11y-checklist.md` e `docs/screenshots/README.md`.

**Fila de validação:** `docs/validation-queue.md` — P7b aceita pelo usuário.

**P6 em andamento, 12/11/2026:** rescue separado recebeu `--startup-recovery`, com locks da sessão/watchdog, validação do journal e restauração transacional. Testes em processo separado com backend falso cobrem `pending`, `active`, falha de áudio, concorrência e interrupção seguida de nova tentativa; produção sem journal não alterou hardware. Build 0/0, 223 testes e smoke. Ainda não há tarefa no logon nem validação real após crash/reinício; journals sem baseline completo de áudio são preservados. Ver `docs/p6-recovery-matrix.md`.

**P6 sem TV, 12/11/2026:** backend de autostart por usuário e callbacks da UI foram preparados e testados com armazenamento falso. A opção continua desabilitada até coordenar o início do App com a recuperação no logon; do contrário o App poderia bloquear o rescue. A preferência anterior é preservada. Nenhuma entrada real do Registro foi alterada. Último build 0/0, 235 testes e smoke; validação de hardware e agendamento permanecem pendentes.

**Launchers 12/11/2026:** Steam e Hydra Big Picture validados na Samsung com restauração de tela/áudio ao AOC. Steam iniciado pelo CouchPad sai do Big Picture e permanece aberto por escolha do usuário. Hydra preexistente é apresentado e preservado; Hydra próprio manteve processos após saída visual. Launcher ausente foi simulado com hardware e rescue reais, com restauração confirmada, sem prova de ausência física do executável. Build 0/0, 215 testes e smoke passaram; detalhes em `docs/launcher-validation.md`. A reconexão do watcher continua sem evidência independente.

**Atualização 12/11/2026:** Ultimate 2 validou ativação, desligamento e cancelamento da ativação com restauração de tela/áudio e journal limpo. Reconexão durante atraso tem relato visual sem timestamps independentes. O usuário dispensou trabalho adicional no botão de ativação; seu teste físico não é critério pendente. Notificações nativas foram adiadas a pedido do usuário: protótipo desativado por padrão, banner não confirmado, contagem na janela preservada; auditar dependências e ausência de telemetria antes de habilitar. Ver `docs/validation-queue.md`.

**Atualização do watcher, 11/11/2026:** implementado `WindowsGamepadWatcher` read-only via XInput nos quatro slots, composto apenas com `--windows-gamepad`; simulação continua padrão, inclusive smoke/galeria. D050 documenta a escolha e limita suporte a dispositivos que exponham XInput. Build 0/0, 197 testes (14 App + 183 Core) e smoke OK. Testes rodaram fora do sandbox após falha de conexão do testhost no ambiente restrito. A matriz física do watcher ainda está pendente; launcher não iniciado.

**Watcher, 12/11/2026:** D051 acrescenta opt-in persistido e localizado para XInput, aplicado após reiniciar; build 0/0, 197 testes e smoke passaram. A UI reconheceu `XInput gamepad`. Após desconexão informada pelo usuário e interrupção da observação, probe somente leitura identificou AOC ativo e TV inativa; áudio padrão nos três papéis estava no AOC, mas journal permanecia `active` desde 07:32 com baseline AOC. Não foi comprovado que o watcher restaurou nem que o journal foi limpo. Preservar journal e investigar antes de novos testes físicos; ver `docs/validation-queue.md`.

> Documento único de contexto para iniciar o trabalho no OpenCode (DeepSeek)
> e continuar no Codex sem perder decisões, evidências ou pendências.
>
> **Como usar:** leia este arquivo inteiro antes de escrever código. Ao final de
> cada fase, atualize a seção **Ledger de status** e o **Registro de decisões**.
> Não presuma nada que não esteja aqui; se algo estiver marcado como
> "desconhecido", trate como desconhecido.

- Versão do documento: 1.1
- Data: 09/11/2026
- Diretório de trabalho: `C:\Users\Usuario\Documents\couchpad windows`
- Estado do diretório: **P0 concluído**; repositório git inicializado
- Documento canônico: `HANDOFF.md` (este arquivo). Histórico do planejamento:
  `C:\Users\Usuario\.opencode\plan\couchpad-windows-handoff.md`

---

## 1. Missão

Criar o **CouchPad para Windows**: um aplicativo de bandeja que, ao conectar um
gamepad, muda a tela para a TV, roteia o áudio para o HDMI e opcionalmente abre
um launcher; ao desconectar, restaura tela, áudio e launcher. Inclui preservação
de HDR e um componente de recuperação no logon.

**Princípios**

- 100% gratuito: **sem licença, trial, ativação, assinatura ou telemetria**.
- Nenhum contato com servidores de licença.
- Interface inspirada no ShiftPad 2.2.0 (layout e padrões visuais), **sem copiar**
  ícone, textos, imagens, XAML ou código.
- Primeiro o app funcionar com serviço simulado; depois o backend real.
- Falhar fechado: se uma capacidade de tela/áudio não for segura, não execute a
  automação; mostre o erro e preserve o estado.

**Fora de escopo**

- Licenciamento/ativação do ShiftPad.
- Copiar ou reutilizar código/assets do ShiftPad (o repositório não declara licença).
- Port para Linux nesta etapa.
- Testes destrutivos de tela/áudio sem autorização explícita.

---

## 2. Decisões já tomadas (não reabrir sem pedir)

| ID | Decisão | Justificativa |
|---|---|---|
| D001 | **Avalonia 12.1.4** com **.NET 10** (`net10.0`) | Já há prova Avalonia no projeto de referência; XAML/MVVM; Windows e futuro Linux |
| D002 | **pt-BR + inglês** desde a primeira versão | Dicionários JSON com troca em tempo real |
| D003 | App **100% free** | Sem licença/ativação/trial/telemetria |
| D004 | UI **inspirada** na referência visual, não copiada | Evitar violação de direitos e manter identidade própria |
| D005 | REA **somente estático** nesta etapa | Sem executar o ShiftPad nem gerar tráfego de ativação |
| D006 | Escopo funcional: **tela, áudio, HDR e rescue** | Licença/ativação fora do escopo |
| D007 | **Serviço simulado primeiro** | Validar UI e estados sem risco de mudar a sessão real |
| D008 | Falha de hardware = **fail-closed** | Nunca deixar a máquina sem display utilizável |
| D009 | Journal de recuperação com escrita atômica | Sobreviver a encerramento inesperado sem corromper estado |
| D010 | Nome **CouchPad**, repositório `couchpad-windows`, identidade visual original | Clareza de marca e independência do ShiftPad |
| D011 | Licença **MIT**, Copyright (c) 2026 Matheus Mello | Consistência com o projeto `couchpad` existente; permite contribuições |
| D012 | **Windows 10 1903+ e Windows 11, x64**; ARM64 via emulação no v1 | Cobertura realista e testável |
| D013 | Distribuição **self-contained, x64** | Sem dependência de runtime instalado |
| D014 | Idioma padrão = **cultura do sistema**; pt-BR + inglês; fallback **inglês** | Melhor primeira experiência e i18n |
| D015 | Config em `%LOCALAPPDATA%\CouchPad\settings.json`, journal em `state\session.json`, logs em `logs\` | Caminhos por usuário, sem admin |
| D016 | Primeiro uso com **automação desligada** e configuração mínima guiada | Evitar mudança acidental de tela/áudio |

**P0 desbloqueada em 09/11/2026** com as decisões D010–D016.

---

## 3. Ambiente (fatos verificados)

| Item | Valor |
|---|---|
| Diretório do projeto | `C:\Users\Usuario\Documents\couchpad windows` (vazio) |
| Projeto de referência | `C:\Users\Usuario\Documents\couchpad` (não modificar sem pedir) |
| SDK .NET | `C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe` — **10.0.403** |
| `dotnet` no PATH | existe, mas **sem SDK**; use o caminho absoluto acima |
| Runtimes | .NET 10.0.11 / 10.0.8 e WindowsDesktop 10.0.11 / 10.0.8 |
| Git | 2.56.0.windows.4 |
| Node (para REA) | `C:\Users\Usuario\AppData\Local\hermes\tools\node-26.12.1-win32-x64\node.exe` (npm/npx 11.20.0) |
| Node no PATH | **não**; precisa adicionar ou usar caminho absoluto |
| REA | `npx rea-agents@latest` → **6.8.0**; skill v49 com 229 ferramentas |
| Registro REA no OpenCode | **ausente** (`C:\Users\Usuario\.config\opencode\opencode.json` não existe) |
| Motores nativos | Ghidra/IDA ausentes; não são necessários para .NET gerenciado |
| Windows | build 26200 (25H2), x64 |
| ShiftPad instalado | 2.2.0 em `%LOCALAPPDATA%\Programs\ShiftPad\`; pode estar em execução (PID muda) |

**Regra:** não reiniciar, encerrar ou atualizar o ShiftPad. A análise é estática.

---

## 4. Alvo de referência: ShiftPad 2.2.0

| Artefato | Fatos |
|---|---|
| `ShiftPad.exe` | 2.2.0, 263.489.652 bytes, SHA-256 `BF67951644497A853CE3093BA72798B83B81B788F96770E7F049D1B6390CD032` |
| Formato | bundle .NET 8 auto-contido com `ShiftPad.r2r.dll` e `ShiftPad.deps.json` embutidos |
| Limite REA | `inspect-managed-artifact` recusa o EXE: snapshot máximo de 128 MiB |
| `ShiftPadRescue.exe` | 1.0.0, 11.438.995 bytes, SHA-256 `219df12da0f873baf0f3ee745ee3d9ca3d174ac12c3f2ae4a01a82a80cb4f100` |
| Rescue | **NativeAOT** (sem metadados CLI); importa `KERNEL32/ADVAPI32/ole32/OLEAUT32/USER32/SHELL32/CRT`; referencia `SetDisplayConfig` |
| Tarefa `ShiftPadRescue` | existe, `Ready`, executa o rescue no logon (Interaction/Limited) |
| Instalador 2.2.0 | `ShiftPad-Setup-2.2.0.exe`, 69.705.045 bytes, SHA-256 `0acf2a319fba3d8bf8c148b0ea365fecc3600d9714dc2bb9379639a82282d476` |
| Repositório público | commit `b86edd5d6635cddc41b8aede06c13c364e66f0f7`; projeto **GameShift 1.0.0** com placeholders |
| DLL pública | `GameShift.dll`, SHA-256 `0f198fb13f11256be4b3fb9d1e808ba38d5d83e312a95e893ccbdca697ce399b`, 31 tipos / 254 métodos |

**Conclusão:** o binário 2.2.0 é mais novo e diferente do código público. A fonte
serve como linha de base, não como verdade do produto distribuído.

---

## 5. Setup do REA (planejado, ainda não executado)

### 5.1 Node no PATH do usuário

```powershell
$node = 'C:\Users\Usuario\AppData\Local\hermes\tools\node-26.12.1-win32-x64'
$p = [Environment]::GetEnvironmentVariable('Path','User')
if (($p -split ';') -notcontains $node) {
  [Environment]::SetEnvironmentVariable('Path', ($p.TrimEnd(';') + ';' + $node), 'User')
}
```

### 5.2 Registrar o MCP no OpenCode

```powershell
npx -y rea-agents@latest setup --client opencode --yes
```

O dry-run aprovado grava `C:\Users\Usuario\.config\opencode\opencode.json` com:

```text
cmd.exe /d /c npx -y rea-agents@6.8.0 mcp
```

- Não usar `--all-detected` (tocaria Codex, VS Code, Hermes, Claude).
- Não usar `--install-hopper` (macOS).
- A skill já está alinhada (v49).

### 5.3 Verificar

```powershell
npx -y rea-agents@latest doctor --client opencode --json
Get-Content "$env:USERPROFILE\.config\opencode\opencode.json" -Raw
```

Critérios: `registration:opencode.ok = true`, `healthy = true`, arquivo com o
servidor `rea`. `environment_healthy` pode ser `false` por Ghidra/IDA — esperado.

### 5.4 Reiniciar o OpenCode

Necessário para o processo herdar o PATH novo e iniciar o MCP. Configurações do
OpenCode recarregam sozinhas, mas variáveis de ambiente do processo não.

### 5.5 Fallback sem PATH

Usar a instalação existente com caminhos absolutos:

```text
C:\Users\Usuario\Documents\couchpad\.build\rea-setup\node\node-v24.22.2-win-x64\node.exe
C:\Users\Usuario\Documents\couchpad\.build\rea-setup\installed\node_modules\rea-agents\scripts\rea.mjs
mcp
```

O `doctor` pode marcar como “stale”; funcionalmente serve.

---

## 6. Plano de análise REA (tela, áudio, HDR, rescue)

### 6.1 Preparação

1. Hash e versão dos artefatos (já registrados na seção 4).
2. Extrair o assembly gerenciado do bundle 2.2.0 para
   `analysis\shiftpad-2.2.0\` (parser local do índice embutido; **sem executar**).
3. Exportar o XML da tarefa `ShiftPadRescue`.
4. Baixar o instalador 2.2.0 somente se necessário para comportamento de instalação.

### 6.2 Perguntas

| Área | Perguntas |
|---|---|
| Tela | APIs usadas; como o TV é identificado; modos target-only/extend/clone; flags `SDC_*`; DPI/escala; rollback; falha sem TV |
| Áudio | `AudioSwitcher` ou COM direto; `IPolicyConfig`; como casa o endpoint HDMI; retry; volume/mute; restauração |
| HDR | `DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO` / `SET_ADVANCED_COLOR_STATE`; ordem; falha parcial |
| Rescue | journal (caminho/formato); gatilho real; decisão de restaurar; concorrência com o app; privilégios |

### 6.3 Ferramentas

| Etapa | Ferramenta |
|---|---|
| Identidade | `inspect_managed_artifact` (summary) |
| Tipos/métodos | `inspect_managed_members` (`*Display*`, `*Audio*`, `*Hdr*`, `*Cec*`, `*Journal*`, `*Settings*`) |
| P/Invoke | `inspect_managed_native_boundaries` |
| Strings/grafo | `user_strings`, `call_edges` |
| PE do rescue | `inspect_pe_imports`, `inspect_pe_certificates`, `inspect_pe_resources` |
| Comparação | `compare_managed_members` (2.2.0 × fonte pública) |
| Evidência | `export_evidence_bundle` |

**Regra de economia:** usar `detail: "summary"`, `filter-output` e IDs de
evidência. Nunca colar saídas completas de 263 MB no contexto.

### 6.4 Entregável

`docs\rea-shiftpad-2.2.0.md` com **observado × inferido × desconhecido**, IDs de
evidência, limitações e lacunas que exigem teste em hardware.

---

## 7. Especificação de UI

### 7.1 Referência visual

Screenshot do ShiftPad 2.2.0 fornecida em 09/11/2026: tema escuro, cards
arredondados, acento verde, toggles azuis, cabeçalho com marca e pill de estado,
grid de 3 colunas com sidebar de parâmetros e rodapé de status.

**Não copiar:** ícone do gamepad, palavra “GameShift/ShiftPad”, textos em russo,
imagens, XAML ou assets. Recriar com identidade CouchPad.

### 7.2 Wireframe lógico

```text
┌──────────────────────────────────────────────────────────────────┐
│ [logo] CouchPad            [pill: Ativo / Aguardando]            │
├──────────────────────────────────────────────────────────────────┤
│ ┌──────────────────────────────┐ ┌─────────────────────────────┐ │
│ │ AUTOMAÇÃO                    │ │ DISPLAY                     │ │
│ │ [toggle]  Ouvindo gamepad    │ │ Atual:  <monitor>           │ │
│ │ Gamepad: ...   Tela: ...     │ │ Destino: <TV>       [refresh]│ │
│ │ Áudio: ...     Launcher: ... │ │                             │ │
│ └──────────────────────────────┘ └─────────────────────────────┘ │
│ ┌──────────────┐ ┌──────────────┐ ┌─────────────────────────────┐ │
│ │ GAMEPAD      │ │ COMBO        │ │ PARÂMETROS                  │ │
│ │ principal    │ │ [1][L][R][B] │ │ Fechar launcher   [toggle]  │ │
│ └──────────────┘ └──────────────┘ │ Bandeja           [toggle]  │ │
│ ┌──────────────┐ ┌──────────────┐ │ Iniciar com Win   [toggle]  │ │
│ │ TRANSIÇÃO    │ │ LAUNCHERS    │ │ Atraso ativação   [select]  │ │
│ │ modo + áudio │ │ Steam/custom │ │ Atraso desativação[select]  │ │
│ └──────────────┘ └──────────────┘ └─────────────────────────────┘ │
├──────────────────────────────────────────────────────────────────┤
│ ● Ativo            [Redefinir] [Sobre] [pt-BR ▾]                 │
└──────────────────────────────────────────────────────────────────┘
```

### 7.3 Tokens

| Token | Valor |
|---|---|
| Fundo | `#0A0A0B` |
| Superfície | `#17181A` |
| Controle | `#1F2023` |
| Borda | `#2A2B2F` |
| Texto primário | `#F2F3F5` |
| Texto secundário | `#9AA0A6` |
| Acento | `#2FD07A` |
| Secundário (opcional) | `#2F80ED` |
| Raio card | 12–14 px |
| Raio controle | 8–10 px |
| Gap | 16 px |
| Padding | 20–24 px |
| Fonte | Segoe UI Variable / Inter |

### 7.4 Componentes

`Card`, `CardHeader`, `StatusDot`, `StatusRow`, `ToggleSwitch`, `Select`,
`IconButton`, `KeyChip`, `PathPicker`, `Pill`, `FooterLink`, `ConfirmDialog`.

Estados obrigatórios:

- Toggle: on/off/disabled.
- Select: normal/aberto/disabled/carregando.
- Status: conectado/desconectado/auto/erro.
- Card: normal/atenção/erro.
- Ações: idle/carregando/sucesso/erro.

### 7.5 Layout responsivo

- `>= 1000 px`: 3 colunas (principal 2 + sidebar 1).
- `720–999 px`: 2 colunas.
- `< 720 px`: 1 coluna.
- Janela padrão ~1020×780; mínimo `640×640` (permite alcançar 1 coluna).
- Conteúdo em `ScrollViewer`; cabeçalho e rodapé fixos.

### 7.6 i18n

- `Localization/Strings.pt-BR.json` e `Strings.en.json`.
- `LocalizationService` com troca em tempo real e persistência.
- Padrão pela cultura do sistema; fallback inglês.
- Nenhuma string hardcoded; teste de pseudo-localização.
- Seletor de idioma no rodapé/Sobre.

### 7.7 Acessibilidade

- Contraste ≥ 4.5:1 (texto) e ≥ 3:1 (componentes).
- Navegação por teclado e foco visível.
- `AutomationProperties.Name` em toggles/selects/ícones.
- DPI 100–200% sem corte.
- Alvos de toque ≥ 32 px.

---

## 8. Arquitetura proposta

```text
src/
  CouchPad.App/               # Avalonia: views, viewmodels, tema, i18n
  CouchPad.Core/              # modelos, estado, interfaces, serviços simulados
  CouchPad.Platform.Windows/  # display, áudio, HDR, rescue (fase posterior)
tests/
  CouchPad.Core.Tests/
```

**Padrões**

- MVVM com `CommunityToolkit.Mvvm`.
- Injeção de dependência simples (`Microsoft.Extensions.DependencyInjection`).
- `async`/`await` + `CancellationToken` em toda operação de hardware.
- Nenhuma chamada de hardware no construtor de ViewModel.
- Serviços atrás de interfaces; modo simulado selecionável.

**Interfaces iniciais (proposta)**

```csharp
public interface IDisplayService {
    Task<DisplaySnapshot> CaptureAsync(CancellationToken ct);
    Task<DisplayResult> ApplyAsync(DisplayTarget target, DisplayMode mode, CancellationToken ct);
    Task RestoreAsync(DisplaySnapshot snapshot, CancellationToken ct);
}

public interface IAudioService {
    Task<AudioSnapshot> CaptureAsync(CancellationToken ct);
    Task<AudioResult> SetDefaultAsync(AudioEndpoint endpoint, CancellationToken ct);
    Task RestoreAsync(AudioSnapshot snapshot, CancellationToken ct);
}

public interface IHdrService {
    Task<HdrSnapshot> CaptureAsync(CancellationToken ct);
    Task ReapplyAsync(HdrSnapshot snapshot, CancellationToken ct);
}

public interface IGamepadService {
    event EventHandler<GamepadEventArgs> Changed;
    Task<IReadOnlyList<GamepadDevice>> ListAsync(CancellationToken ct);
    GamepadDevice? Primary { get; set; }
}

public interface ILauncherService {
    Task<LaunchResult> LaunchAsync(LauncherTarget target, CancellationToken ct);
    Task CloseAsync(LauncherTarget target, CancellationToken ct);
}

public interface IRescueStore {
    Task SaveAsync(SessionJournal journal, CancellationToken ct);
    Task<SessionJournal?> LoadAsync(CancellationToken ct);
    Task ClearAsync(CancellationToken ct);
}
```

**Journal (proposta, não copiada)**

- `%LOCALAPPDATA%\CouchPad\state\session.json`
- Escrita atômica (temp + `File.Replace`).
- Campo `schemaVersion`; nunca apagar campos desconhecidos.
- Conteúdo: topologia anterior, HDR, endpoint/volume/mute, launcher próprio
  (PID + boot ID + hora de início), timestamp e fase da operação.
- O rescue lê no logon e restaura somente se o journal indicar sessão pendente.

> O caminho/formato reais do ShiftPad serão confirmados pelo REA; esta proposta
> é design do CouchPad, não fato observado.

---

## 9. Fases e critérios de aceite

| Fase | Entrega | Pronto quando |
|---|---|---|
| P0 | Repositório, solução, projetos, tema, i18n, `AGENTS.md` | `dotnet build` passa; app abre vazio; troca de idioma funciona |
| P1 | Shell estático com as 8 áreas da UI | Layout responsivo nos 3 breakpoints; sem hardware |
| P2 | Componentes reutilizáveis e estados | Toggle/select/status/card com todos os estados |
| P3 | Serviço simulado + fluxo de ativação | Conectar/desconectar simulado muda estados; atrasos cancelam |
| P4 | Display real | Captura/aplica/restaura com rollback; testes em hardware |
| P5 | Áudio + HDR | Endpoint correto, retry, volume/mute, HDR reaplicado |
| P6 | Rescue + bandeja + autostart | Journal atômico; rescue no logon; sem admin desnecessário |
| P7a | Docs, i18n, a11y, screenshots, release notes | Checklist de a11y; screenshots nos 3 breakpoints × 2 idiomas |
| P7b | Correções finais, instalador, release | Regressão; instalador definido; tag v1.0.0 |

**Não avançar de fase sem aceite explícito do usuário.**

---

## 10. Comandos

```powershell
# SDK correto (o dotnet do PATH não tem SDK)
$env:DOTNET = 'C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe'

# Na raiz do projeto
& $env:DOTNET new sln -n CouchPad
& $env:DOTNET new classlib -n CouchPad.Core -o src/CouchPad.Core -f net10.0
& $env:DOTNET new xunit -n CouchPad.Core.Tests -o tests/CouchPad.Core.Tests -f net10.0
# App Avalonia: usar template oficial 12.1.4 ou criar csproj manualmente
& $env:DOTNET build
& $env:DOTNET test
```

**Pacotes de referência (fixar versões)**

- `Avalonia` 12.1.4
- `Avalonia.Desktop` 12.1.4
- `Avalonia.Themes.Fluent` 12.1.4
- `Avalonia.Diagnostics` 12.1.4 (Debug)
- `CommunityToolkit.Mvvm` (última 8.x)
- `Microsoft.Extensions.DependencyInjection` (opcional)
- `xunit` + `Microsoft.NET.Test.Sdk`

Se o template `avalonia.mvvm` não existir, criar o csproj manualmente; não
instalar SDKs ou runtimes adicionais sem aprovação.

---

## 11. Convenções

- C# com `Nullable` e `ImplicitUsings` habilitados.
- Um commit por fase; mensagens claras.
- Sem lógica de negócio em code-behind.
- Sem `Thread.Sleep`; usar `Task.Delay` + `CancellationToken`.
- Logs sem PII, sem tokens, sem caminhos de usuário em excesso.
- Toda ação de hardware deve ter caminho de falha e rollback.
- Não alterar tela/áudio durante P0–P3.
- Testes unitários para lógica pura (journal, timers, seleção de endpoint).
- Strings sempre via i18n.

---

## 12. Custos e cache (DeepSeek)

| Modelo | Entrada | Saída | Cache read |
|---|---:|---:|---:|
| DeepSeek V4.1 Flash | US$ 0,15 / 1M | US$ 0,60 / 1M | US$ 0,003 / 1M |
| DeepSeek V4 Pro | US$ 0,66 / 1M | US$ 1,98 / 1M | US$ 0,022 / 1M |

- Cache do DeepSeek é **automático**; não pedir no prompt.
- Sessão estável + contexto append-only = mais cache hit.
- Não mudar ferramentas/MCP no meio da sessão.
- Evitar colar evidência REA gigante; usar resumos e IDs.
- Estimativa do app completo: 14–36M tokens de entrada, 0,6–1,2M de saída
  (~US$ 1–4 no Flash com cache).
- Para análise pontual do REA, o CLI pode ser mais econômico que expor 229
  ferramentas MCP no contexto.

---

## 13. Protocolo de handoff OpenCode ↔ Codex

### 13.1 Antes de codar, ler

1. Este documento.
2. `AGENTS.md` do projeto (criar na P0).
3. `docs/ui-spec.md` (detalhamento visual).
4. `docs/rea-shiftpad-2.2.0.md` (quando existir).
5. `docs/decisions.md` (registro de decisões).

### 13.2 Atualizar sempre

- **Ledger de status** (seção 14).
- **Registro de decisões** (seção 15).
- **Pendências/desconhecidos** (seção 16).

### 13.3 Regras de ouro

- Não reabrir decisões D001–D009 sem pedido explícito.
- Não copiar código/assets do ShiftPad.
- Não implementar licença/ativação.
- Não executar o ShiftPad.
- Não alterar tela/áudio reais antes da P4.
- Se faltar contexto, ler os arquivos acima antes de perguntar.
- Se uma decisão nova for necessária, registrar e pedir confirmação.

### 13.4 Prompt para o OpenCode (início)

```text
Leia C:\Users\Usuario\.opencode\plan\couchpad-windows-handoff.md por completo.
Trabalhe na fase P0: criar o repositório git, a solução .NET 10, os projetos
CouchPad.App (Avalonia 12.1.4), CouchPad.Core e CouchPad.Core.Tests, o tema
escuro com os tokens da seção 7.3, o serviço de i18n pt-BR/en e o AGENTS.md.
Use o SDK em C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe.
Não implemente display/áudio/HDR ainda. Sua faixa é P0–P3 e P7a: UI, tema,
i18n, componentes, simulação e documentação. Não escreva P/Invoke, COM interop,
SetDisplayConfig, Core Audio, Task Scheduler nem nada em
src/CouchPad.Platform.Windows. Ao terminar, rode build e testes, gere os
screenshots, escreva docs/handoff-deepseek-to-codex.md, atualize o ledger e pare
para revisão.
```

### 13.5 Prompt para o Codex (continuação)

```text
Você está continuando o projeto CouchPad Windows. Leia primeiro:
- C:\Users\Usuario\.opencode\plan\couchpad-windows-handoff.md
- AGENTS.md
- docs/ui-spec.md
- docs/rea-shiftpad-2.2.0.md (se existir)
- docs/decisions.md
Verifique o ledger de status. Sua faixa é REA 2.2.0 + P4–P6 + P7b. Não reabra
P0–P3 sem necessidade; se precisar mudar uma interface do Core, documente em
docs/decisions.md e mantenha a UI compilando. Não reabra as decisões D001–D009,
não copie código/assets do ShiftPad e não implemente licença/ativação. Antes de
mudar tela/áudio reais, confirme a fase e o aceite. Ao final, escreva
docs/handoff-codex-to-deepseek.md, atualize o ledger e o registro de decisões.
```

### 13.6 Divisão de trabalho: DeepSeek/OpenCode × Codex

**Princípio:** o DeepSeek fica com as fases baratas, bem especificadas e sem
hardware. O Codex fica com as fases caras, exploratórias e de depuração em
hardware.

| Fase | Dono | Tokens aproximados | Por quê |
|---|---|---:|---|
| Setup REA + verificação | Codex | 1–2M | Pré-requisito do REA |
| REA 2.2.0 (tela/áudio/HDR/rescue) | **Codex** | 2–5M | Evidência grande, muitos turnos |
| P0 bootstrap | **DeepSeek** | 0,5–1M | Estrutura previsível |
| P1 shell Avalonia | **DeepSeek** | 2–4M | Muito código, pouca depuração |
| P2 componentes | **DeepSeek** | 1,5–3M | Especificado por tokens/estados |
| P3 serviço simulado | **DeepSeek** | 1,5–3M | Fakes e testes puros |
| P4 display real | **Codex** | 4–9M | Hardware, rollback, logs |
| P5 áudio + HDR | **Codex** | 5–10M | COM, timing, falhas silenciosas |
| P6 rescue/bandeja/autostart | **Codex** | 3–6M | NativeAOT, Task Scheduler |
| P7a docs/a11y/screenshots | **DeepSeek** | 1–2M | Escrita e revisão |
| P7b correções/release | **Codex** | 3–6M | Regressão e empacotamento |

**Distribuição estimada**

- DeepSeek: **~30–35%** dos tokens (P0–P3 + P7a)
- Codex: **~65–70%** dos tokens (REA + P4–P6 + P7b)

#### Contrato de fronteira

**DeepSeek pode escrever:**

- `src/CouchPad.App/**`
- `src/CouchPad.Core/**` (modelos, interfaces, serviços simulados)
- `tests/CouchPad.Core.Tests/**`
- `docs/ui-spec.md`, `docs/ui-a11y-checklist.md`
- `Localization/**`, `Themes/**`
- `AGENTS.md`, `HANDOFF.md` (atualizações de status)

**DeepSeek NÃO pode escrever:**

- P/Invoke, COM interop, `SetDisplayConfig`, Core Audio, Task Scheduler
- `src/CouchPad.Platform.Windows/**`
- Qualquer código que altere tela, áudio, HDR ou execute o rescue
- Instalador, assinatura, CI de release

**Codex pode escrever:**

- `src/CouchPad.Platform.Windows/**`
- `analysis/**`, `docs/rea-shiftpad-2.2.0.md`
- Testes de integração/hardware
- Instalador, empacotamento, CI de release
- Extensões compatíveis em `CouchPad.Core` (documentadas)

**Regra de ouro:** se o Codex precisar mudar uma interface do Core, ele
documenta a mudança em `docs/decisions.md` e mantém a UI compilando. Se a UI
precisar de um estado novo, o DeepSeek o adiciona primeiro no modelo/simulador.

#### Sequência recomendada

```text
1. DeepSeek: P0 → P1 → P2 → P3
   └─ tag v0.3.0-sim + docs/handoff-deepseek-to-codex.md

2. Codex: REA 2.2.0 → P4 → P5 → P6
   └─ tag v0.6.0-hardware + docs/handoff-codex-to-deepseek.md

3. DeepSeek: P7a (docs, i18n, a11y, screenshots)
   └─ tag v0.9.0-rc

4. Codex: P7b (correções finais, instalador, release)
   └─ tag v1.0.0
```

**Opcional (paralelo):** o Codex faz o REA enquanto o DeepSeek faz P0–P3. Nesse
caso, o Codex trabalha só em `analysis/` e `docs/rea-*`, sem tocar no app, e o
DeepSeek segue em `main`. O merge é só de documentação; o código de hardware
começa depois da P3.

#### Artefatos de handoff

**DeepSeek → Codex:** `docs/handoff-deepseek-to-codex.md`

- tag/commit exato
- como rodar build/test/app
- screenshots nos 3 breakpoints × 2 idiomas
- o que é real × simulado
- contratos das interfaces e fakes
- TODOs com caminho de arquivo
- perguntas abertas para o REA

**Codex → DeepSeek:** `docs/handoff-codex-to-deepseek.md`

- comportamento real observado
- nomes de estados e códigos de erro
- ajustes de UI necessários
- limitações e riscos
- o que ainda é desconhecido

#### Orçamento e regra de parada

| Agente | Entrada | Saída |
|---|---:|---:|
| DeepSeek | 8–14M | 0,4–0,7M |
| Codex | 20–32M | 0,8–1,2M |

- Se uma fase estourar **1,5×** o orçamento, parar e escrever um diagnóstico
  curto: o que foi tentado, qual log falta, qual decisão está bloqueada.
- Não iniciar P4 antes do aceite da P3.
- Não iniciar P7b antes do aceite da P7a.

#### Vantagem do split

- O DeepSeek **não precisa do MCP do REA**: evita o custo de schema de 229
  ferramentas e o setup do Node.
- O Codex concentra o que realmente exige iteração: hardware, REA e release.
- O handoff em arquivos mantém o contexto entre agentes sem depender da
  conversa.

### 13.7 Roster de agentes e modelos

Arquivos instalados no projeto:

- `.opencode/agents/` — 6 agentes Markdown para DeepSeek.
- `docs/agents/codex/roles/` — 4 papéis Markdown para Codex.
- `C:\Users\Usuario\.codex\*.config.toml` — 4 perfis Codex.
- `.opencode/skills/couchpad-*` — 5 skills detectadas pelo OpenCode.

| Agente | Plano | Modelo | Effort | Tarefa |
|---|---|---|---|---|
| `orchestrator` | OpenCode | `deepseek-flash` | `low` | planeja, delega, integra |
| `scaffolder` | OpenCode | `deepseek-flash` | `none` | repo, solução, tema, i18n |
| `ui-builder` | OpenCode | `deepseek-flash` | `low` | views, componentes, estados |
| `sim-engineer` | OpenCode | `deepseek-flash` | `high` | fakes, state machine, testes |
| `reviewer` | OpenCode | `deepseek-flash` | `high` | revisão somente leitura |
| `docs-writer` | OpenCode | `deepseek-flash` | `none` | docs, textos, screenshots |
| `rea` | Codex | `gpt-6.1-terra` | `high` | REA 2.2.0 |
| `hardware` | Codex | `gpt-6.1-terra` | `high` | P4–P6 |
| `release` | Codex | `gpt-6.1-terra` | `medium` | P7b |
| `review` | Codex | `gpt-6.1-terra` | `medium` | revisão independente |

**Instalação (feita no P0)**

- OpenCode: `.opencode/agents/*.md` (6 agentes).
- Codex: perfis em `C:\Users\Usuario\.codex\*.config.toml` e papéis em
  `docs/agents/codex/roles/`.
- Codex: `codex -p rea`, `codex -p hardware`, etc.
- Skills: `.opencode/skills/couchpad-*`.

**Economia no DeepSeek**

- `orchestrator` em `low`; `scaffolder`/`docs-writer` em `none`;
  `ui-builder` em `low`; `sim-engineer`/`reviewer` em `high`.
- `max` não é usado.
- `steps` limita loops.
- Um subagente por vez quando houver dependência.
- Brief curto com caminhos e critério de aceite.

**Observação:** o catálogo do OpenCode não expõe provider OpenAI nesta máquina,
então `gpt-6.1-terra` não roda dentro do OpenCode. Os dois planos permanecem
separados.

---

## 14. Ledger de status

| Fase | Dono | Status | Evidência/observação |
|---|---|---|---|
| Setup REA | Codex | **pendente** | Planejado; nada gravado. Ver seção 5 |
| REA 2.2.0 | Codex | **pendente** | Depende do setup e da extração do bundle |
| P0 bootstrap | DeepSeek | **concluído** | build 0 avisos/0 erros; 26 testes; smoke pt/en; revisão aplicada; ver seção 19 |
| P1 shell | DeepSeek | **concluído** | tag `v0.2.0-shell`; build 0 avisos/0 erros; 29 testes; smoke 1020×780 pt/en; capturas 3/2/1 colunas; ver seção 20 |
| P2 componentes | DeepSeek | **concluído** | tag `v0.3.0-components`; build 0 avisos/0 erros; 34 testes; galeria pt/en; shell 3/2/1 colunas; ver seção 21 |
| P3 simulação | DeepSeek | **aceito** | tag `v0.4.0-sim`; contrato 0.4.2 no HEAD; build 0 avisos/0 erros; 66 testes; fluxo simulado idle→pending→active→restored; journal e persistência; aceite do usuário em 09/11/2026; ver seção 22 |
| P4 display | Codex | **aceito com limitação** | TvOnly/Extend, rollback, falha/cancelamento, desconexão e kill após retorno do CCD; morte dentro de `SetDisplayConfig` não comprovada; aceite do usuário em 11/11/2026; matriz em `docs/p4-hardware-matrix.md` |
| P5 áudio/HDR | Codex | **aceito** | Áudio validado em hardware com controle simulado, restore, falha, cancelamento, endpoint ausente e rescue; HDR adiado; aceite do usuário em 11/11/2026; ver `docs/p5-audio-hdr.md` |
| P6 rescue/tray | Codex | aceita em 13/11/2026 | Bandeja, logon com e sem journal, recuperação assistida e autostart validados. As duas tarefas retornaram 0 no logon final. Build 0/0, 259 testes, smoke e testes de processo passaram. Tag `v0.6.0-hardware`. Handoff P7a em `docs/handoff-codex-to-deepseek.md`. |
| P7a docs/a11y | DeepSeek | **aceita** | A11y/i18n/UIAutomation, 6 capturas 0.6.0, README/notas; aceite do usuário em 13/11/2026; Narrator/NVDA manual pendente; ver seção P7a |
| P7b corrections/release | Codex | aceita pelo usuário | Instalador autocontido, primeiro uso, bandeja, autostart, reinstalação e remoção em conta padrão. Build Debug/Release 0/0, 259 testes por configuração e smoke. Tag `v1.0.0`. Ver `docs/install-validation.md` e D066–D067. |

---

## 15. Registro de decisões

| Data | ID | Decisão | Motivo |
|---|---|---|---|
| 09/11/2026 | D001 | Avalonia 12.1.4 / net10.0 | Prova existente; XAML/MVVM |
| 09/11/2026 | D002 | pt-BR + inglês | Público-alvo e i18n |
| 09/11/2026 | D003 | 100% free | Sem licença/ativação |
| 09/11/2026 | D004 | UI inspirada, não copiada | Direitos autorais |
| 09/11/2026 | D005 | REA estático | Segurança e escopo |
| 09/11/2026 | D006 | Tela/áudio/HDR/rescue | Foco funcional |
| 09/11/2026 | D007 | Simulação primeiro | Evitar risco de hardware |
| 09/11/2026 | D008 | Fail-closed | Não deixar a máquina sem display |
| 09/11/2026 | D009 | Journal atômico | Recuperação confiável |
| 09/11/2026 | D010 | Nome CouchPad, repo `couchpad-windows` | Marca própria e independência |
| 09/11/2026 | D011 | Licença MIT | Consistência com o projeto `couchpad` |
| 09/11/2026 | D012 | Windows 10 1903+/11, x64; ARM64 por emulação | Cobertura testável |
| 09/11/2026 | D013 | Self-contained, x64 | Sem dependência de runtime |
| 09/11/2026 | D014 | Cultura do sistema; pt-BR + inglês; fallback inglês | Primeira experiência |
| 09/11/2026 | D015 | Config/journal/logs em `%LOCALAPPDATA%\CouchPad` | Per-user, sem admin |
| 09/11/2026 | D016 | Automação desligada no primeiro uso | Evitar mudança acidental |
| 09/11/2026 | D017 | Layout responsivo em code-behind, 3 breakpoints, mínimo 640×640 | Alcançar 1 coluna sem duplicar XAML |
| 09/11/2026 | D018 | P1 somente UI; refresh de displays é placeholder de 700 ms | Validar shell sem hardware |
| 09/11/2026 | D019 | Até glifos/unidades vêm dos catálogos; teste bloqueia literais | Nenhuma string fora dos dicionários |
| 09/11/2026 | D020 | Componentes com `ControlTheme` próprio e galeria `--gallery` | Reuso e validação sem hardware |
| 09/11/2026 | D021 | `ActionButton` com Variant/State; foco `TextPrimary` 2 px; hover/pressed primários distintos | Estados visíveis e acessíveis |
| 09/11/2026 | D022 | Classes loading desabilitam Button/ComboBox; shell desabilita refresh durante atualização | Bloquear duplo acionamento |
| 09/11/2026 | D023 | Folder picker por evento; view usa `StorageProvider` | Manter diálogo fora do VM/Core |
| 09/11/2026 | D024 | Display/áudio/gamepad simulados atrás de interfaces; atraso injetável | Testar sem hardware |
| 09/11/2026 | D025 | State machine Idle/Waiting/Pending/Applying/Active/Error com CTS e gate | Cancelar atrasos e evitar concorrência |
| 09/11/2026 | D026 | Snapshot separa baseline do modo aplicado; journal grava baseline | Fail-closed e rescue P6 |
| 09/11/2026 | D027 | AppSettings persiste toggles, atrasos, modos, launcher e caminho | Preferências sobrevivem ao restart |
| 09/11/2026 | D028 | UI P3 simula conectar/desconectar gamepad | Validar state machine sem risco |
| 09/11/2026 | D029 | `DisplayState.Topology` opaco e journal `pending` antes do apply; falha inicial aborta | Fechar janela de crash e permitir rescue fiel |
| 09/11/2026 | D030 | Suprimir só `NU1900` quando a auditoria NuGet estiver offline | Build 0 avisos sem esconder vulnerabilidades |
| 10/11/2026 | D031 | `DisplayMode.InternalOnly` para baseline sem TV; `Mode` é resumo; `Topology` é a fonte de restore | Mapear o estado real sem inferir TV pela ordem |
| 10/11/2026 | D032 | `IRescueGuard`/`IRescueLease` no Core; begin/commit/abort/abandon; `NoopRescueGuard` na simulação | Watchdog independente sem acoplar Core à plataforma |
| 10/11/2026 | D033 | App referencia `Platform.Windows` só na raiz de composição; serviço real opt-in | UI testável e padrão seguro |
| 10/11/2026 | D034 | Perda do watchdog antes da liberação marca `recoveryUncertain` e bloqueia mutações até reiniciar | Não mudar sem saber o estado |
| 10/11/2026 | D035 | Seletores de TV/monitor + `AllowDisplayWrites` padrão falso; App compõe real só com opt-in e seletores; banner de recuperação incerta | Matriz configurável sem escrita acidental |

---

## 16. Pendências e desconhecidos

### 16.1 P0 — confirmado

Nenhuma pendência bloqueadora. D010–D016 confirmadas em 09/11/2026.

### 16.2 Antes de P4/P5 — display, áudio e HDR

| # | Pendência | Dono | Como resolver / recomendação |
|---|---|---|---|
| 1 | Identidade do TV (EDID, target path, nome) | Codex | REA + teste; preferir target path + EDID, fallback nome |
| 2 | Modos de display suportados | Produto | Confirmar target-only, estender e espelhar; padrão target-only |
| 3 | API de display | Codex | REA; preferir CCD (`QueryDisplayConfig`/`SetDisplayConfig`) |
| 4 | API de áudio e endpoint padrão | Codex | REA; documented APIs primeiro, `IPolicyConfig` como fallback isolado |
| 5 | Política de HDR | Codex | REA + Win32; capturar/reaplicar; não forçar se não suportar |
| 6 | Acesso a hardware de teste | Usuário | Confirmar TV, HDMI, controle e monitor alternativo |
| 7 | Privilégios necessários | Codex | Per-user por padrão; admin só se inevitável |
| 8 | Matriz mínima de teste | Codex | `couchpad-hardware/references/test-matrix.md` |

### 16.3 Antes de P6 — rescue, bandeja e autostart

| # | Pendência | Dono | Como resolver / recomendação |
|---|---|---|---|
| 9 | Rescue separado ou flag no app | Codex | `CouchPadRescue.exe` separado, NativeAOT |
| 10 | Formato/caminho do journal do CouchPad | Codex | JSON versionado, escrita atômica; REA informa o do ShiftPad |
| 11 | Gatilho e atraso da tarefa | Codex | Task Scheduler no logon, Interactive/Limited |
| 12 | Lock, concorrência e anti-loop | Codex | Lock entre app/rescue; limite de tentativas |
| 13 | Bandeja e autostart | Codex | Prova técnica na P6; registrar decisão |
| 14 | Instância única e IPC | Codex | Mutex + named pipe |

### 16.4 Antes de P7 — release

| # | Pendência | Dono | Como resolver / recomendação |
|---|---|---|---|
| 15 | Instalador | Codex | Inno Setup per-user; MSIX/WiX só se necessário |
| 16 | Assinatura | Usuário | Certificado, self-signed ou sem assinatura documentada |
| 17 | Atualização | Produto | Manual no v1; winget/auto-update depois |
| 18 | Canal de release e suporte | Usuário | GitHub Releases + README |
| 19 | Licenças de dependências | Codex | `NOTICE` com Avalonia/MIT, CommunityToolkit, ícones |
| 20 | Definição de v1.0 | Produto | Matriz mínima + instalador + rescue + docs |
| 21 | CI | Codex | GitHub Actions `windows-latest`; build/test sem hardware |
| 22 | Crash reporting | Produto | Sem telemetria; dump local opcional e explícito |
| 23 | Metas de performance | Produto | Startup, memória e CPU ociosa; definir antes da P7 |

### 16.5 Pode esperar

- Tema claro.
- Mais idiomas além de pt-BR/en.
- Modo portátil.
- CLI de controle.
- Auto-update.
- Winget.
- ARM64 nativo.
- Estatísticas locais (sem telemetria).

### 16.6 Riscos sem dono

- Acesso a hardware de teste (TV/HDMI/controle).
- Custo e decisão do certificado de assinatura.
- Disciplina clean-room: comportamento e APIs públicas, nunca código do ShiftPad.
- Escopo do rescue: só tela ou também áudio/HDR/launcher.

### 16.7 Desconhecidos que o REA deve responder

1. Como o ShiftPad identifica o TV.
2. Como define o endpoint padrão de áudio.
3. Como representa e reaplica HDR.
4. Formato/caminho do journal do ShiftPad.
5. Gatilho exato da tarefa de rescue.
6. Suporte real a clone/estender.
7. Diferenças entre a fonte pública e o binário 2.2.0.

---

## 17. Definição de pronto (por fase)

Uma fase só termina quando:

1. `dotnet build` e `dotnet test` passam.
2. Critérios de aceite da seção 9 foram atendidos.
3. Ledger e decisões foram atualizados.
4. Nenhuma ação de hardware fora do escopo foi executada.
5. O usuário revisou e aprovou.

---

## 18. Aviso final

Este documento descreve **comportamento observado e decisões de projeto**, não
código do ShiftPad. O repositório público não declara licença; não copie código,
ícones ou textos. A implementação do CouchPad deve ser original, baseada em APIs
públicas do Windows e em evidências estáticas do REA.

---

## 19. Evidência do P0 (09/11/2026)

### Entregue

- Solução `CouchPad.slnx` com 4 projetos.
- `src/CouchPad.App`: Avalonia 12.1.4, tema escuro, i18n, shell inicial.
- `src/CouchPad.Core`: `AppLanguage`, `AppPaths`, `AppSettings`,
  `JsonAppSettingsStore`.
- `src/CouchPad.Platform.Windows`: fronteira vazia, sem implementação.
- `tests/CouchPad.Core.Tests`: 26 testes.

### Verificação

| Item | Resultado |
|---|---|
| `dotnet build CouchPad.slnx -c Debug` | 0 avisos, 0 erros |
| `dotnet test CouchPad.slnx -c Debug` | 26 aprovados, 0 falhas |
| `dotnet CouchPad.dll --smoke` | janela 1020×780, pt-BR inicial, alternância pt/en, título CouchPad |
| Skills | 5 detectadas em `.opencode/skills/` |
| Agentes OpenCode | 6 em `.opencode/agents/` |
| Codex `-p rea` | `gpt-6.1-terra`, effort `high`, resposta OK |
| Bindings compilados | `AvaloniaUseCompiledBindingsByDefault=true`, 0 avisos |

### Revisão independente

O agente `reviewer` revisou o commit P0 e apontou 10 itens. Aplicados:

1. `AppSettings` preserva chaves desconhecidas via `[JsonExtensionData]`.
2. Settings aceita vírgula final e comentários; JSON corrompido gera `.bak`.
3. Removido o estilo global de `TextBlock`; herança de `disabled`/seleção intacta.
4. `AccentMuted` escurecido para `#123D28` (contraste AA com `Accent`).
5. Seletor de idioma com rótulo visível e estado ativo.
6. Marca `C` movida para a chave `app.mark`.
7. Teste de paridade pt-BR/en e de placeholder.
8. `MinHeight` reduzido para 640.
9. Bindings compilados ativados.
10. Versão lida de `AssemblyInformationalVersion`.

### Referências do repositório

- `AGENTS.md` — regras sempre ativas.
- `README.md` — visão, build e escopo.
- `docs/decisions.md` — D001–D016.
- `LICENSE` — MIT.

### Pendências para P1

1. Header, cards, footer e layout responsivo.
2. Componentes do catálogo (`Card`, `Pill`, `StatusDot`, etc.).
3. Spec visual em `docs/ui-spec.md`.
4. Screenshots dos 3 breakpoints × 2 idiomas.

---

## 20. Evidência do P1 (09/11/2026)

- Tag: `v0.2.0-shell`

### Entregue

- Shell estático com cabeçalho, 7 cards e rodapé (8 áreas).
- `MainWindowViewModel` com estado dos toggles, selects, atrasos, modos,
  caminho do launcher, comandos de refresh/reset e seletor de idioma.
- `MainWindow.axaml` com os cards Automação, Display, Gamepad, Combinação,
  Parâmetros, Transição e Launchers.
- Layout responsivo em 3/2/1 colunas (`MainWindow.axaml.cs`), com
  `ScrollViewer` e janela mínima `640×640`.
- `AboutWindow` modal com versão, licença e aviso 100% gratuito.
- Catálogo pt-BR/en com 78 chaves; chips e unidades também localizados.
- `Controls.axaml`: cards, pills, dots, chips, path box, botões, select e
  toggle switch com estados.
- `docs/ui-spec.md` com breakpoints, áreas, tokens, estados, i18n e a11y.
- `UiLocalizationTests`: chaves do XAML existem nos dois catálogos; nenhum
  texto visível literal; as 7 seções aparecem na janela principal.
- `docs/handoff-deepseek-to-deepseek-p1.md` com contratos e TODOs da P2.

### Verificação

| Item | Resultado |
|---|---|
| `dotnet build CouchPad.slnx -c Debug` | 0 avisos, 0 erros |
| `dotnet test CouchPad.slnx -c Debug` | 29 aprovados, 0 falhas |
| `dotnet CouchPad.dll --smoke` | janela 1020×780; pt `Ouvindo gamepad`; en `Listening for gamepad`; título CouchPad |
| Layout 3 colunas | 1020×780, sem rolagem |
| Layout 2 colunas | 860×780, rolagem vertical |
| Layout 1 coluna | 660×780, rolagem vertical |
| Idioma | troca em tempo real; estado `Aguardando gamepad` em âmbar |
| Sobre | diálogo modal abre e fecha; `Esc` fecha |

### Capturas de revisão (fora do repositório)

`C:\Users\Usuario\AppData\Local\Temp\opencode\p1-3col.png`,
`p1-2col.png`, `p1-1col.png`, `p1-en.png`, `p1-en-waiting.png`,
`p1-screen.png`. As oficiais entram em `docs/screenshots/` na P7a.

### Decisões da fase

- D017: layout responsivo em code-behind; mínimo `640×640`.
- D018: P1 somente UI; refresh de displays é placeholder de 700 ms.
- D019: glifos/unidades também vêm dos catálogos; teste bloqueia literais.

### Fora do escopo (por desenho)

- Detecção real de gamepad e captura de combinação (P3).
- Persistência dos toggles e atrasos (P3).
- Leitura/aplicação de display, áudio e HDR (P4/P5).
- Bandeja, autostart, rescue e tarefa (P6).
- Screenshots oficiais versionadas (P7a).

---

## 21. Evidência do P2 (09/11/2026)

- Tag: `v0.3.0-components`

### Entregue

- Controles próprios em `src/CouchPad.App/Controls/`:
  `Card`, `StatusPill`, `StatusRow`, `KeyChip`, `PathPicker` e
  `ActionButton`.
- `Themes/Components.axaml` com `ControlTheme` implícito para cada controle,
  mesclado em `App.axaml`.
- `MainWindow.axaml` refatorado: os 7 cards agora usam `controls:Card`; pills,
  linhas de status, chips e o seletor de pasta usam os controles compartilhados.
- `ActionButton` com `Variant` (Primary/Secondary) e `State`
  (Idle/Loading/Success/Error); foco em `TextPrimary` 2 px; hover/pressed
  primários com `AccentMutedHover`/`AccentMutedPressed`.
- Botão de atualizar displays exercita `State=Loading` durante o placeholder
  de 700 ms.
- Seletor de pasta via `ChooseLauncherFolderCommand` + evento
  `FolderPickerRequested`; a janela abre `StorageProvider.OpenFolderPickerAsync`
  e trata cancelamento/indisponibilidade.
- `ComponentGalleryWindow` acessível por `--gallery`, com todos os controles e
  estados, em pt-BR e inglês.
- Catálogos com 116 chaves em paridade; `footer.phase` = `P2 · componentes` /
  `P2 · components`.
- `docs/ui-components.md` com catálogo, matriz de estados, tokens, a11y e
  limites.
- `UiLocalizationTests` ampliado para composição do shell, temas, estados da
  galeria e ausência de literais no tema.

### Verificação

| Item | Resultado |
|---|---|
| `dotnet build CouchPad.slnx -c Debug` | 0 avisos, 0 erros |
| `dotnet test CouchPad.slnx -c Debug` | 34 aprovados, 0 falhas |
| `dotnet CouchPad.dll --smoke` | janela configurada 1020×780; limitada a 1020×699 na tela 720p de teste; pt `Ouvindo gamepad`; en `Listening for gamepad`; título CouchPad |
| `dotnet CouchPad.dll --gallery` | galeria configurada 1100×720, limitada pela área de trabalho em telas 720p; pt/en; todos os estados |
| Layout 3 colunas | 1020×780 |
| Layout 2 colunas | 780×780 |
| Layout 1 coluna | 640×780 |
| Foco | borda `TextPrimary` 2 px no ActionButton; anel de acento no toggle |
| Loading | barra indeterminada + `IsEnabled=false` no refresh |
| Fronteira | nenhum P/Invoke/COM/hardware no App |

### Capturas de revisão (fora do repositório)

`C:\Users\Usuario\AppData\Local\Temp\opencode\p2-gallery-pt.png`,
`p2-gallery-en.png`, `p2-main-pt.png`, `p2-main-en.png`,
`p2-main-pt-2col.png`, `p2-main-pt-640.png`, `p2-refresh-loading.png`. As
oficiais entram em `docs/screenshots/` na P7a.

### Revisão independente

O agente `reviewer` revisou a P2. Aplicados:

1. Foco do `ActionButton` primário trocado de acento para `TextPrimary` 2 px.
2. Hover/pressed primários com `AccentMutedHover` e `AccentMutedPressed`.
3. `Button.loading` e `ComboBox.loading` passam a definir `IsEnabled=false`.
4. `State` do botão de refresh ligado a `DisplayRefreshState`.
5. Texto de erro do select com chave dedicada.
6. Comentário com encoding corrigido (`Botões`).
7. Título do `Card` com `TextWrapping`.
8. Exemplo de foco da galeria no variant primário (pior caso).
9. Seletores explícitos `:loading:disabled` no `ActionButton` e em
   `Button.loading`.
10. Teste de classes removidas estendido a `AboutWindow` e à galeria.

### Decisões da fase

- D020: componentes com `ControlTheme` próprio e galeria `--gallery`.
- D021: `ActionButton` com Variant/State, foco distinto e hover/pressed.
- D022: classes loading desabilitam Button/ComboBox; shell desabilita refresh.
- D023: folder picker por evento; view usa `StorageProvider`.

### Fora do escopo (por desenho)

- Persistência dos toggles/atrasos e fluxo de ativação (P3).
- Leitura/aplicação real de display, áudio e HDR (P4/P5).
- Bandeja, autostart, rescue e tarefa (P6).
- Screenshots oficiais versionadas (P7a).

---

## 22. Evidência do P3 (09/11/2026)

- Tag: `v0.4.0-sim`
- Aceite do usuário: 09/11/2026

### Entregue

- Core de simulação:
  - `IGamepadWatcher` / `ISimulatedGamepad` / `SimulatedGamepadWatcher`;
  - `IDisplayService` / `SimulatedDisplayService`;
  - `IAudioService` / `SimulatedAudioService`;
  - `IDelayScheduler` / `SystemDelayScheduler`.
- State machine `AutomationCoordinator` com `Idle`, `WaitingForGamepad`,
  `Pending`, `Applying`, `Active`, `Error` (`RescuePending` reservado).
  CTS + `SemaphoreSlim` cancelam e serializam transições.
- Atrasos de 0/1/2/3/5 s (`DelayOptions`) com cancelamento ao desconectar,
  desligar ou reconectar.
- Snapshot separa o **baseline de restauração** do modo aplicado; reaplicar
  modo preserva o baseline; falha de reapply mantém o snapshot anterior.
- Journal atômico em `%LOCALAPPDATA%\CouchPad\state\session.json`:
  grava `active`, modos aplicados e baseline (`restoreDisplay`/`restoreAudio`);
  é limpo ao restaurar. Falha de journal não derruba a automação.
- Persistência em `settings.json`: automação, toggles, atrasos, modos, Steam,
  caminho do launcher e idioma; leitura antes de salvar preserva extensões.
- UI:
  - card Gamepad com “Conectar controle”/“Desconectar” simulado;
  - status `status.pending` e tons da state machine;
  - `DisplaySummary`/`AudioSummary` refletem o modo aplicado;
  - card Automação e card Display usam os valores dinâmicos;
  - fallback para watcher real: a VM aceita `IGamepadWatcher`,
    `IDisplayService`, `IAudioService` e `IDelayScheduler` injetados e semeia
    o controle já conectado no startup.
- i18n: `status.pending`, `gamepad.connect/disconnect/simulated/simulated_help`,
  `display.active.*`, `audio.active.hdmi`; 121 chaves em paridade.
- Docs: `docs/automation-simulation.md`, `docs/handoff-deepseek-to-codex.md`.
- Testes: `AutomationCoordinatorTests`, `JsonSessionJournalStoreTests`,
  `JsonAppSettingsStoreTests` ampliado.

### Verificação

| Item | Resultado |
|---|---|
| `dotnet build CouchPad.slnx -c Debug` | 0 avisos, 0 erros |
| `dotnet test CouchPad.slnx -c Debug` | 66 aprovados, 0 falhas |
| `dotnet CouchPad.dll --smoke` | janela configurada 1020×780; pt/en; título CouchPad |
| Fluxo simulado | idle → simulado conectado → pending → active → deactivation pending → restored |
| Persistência | `automationEnabled=true` e idioma gravados; settings relido |
| Journal | arquivo `active` criado ao ativar; limpo após restaurar |
| Fronteira | nenhum P/Invoke/COM/hardware no App/Core; Platform.Windows intocado |

### Capturas de revisão (fora do repositório)

`C:\Users\Usuario\AppData\Local\Temp\opencode\p3-01-idle.png`,
`p3-02-gamepad.png`, `p3-03-pending.png`, `p3-04-active.png`,
`p3-05-deactivation-pending.png`, `p3-06-restored.png`.

### Revisão independente

O agente `reviewer` revisou a P3 e apontou melhorias aplicadas:

1. Journal passou a gravar o baseline (`restoreDisplay`/`restoreAudio`), não
   apenas o modo aplicado.
2. `ActivateAsync`/`DeactivateAsync` capturam uma cópia imutável de
   `AutomationSettings` antes dos awaits, evitando divergência com mudanças
   concorrentes.
3. ViewModel aceita interfaces injetadas e semeia o coordenador a partir do
   gamepad atual no startup.
4. Exemplo de `automation.none` removido de `docs/ui-components.md`.
5. Testes adicionais para desconectar sem snapshot/desligado e para o baseline
   no journal.

Nenhum bloqueador restante.

### Decisões da fase

- D024: simulação atrás de interfaces; atraso injetável.
- D025: state machine cancelável e serializada.
- D026: snapshot/journal guardam o baseline.
- D027: settings persiste todas as preferências.
- D028: UI simula conectar/desconectar gamepad.
- D029: `DisplayState.Topology` opaco e journal `pending` antes do apply.
- D030: supressão pontual de `NU1900` em ambiente offline.
- D031: `DisplayMode.InternalOnly` e mapeamento do estado real.
- D032: `IRescueGuard`/`IRescueLease` no Core.
- D033: composição Windows opt-in na raiz do App.
- D034: `recoveryUncertain` bloqueia novas mutações.
- D035: seletores de display + `AllowDisplayWrites` falso por padrão.

### Fora do escopo (por desenho)

- Display/áudio/HDR reais e watcher real de gamepad (P4/P5).
- Captura de combinação de botões (P4/P6).
- Rescue no logon, bandeja, autostart e tarefa (P6).
- Refresh de displays real; continua placeholder até a P4.
- Screenshots oficiais versionadas (P7a).

### Contrato endurecido P3.1 (09/11/2026)

- `DisplayState.Topology`: snapshot opaco da topologia completa, serializado
  pela implementação de plataforma; o Core persiste e devolve no restore.
- Journal grava `pending` **antes** do apply (com baseline) e `active` depois;
  falha do `pending` inicial aborta a mudança, falha do `active` mantém o
  `pending` para rescue.
- `SessionJournalEntry` também guarda `appliedAudio`.
- Commit `5f5c8c4` (v0.4.1) e `DisplayMode.InternalOnly` (v0.4.2); 66 testes.
  A tag `v0.4.0-sim` permanece no commit da fase; o contrato novo está no HEAD.

### REA/P4 — andamento 2026-11-09

- REA estática parcial aceita pelo usuário como evidência; conclusões e lacunas em `docs/rea-shiftpad-2.2.0.md`, commit `d2e1a74`.
- P4 ainda não implementada ou aceita. O contrato de topologia/journal foi endurecido em `5f5c8c4` (v0.4.1) e `DisplayMode.InternalOnly` entrou em 0.4.2; a implementação CCD, o rollback e os testes de hardware continuam pendentes.
- Probe read-only commitado em `5c53433`: `QDC_ALL_PATHS` encontrou a Samsung `SAM7167` (target 41015) inativa e o baseline só com o AOC ativo. O seletor por EDID/path recusa ambiguidade.
- `docs/p4-hardware-matrix.md` registra a consulta somente leitura e os cenários não executados; dois monitores ativos não demonstram identificação de TV nem aprovação da matriz.
- Build serial passou com zero avisos/erros; 66 testes passaram; smoke abriu e fechou normalmente. Nenhuma topologia foi alterada.

### P4 — slice somente leitura em 2026-11-10

O probe CCD consulta separadamente paths ativos e todos os paths. Na sessão consultada, identificou AOC ativo e Samsung SAM7167 inativa por path e EDID. A seleção não usa ordem dos monitores. Escrita permanece bloqueada. O contrato agora dispõe de `DisplayMode.InternalOnly` (v0.4.2), mas o serviço ainda precisa mapear `Mode`/`PrimaryActive`/`TvActive` e implementar apply, verificação e rollback; o serviço não está integrado à automação. Build sem avisos/erros e 66 testes aprovados; nenhuma mudança de topologia foi executada.

### P4 — mapeamento do baseline, 2026-11-10

`WindowsDisplayOptions` recebe o seletor de TV; `GetCurrent` identifica o target em todos os paths e mapeia o snapshot ativo em `InternalOnly`, `TvOnly`, `Extend` ou `Mirror` quando inequívoco. O probe confirmou `InternalOnly` com AOC ativo e Samsung selecionada inativa. Build 0 avisos/erros; 72 testes aprovados. Escritas não foram implementadas e seguem bloqueadas mesmo com `AllowWrites=true`; não integrar o serviço à automação. Apply, validação, verificação, rollback e testes de hardware de alteração continuam pendentes.

### P4 — planejador de topologia, 2026-11-10

Adicionado planejador puro para propor paths de TV-only, extensão e clone com TV inicialmente inativa; não chama `SetDisplayConfig` nem altera hardware. Exige um único target não TV ativo e não garante suporte: a validação pelo Windows permanece pendente. Build 0 avisos/erros e 77 testes aprovados. Escrita, verificação e rollback continuam não implementados e bloqueados.

### P4 — validação sem mudança, 2026-11-10

`DisplayTopologyValidator` executa apenas `SDC_VALIDATE | SDC_USE_SUPPLIED_DISPLAY_CONFIG`. Na sessão local, as propostas TvOnly, Extend e Mirror retornaram 0 com AOC ativo e Samsung selecionada inativa. Isto não prova apply nem rollback. Build 0 avisos/erros, 77 testes e smoke aprovados. A escrita continua não implementada.

### P4 — fluxo inicial de escrita, 2026-11-10

Fluxo opt-in de apply/restore e helper de transação com rollback adicionados, ainda não validados em hardware nem integrados à UI. Falta tratar transições a partir de TV-only, revisão da comparação exata do snapshot, testes com backend CCD fake e matriz de hardware; não considerar P4 aceita. Build 0 avisos/erros; 81 testes e smoke aprovados. Sem mudança de topologia real.

### P4 — testes do backend falso, 2026-11-10

Backend CCD injetável internamente para testes; 84 testes aprovados e smoke OK. A P4 continua sem aceite: rollback independente de falha do processo, transições a partir de TvOnly e matriz em hardware permanecem pendentes. Sem alteração real de topologia.

### P4 — revalidação sem apply, 2026-11-10

No HEAD `cc4d6c3`, build 0 avisos/erros, 84 testes e smoke passaram; probe de validação CCD retornou 0 nos três modos com Samsung inativa. A P4 não foi aceita: apply e rollback no driver não foram testados, e falta recuperação independente de falha do processo.

### P4 — planejador com monitor explícito, 2026-11-10

Planejador atualizado para usar seletores explícitos e QDC_ALL_PATHS, incluindo origem TvOnly; doze combinações sintéticas passaram. Build 0 avisos/erros, 96 testes e smoke aprovados. Não executar SDC_APPLY antes de implementar e testar watchdog independente; restauração ainda compara bytes exatos. Matriz real pendente.

### P4 — comparação semântica inicial, 2026-11-10

A verificação de restore compara identidades CCD e campos de modos em vez dos buffers inteiros. Build 0 avisos/erros, 97 testes e smoke passaram. Sem watchdog independente e sem testes reais; nenhuma alteração de topologia. P4 não aceita.

### P4 — contrato rescue e comparação semântica, 2026-11-10

O Core/App recebeu em `0281b7d` o contrato `IRescueGuard`/`IRescueLease`: begin após journal pending, commit após apply/verify, abort após rollback bem-sucedido e abandon quando incerto. A implementação Windows independente permanece pendente; a simulação mantém o guard noop. O comparador semântico está em desenvolvimento e não foi validado contra normalizações reais do driver. Build 0 avisos/erros, 104 testes e smoke aprovados. Não executar SDC_APPLY até o watchdog real e seus testes de crash estarem verdes.

### P4 — laço de decisão do watchdog, 2026-11-10

Laço puro e testes simulam commit/abort, morte do pai, timeout, abandon e concorrência entre commit e morte; 110 testes e smoke passaram. Isso ainda não implementa processo separado, IPC, leitura do journal, lease nem composição do guard Windows. A escrita real não foi executada; manter `AllowWrites=false` e não usar o laço isolado como rescue.

### P4 — watchdog em implementação, 2026-11-10

`f9ab5ff` registra o laço puro e seus testes. Há trabalho não commitado para processo separado e `WindowsRescueGuard`, ainda sem testes de processo ou composição no App. O projeto rescue compilou antes da última revisão; a compilação e os testes posteriores não foram executados porque a revisão automática de permissão atingiu o limite de uso. O protocolo, a recuperação após queda e a exclusão mútua não foram comprovados. Nenhum SDC_APPLY foi executado; não habilitar o serviço real antes de validação completa.

### P4 — testes de processo fake do watchdog, 2026-11-10

Passaram commit, abort, abandon, timeout, dispose, pipe quebrado e morte do pai antes/depois de apply simulado, tanto com filho fake quanto com o código rescue compilado com backend fake; single instance também passou no segundo. Build da solução 0 avisos/erros, 110 testes unitários e smoke passaram. Sem SDC_APPLY; integração ao App, revisão do protocolo e testes reais continuam pendentes. Alterações sem commit.

### P4 — composição do guard, 2026-11-10

A composição do App continua em simulação por padrão (inclusive smoke/galeria). A opção técnica `--windows-display <tv-path> <tv-manufacturer> <tv-product> <monitor-path> <monitor-manufacturer> <monitor-product>` cria conjuntamente `WindowsDisplayService` e `WindowsRescueGuard` com o mesmo journal. Ambos ficam impedidos de escrever por padrão: `AllowWrites=false` e `allowRecovery=false`; o guard recusa iniciar o filho nesse estado, evitando restore pelo timeout quando nenhum apply ocorreu. O build copia o executável e arquivos de runtime do rescue para a saída Debug; empacotamento de publicação ainda não foi validado. D033 registra a referência da raiz de composição à plataforma.

Build 0 avisos/erros, 114 testes unitários e smoke passaram; o executável copiado iniciou e recusou argumentos ausentes. Testes de processo com backend falso passaram novamente após o gate. A composição e o guard ainda não foram validados em hardware, nem se deve habilitar escrita ou declarar P4 aceita.

### P4 — revisão do watchdog e publicação, 2026-11-10

A revisão `docs/p4-watchdog-review.md` identificou pendências impeditivas: morte do watchdog após READY, concorrência timeout/apply, ACK de commit incerto e diagnóstico/retry de falha do rescue. Escrita segue desabilitada. Publicação Release win-x64 self-contained inicialmente omitiu a DLL do rescue; após correção, exe/DLL/deps/runtimeconfig estavam presentes e o executável sem argumentos retornou 2. Build 0 avisos/erros, 114 testes e smoke passaram. Sem SDC_APPLY; P4 não aceita.

### P4 — acompanhamento de ACK e diagnóstico, 2026-11-10

Cancelamento após o envio do sinal não interrompe a espera por ACK; falha de pipe ao enviar ACK leva o filho a tentar restore. Testes de processo com backend falso passaram para esses cenários. Rescue grava falhas tratadas localmente sem mensagem de exceção; retry no logon permanece P6. Duas capturas CCD consecutivas foram semanticamente equivalentes, sem mudança de topologia. Morte do watchdog após READY, concorrência timeout/apply e resultado incerto de ACK continuam pendentes; não habilitar escrita. Build 0 avisos/erros; 115 testes e smoke passaram.


### P4 — monitoração do watchdog, 2026-11-10

O lease sinaliza falha do processo antes da liberação e oferece verificação imediata; o coordenador vincula o sinal ao token da transição, verifica antes/depois das operações e preserva o journal em perda do watchdog (D034). Testes cobrem perda antes, durante e depois do apply falso, inclusive chamada não cancelável, e perda durante rollback/desativação. O processo rescue com backend falso foi encerrado após READY e a falha foi detectada; commit/abort normais não sinalizaram falha. Build 0 avisos/erros, 120 testes e smoke passaram. Timeout concorrente com apply e ACK incerto continuam pendentes; sem SDC_APPLY real.

### P4 — seletores e opt-in de display (DeepSeek), 2026-11-10

A UI agora descobre targets CCD em modo read-only, permite escolher TV e monitor interno, persiste path/EDID em settings e adiciona o toggle avançado `AllowDisplayWrites` (padrão falso). O App só compõe `WindowsDisplayService` com escrita e `WindowsRescueGuard` com recovery quando o opt-in e os dois seletores estão completos; caso contrário continua em simulação. O banner `recovery.help` aparece quando o coordenador marca `recoveryUncertain` e o toggle de automação fica bloqueado até reiniciar. Build 0 avisos/erros, 144 testes (Core + App), smoke OK; nenhum `SDC_APPLY` executado. A matriz real P4 continua pendente e é do Codex/usuário. Runbook em `docs/p4-runbook.md`.

### P4 — isolamento e recuperação, 2026-11-10

Decisões duráveis vinculadas ao baseline e exclusão entre processos impedem escrita concorrente do app e rescue; o lock da decisão é mantido durante a chamada CCD. Journal existente, ilegível ou falha ao limpar bloqueia automação; toda instância Windows que usa o journal compartilhado obtém posse exclusiva, inclusive simulação. Desligar o opt-in revoga novos applies na sessão e mantém restore disponível (D036–D041). Revisão independente encontrou a disputa entre simulação e hardware e a correção foi incorporada. Build 0 avisos/erros, 162 testes, smoke e testes de processo com backend falso passaram, incluindo crash, timeout, ACK perdido e espera pelo lock. Nenhum `SDC_APPLY` real: matriz, imagem visível e kill durante apply real permanecem pendentes. Uma chamada síncrona CCD travada pode atrasar indefinidamente o watchdog; recuperação após falha simultânea e logon pertence à P6.

Outra revisão identificou restore desnecessário antes de qualquer tentativa de apply. D042 registra a classificação de falha comprovadamente sem mutação: guard não iniciado, cancelamento anterior e recusa de validação não devem produzir SDC_APPLY de rollback. Depois dessa correção, 163 testes passaram. O teste com processo rescue real e backend falso confirmou que o timeout espera a liberação da trava da decisão e restaura em seguida. A matriz real continua pendente.

Na primeira execução real, `TvOnly` foi verificado por CCD e pelo usuário (imagem na Samsung, AOC apagado). O controle foi interrompido por Escape antes de o agente comandar restore. Ao retomar, o CCD apontava `InternalOnly`, enquanto o App ainda informava `Ativo`/TV e o journal real seguia `active` com baseline. Causa da divergência desconhecida; não há prova de restore pelo CouchPad. Matriz interrompida para apuração, com AOC ativo e Samsung inativa segundo CCD. Não sobrescrever o journal. P4 não aceita; ver `docs/p4-hardware-matrix.md`.

O usuário esclareceu que retornou ao AOC via Windows/Win+P. O CouchPad foi então desconectado e limpou o journal, com CCD em `InternalOnly`; isso não valida restore direto de `TvOnly`. `Extend` foi aplicado e confirmado pelo usuário e por CCD, seguido de restore pelo CouchPad para `InternalOnly` com journal limpo. A tentativa seguinte de `Mirror` apresentou erro no App, de causa/etapa desconhecida; CCD terminou `InternalOnly`, journal ausente e usuário confirmou imagem normal no AOC. Matriz interrompida por segurança, sem prosseguir para falhas e kill real. P4 não aceita.

O usuário retirou o espelhamento do produto (D043): a opção foi removida, pedidos de apply são recusados sem escrita e configurações antigas nesse modo iniciam com automação desligada. Leitura e restore de baseline espelhado permanecem disponíveis para recuperação. Mirror não é mais critério da P4; a evidência histórica do erro foi preservada. Build Release sem avisos/erros, 166 testes e smoke passaram; Debug estava bloqueado pela instância de teste ainda aberta. A validação de TvOnly com restore direto e os cenários de falha/crash continuam pendentes.

Fluxo desejado pelo usuário: conectar controle → contagem configurável padrão 2 s → TV exclusiva e áudio na TV → abrir Steam Big Picture ou outro launcher → desconectar controle → restaurar display e áudio anteriores. D044 ajusta novas configurações/redefinição para 2 s e mostra a contagem localizada; preferências existentes são mantidas. Controle e áudio ainda são simulados e o launcher real não está integrado. Build Release 0 avisos/erros, 166 testes e smoke passaram. P4 continua sem aceite e as integrações reais dependem da conclusão e validação das fases correspondentes.

O card localizado “Contagem regressiva” reúne os seletores independentes de ativação e desativação, preservando os valores salvos e as opções imediato/1/2/3/5 segundos. O layout responsivo inclui o novo card nas três larguras. Build Release 0 avisos/erros, 167 testes e smoke passaram; nenhuma alteração de hardware foi executada.

O usuário forneceu `shiftpad_preview.png` e solicitou seu uso como imagem e ícone do aplicativo (D045). O arquivo está em `src/CouchPad.App/Assets/couchpad.png`, com conversão para ICO multirresolução no mesmo diretório; a marca no cabeçalho, o ícone da janela e o executável usam esses recursos. Build Release, 167 testes e smoke passaram; o ícone do executável foi extraído com sucesso. Nenhuma alteração de hardware foi executada.

Por solicitação do usuário, a imagem gerada com fundo transparente substituiu o PNG e o ICO multirresolução. O canto do PNG tem alpha zero; build Debug, 167 testes e smoke passaram. A instância Release aberta bloqueou a recompilação Release e conserva a imagem anterior até ser fechada e recompilada.

Em 2026-11-11, `TvOnly` foi novamente aplicado no hardware e confirmado pelo usuário. Após interrupção do controle de computador, a topologia ainda estava em `TvOnly` com journal `active`; a ação **Desconectar** no CouchPad restaurou o baseline para `InternalOnly`, limpou o journal e o usuário confirmou imagem no AOC. Ver `docs/p4-hardware-matrix.md` para detalhes e limitações. Os 167 testes unitários e o harness de crash/timeout com backend falso passaram novamente. P4 segue pendente dos cenários reais de falha/cancelamento, alvo indisponível/ambíguo e kill durante apply, além da revisão/aceite; sem tag.

Validação adicional: o probe somente leitura recusou um seletor inexistente e a consulta posterior continuou em `InternalOnly`; 27 testes direcionados passaram com backend falso. Isso não equivale a desconexão física ou teste de apply inválido. Os demais cenários reais da P4 permanecem pendentes.

O probe recebeu opção para validar proposta CCD com target inválido: `SDC_VALIDATE` retornou 87 e o baseline permaneceu semanticamente igual. Não foi chamado apply; modo de vídeo indisponível e demais falhas reais seguem pendentes.

Com a Samsung fisicamente desconectada pelo usuário, o Windows enumerou somente AOC e o probe recusou a TV ausente; o usuário confirmou imagem no AOC. Após reconexão, path/EDID da Samsung foram encontrados e as validações CCD de TvOnly/Extend retornaram 0 sem apply. O teste não exercitou apply durante desconexão nem ambiguidade física.

Em nova desconexão física da Samsung, o Codex tentou ativar `TvOnly` pelo App: este mostrou erro, journal permaneceu ausente, Windows detectou somente AOC e o usuário confirmou imagem normal. A reconexão foi verificada pelo probe. As chamadas CCD individuais não foram instrumentadas; ambiguidade física, modo indisponível e testes de falha/crash após mutação permanecem pendentes.

Regra em `AGENTS.md`: executar e registrar cada validação antes da próxima, continuar sem pedidos repetidos e parar imediatamente em falha de recuperação/tela preta. `SDC_VALIDATE` recusou resolução impossível com código 31 e sem alteração semântica da topologia; não houve apply. Pendências ordenadas em `docs/p4-hardware-matrix.md`: ambiguidade física, falha/cancelamento após alteração, desconexão durante transição, kill durante apply, validação final e revisão.

O usuário informou não haver segundo display com nome idêntico para produzir ambiguidade física. Três testes sintéticos de seleção, fallback e recusa de ambiguidade passaram; não há evidência de ambiguidade em hardware. A sequência estrita depende de equipamento adequado ou mudança explícita do critério.

O usuário dispensou expressamente o teste de ambiguidade em hardware por falta de equipamento; a evidência continua limitada aos testes sintéticos. Prosseguir para falha/cancelamento após alteração, depois desconexão durante transição e kill durante apply.

Preparação de falha pós-apply: `tools/DisplayFailureHardware` foi compilado, requer `--hardware-confirmed`, exige posse exclusiva do journal e o recusa se presente; usa coordenador e watchdog reais e injeta falha no áudio simulado após apply de display. Sem argumentos retornou 2; três testes relacionados com backend falso passaram. O instrumento ainda não foi executado nem revisado de forma independente em hardware. A instância anterior do CouchPad mantém a posse exclusiva e estava minimizada, impedindo a preparação segura do teste. Falha/cancelamento continua pendente.

Teste real posterior: com baseline AOC, `DisplayFailureHardware` aplicou TvOnly e injetou falha controlada no áudio simulado após apply; o coordenador reportou a falha esperada, restaurou baseline semanticamente igual e limpou journal. O usuário confirmou visualmente a ida à TV e o retorno ao AOC. Faltam cancelamento, desconexão durante transição e kill durante apply; teste não prova falha dentro da chamada CCD.

Cancelamento após apply real também passou: o instrumento sinalizou desconexão enquanto áudio simulado aguardava, o coordenador restaurou baseline semanticamente igual e limpou journal, e o usuário confirmou ida à TV e retorno ao AOC. Isso não prova cancelamento dentro de `SetDisplayConfig`. Restam desconexão física durante transição e kill durante apply.

Desconexão física: após apply `Extend`, durante a operação de áudio simulada, o usuário retirou o cabo da Samsung mantendo AOC ativo. O instrumento detectou ausência, injetou falha e verificou baseline semanticamente igual com journal ausente; usuário confirmou imagem no AOC e reconectou a TV. Não foi desconexão dentro da chamada CCD. Falta kill durante apply e revisão final.

Morte do processo: teste encerrou o PID próprio após retorno do apply CCD e antes do commit, enquanto aguardava áudio simulado. Watchdog registrou `recovered`; baseline do journal coincidiu semanticamente com a topologia atual e o usuário confirmou imagem no AOC. Após conferir decisão, hash e posse exclusiva, o journal de teste foi limpo. Esse cenário não prova morte dentro de `SetDisplayConfig`; manter essa limitação explícita na revisão e no aceite. Build Release e 167 testes passaram; smoke OK.

Revisão independente apontou que uma opção temporária de limpeza do journal poderia confundir uma decisão antiga `recovered` com uma nova transação de mesmo baseline. A opção foi removida e não deve ser disponibilizada; no teste específico a decisão recém-gerada foi identificada e conferida. A revisão confirmou que o kill foi após o retorno da chamada CCD e que uma chamada CCD travada pode atrasar indefinidamente o watchdog.

Revisão final para aceite: não foram encontrados `cdb`, `windbg`, `procdump`, `vsdbg` nem Windows Kits Debuggers no caminho padrão; não se conseguiu sincronizar kill dentro de `SetDisplayConfig` com a instrumentação disponível. Isso é limitação, não evidência de impossibilidade. Build e 167 testes Release e smoke passaram em 2026-11-11; journal verificado ausente antes do smoke. Aguardar decisão do usuário sobre aceite da P4 com esse risco; não criar tag de hardware antes da P6. A recuperação no logon continua pendente da P6.

**P4 aceita em 2026-11-11:** o usuário aceitou expressamente a limitação do teste de morte dentro de `SetDisplayConfig` e o risco associado ao timeout. O cenário permanece não comprovado; a recuperação após reinício/logon continua na P6. Nenhuma tag de hardware foi criada.

### P5 — início somente leitura, 2026-11-11

`WindowsAudioProbe` e `tools/AudioProbe` consultam os endpoints Core Audio e os IDs padrão dos três papéis, sem escrita; a sessão tinha oito endpoints, um ativo, e os três papéis com o mesmo padrão. `AudioOutput` atual não preserva IDs por papel e o watchdog ainda restaura apenas display, de modo que escrita real de áudio continua desabilitada. Falta identificar endpoint da TV, ampliar contrato/journal/rescue com decisão registrada, implementação de áudio/HDR e matriz de hardware. Build Debug e 167 testes passaram; ver `docs/p5-audio-hdr.md`.

O usuário dispensou o teste de HDR da etapa atual e pediu registrar o tema para implementações futuras. O cenário está adiado, não aprovado; requisitos e validação de áudio da P5 permanecem. Ver `docs/p5-audio-hdr.md`.

Na primeira revalidação de áudio somente leitura, o probe compilou sem avisos/erros e reportou oito endpoints, um ativo, IDs únicos e padrões dos três papéis estáveis em duas leituras; PnP identificou o dispositivo presente como AOC 27G2G4. Nenhum áudio foi alterado e o endpoint da Samsung não foi identificado. Testes de troca/restauração e som real continuam pendentes de implementação.

As pendências de áudio foram listadas em ordem de implementação e validação em `docs/p5-audio-hdr.md`. Conforme regra do projeto e solicitação do usuário, concluir e registrar cada item antes de iniciar o próximo; HDR segue adiado.

Na observação protegida com `TvOnly`, `SAMSUNG (3- HD Audio Driver for Display Audio)` ficou ativo e padrão nos três papéis; após restore a leitura imediata ainda indicou Samsung, e a leitura posterior indicou AOC ativo/padrão. A troca de display pode, portanto, alterar padrões automaticamente e de forma assíncrona. Testes sintéticos de seleção exata recusam ausência e ambiguidade. Não foram provados vínculo estável CCD↔endpoint ou igualdade exata dos IDs antes/depois; item 1 de áudio ainda em andamento. 169 testes passaram.

Teste posterior tocou tom iniciado após `TvOnly`; usuário confirmou som na Samsung e imagem no AOC após retorno. IDs padrão dos três papéis voltaram aos valores prévios após cerca de 1,9 s. Conteúdo que já tocava não foi ouvido na Samsung no teste anterior; migração de streams existentes permanece pendente. Identificação para este hardware e seleção pura por ID foram validadas, mas vínculo automático CCD↔endpoint e integração de produção não foram comprovados.

P5 item 2 em andamento: D046 introduz baseline versionado com IDs por papel e volume/mudo, persistência opcional no journal, hash de rescue compatível com entradas antigas e captura anterior ao display no coordenador. Serviço com baseline real exige guard que declare recuperação de áudio; guard Windows ainda não declara, por isso a troca real continua bloqueada. Fakes validam journal antes de apply e restore, e serialização cobre papéis distintos. 174 testes passaram; captura real de volume/mudo e rescue de áudio permanecem pendentes.

O probe agora captura, sem escrita, IDs por papel e volume/mudo dos endpoints padrão via Core Audio. Na máquina atual os três papéis estavam representados por um endpoint; duas capturas mantiveram os mesmos IDs e volume válido. Compatibilidade do hash anterior foi testada. Restauração real, papéis fisicamente distintos e watchdog de áudio não foram validados; guard real continua bloqueando a integração de áudio.

P5 item 3 em andamento: guard e executável rescue de display recusam explicitamente journal que contém baseline real de áudio, impedindo falso sucesso antes da implementação de restore de áudio. Teste de guard verifica recusa antes de iniciar o processo e preservação do journal; 175 testes passaram. Recuperação independente de áudio ainda não está implementada.

### P5 aceite e P7a snapshot (DeepSeek), 11/11/2026

- P5 **aceita pelo usuário** com as limitações documentadas: endpoint
  tardio/papéis distintos só com fakes, migração de stream preexistente não
  comprovada e HDR adiado.
- P7a (snapshot): `docs/ui-a11y-checklist.md`, contraste corrigido
  (`DangerMuted #35191B`), live region nos erros/recuperação, README atualizado,
  notas de release em rascunho e seis capturas em `docs/screenshots/`.
- As capturas oficiais serão refeitas após a P6 (bandeja/autostart/logon).
- Build 0 avisos/erros, **194 testes**, smoke OK; nenhuma tag nova.
- Próxima validação da fila: watcher físico de gamepad.

### P7a — a11y, i18n e screenshots oficiais (13/11/2026)

- Contraste revisado: `DangerMuted #35191B` (4,62:1), tokens de texto/estado
  AA; erros de startup, launcher e recuperação em `Danger` + texto.
- Ordem de foco alinhada à ordem visual nos 3 breakpoints via reordenação do
  visual tree; UIAutomation confirmou ordem e nomes (pt/en, 3/2/1 colunas), sem
  controles focáveis sem nome.
- Live regions: status pill, recuperação incerta, falha de recuperação, erro de
  startup, erro de launcher e erro de descoberta de display.
- Bandeja: headers localizados e tooltip via `app.name`; menu nativo não foi
  percorrido por Narrator/NVDA.
- i18n: 157 chaves em paridade; nenhuma string visível hardcoded.
- Seis capturas oficiais em `docs/screenshots/` (pt/en × 1020/780/640),
  simulação, versão `0.6.0`; README das capturas atualizado.
- README, notas de release, `ui-spec` e checklist de a11y atualizados.
- Build 0 avisos/erros, **259 testes** e smoke OK; nenhuma tag nova.
- **P7a aceita pelo usuário em 13/11/2026.** Pendências registradas: Narrator/NVDA completo, menu nativo da bandeja com leitor de tela, anúncio da contagem em 5 s, reconexão do watcher sem evidência independente; HDR/notificações adiados.
