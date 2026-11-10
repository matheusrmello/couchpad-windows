# CouchPad Windows — Handoff e Bootstrap

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
| P4 display | Codex | pendente | Depende de spec REA + hardware |
| P5 áudio/HDR | Codex | pendente | Depende de P4 |
| P6 rescue/tray | Codex | pendente | Depende de P4/P5 |
| P7a docs/a11y | DeepSeek | pendente | Depende de P6 |
| P7b corrections/release | Codex | pendente | Depende de P7a |

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
