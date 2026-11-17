---
name: couchpad-rea
description: Use ao analisar estaticamente o binário ShiftPad 2.2.0 com REA, com foco em tela, áudio, HDR e rescue. Cobre setup, extração do bundle, sequência de ferramentas, evidência e limites legais.
---

# CouchPad REA

## Quando usar

- Antes de implementar P4–P6.
- Para responder dúvidas de comportamento do ShiftPad 2.2.0.
- Para comparar o binário distribuído com o código público.

## Quando NÃO usar

- Para código-fonte comum do CouchPad.
- Para licença/ativação (fora do escopo).
- Para executar o ShiftPad ou gerar tráfego de ativação.

## Limites legais e éticos

- **Não executar** o ShiftPad.
- **Não** ativar licença, trial ou contatar servidores.
- **Não** copiar código, ícones, textos ou assets.
- **Não** contornar proteção/licença.
- O repositório público não declara licença; use apenas para comparação.

## Setup no OpenCode

```powershell
npx -y rea-agents@latest doctor --client opencode --json
npx -y rea-agents@latest setup --client opencode --yes
```

- Verificar a registration do cliente OpenCode no resultado do `doctor`.
- Se a versão do CLI não reconhecer `opencode`, configure o servidor MCP do REA
  manualmente no OpenCode e valide as ferramentas antes de usá-las.
- O Codex foi descontinuado em 2026-11-17; não usar `--client codex`.
- Ghidra/IDA não são necessários para .NET gerenciado.
- O rescue é NativeAOT; pseudocódigo exigiria Ghidra (passo separado).

## Identidade dos artefatos

| Artefato | SHA-256 |
|---|---|
| `ShiftPad.exe` 2.2.0 | `BF67951644497A853CE3093BA72798B83B81B788F96770E7F049D1B6390CD032` |
| `ShiftPadRescue.exe` 1.0.0 | `219df12da0f873baf0f3ee745ee3d9ca3d174ac12c3f2ae4a01a82a80cb4f100` |
| `ShiftPad-Setup-2.2.0.exe` | `0acf2a319fba3d8bf8c148b0ea365fecc3600d9714dc2bb9379639a82282d476` |
| `GameShift.dll` (repo) | `0f198fb13f11256be4b3fb9d1e808ba38d5d83e312a95e893ccbdca697ce399b` |

Caminhos:

- `%LOCALAPPDATA%\Programs\ShiftPad\ShiftPad.exe`
- `%LOCALAPPDATA%\Programs\ShiftPad\ShiftPadRescue.exe`
- Repositório clonado: `C:\Users\Usuario\Documents\couchpad\.build\rea-setup\shiftpad-source`

## Extração do bundle

- O `ShiftPad.exe` tem 263 MB e excede o snapshot de 128 MiB do REA.
- Erro observado: `resource_constraint` / `max_snapshot_bytes: 134217728`.
- Extrair o assembly gerenciado (`ShiftPad.r2r.dll`) e o `.deps.json` com um
  parser local do índice embutido, **sem executar**.
- Salvar em `analysis/shiftpad-2.2.0/` e registrar hashes.
- O rescue é NativeAOT: `inspect-managed-artifact` retorna
  “no CLI data directory”.

## Sequência de ferramentas

1. `inspect_managed_artifact` (`detail: summary`) na DLL extraída.
2. `inspect_managed_members` com `type_names`:
   `*Display*`, `*Audio*`, `*Hdr*`, `*Color*`, `*Cec*`, `*Journal*`,
   `*Rescue*`, `*Settings*`, `*Gamepad*`.
3. `inspect_managed_native_boundaries` para P/Invoke e DLLs nativas.
4. `user_strings` e `call_edges` para caminhos, APIs e sequência.
5. `inspect_pe_imports`, `inspect_pe_certificates`, `inspect_pe_resources`,
   `inspect_pe_load_config` no rescue e no host.
6. `compare_managed_members` entre 2.2.0 e a DLL pública.
7. `export_evidence_bundle` para auditoria.

Detalhes em `references/tool-cheatsheet.md`.

## Disciplina de evidência

- Summary-first; nunca colar dump completo no contexto.
- Usar `filter-output`, paginação e IDs de evidência.
- Registrar: comando, artefato, hash, ID (`ev_...`), limitação.
- Separar **observado**, **inferido** e **desconhecido**.
- Não afirmar execução; análise é estática.
- Comparações usam identidade exata (hash/MVID), não nome.

## Formato do relatório

`docs/rea-shiftpad-2.2.0.md`:

1. Escopo e identidade dos artefatos.
2. Metodologia e ferramentas.
3. Tela: APIs, sequência, identidade do TV, modos, falhas.
4. Áudio: APIs/COM, endpoint, timing, restauração.
5. HDR: captura, reaplicação, falhas.
6. Rescue: journal, gatilho, decisão, concorrência.
7. Fonte pública × binário 2.2.0.
8. Lacunas e testes necessários.
9. IDs de evidência e limitações.

## Condições de parada

- Ferramenta retorna `resource_constraint`: reduzir escopo/extrair.
- Evidência ambígua: registrar como desconhecido, não inferir.
- Necessidade de executar o alvo: parar e pedir autorização.
- Necessidade de Ghidra: parar e pedir aprovação separada.
- Dúvida legal: parar e consultar o usuário.

## Referências

- `references/tool-cheatsheet.md` — comandos, limites e exemplos.
- `references/questions.md` — perguntas por área.
- `docs/rea-shiftpad-2.2.0.md` — relatório final.
