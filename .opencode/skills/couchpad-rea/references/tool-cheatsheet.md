# REA — cheat sheet

## Diagnóstico e setup

```powershell
npx -y rea-agents@latest doctor --client opencode --json
npx -y rea-agents@latest setup --client opencode --dry-run --json
npx -y rea-agents@latest setup --client opencode --yes
```

- `doctor` é somente leitura.
- `setup --dry-run` mostra caminhos, backup e comando.
- `setup --yes` grava a configuração; aprovar antes.
- Não usar `--install-hopper` no Windows.

## Identidade

```powershell
npx -y rea-agents@latest inspect-managed-artifact "analysis\shiftpad-2.2.0\ShiftPad.r2r.dll" --format json
```

- MCP: `inspect_managed_artifact` com `detail: "summary"`.
- Retorna identidade, framework, MVID, contagens, cobertura e limitações.
- No EXE de 263 MB: `resource_constraint` (limite 128 MiB).

## Membros

MCP:

```json
{
  "path": "analysis\\shiftpad-2.2.0\\ShiftPad.r2r.dll",
  "detail": "summary",
  "type_names": ["*Display*", "*Audio*", "*Hdr*", "*Journal*", "*Rescue*"]
}
```

Depois paginar com `inspect_analysis_view`:

```json
{
  "parent_evidence_id": "ev_...",
  "view": {
    "kind": "managed-members",
    "collection": "methods",
    "offset": 0,
    "limit": 64
  }
}
```

Coleções: `types`, `exported_types`, `fields`, `methods`, `member_refs`,
`call_edges`, `field_accesses`, `user_strings`.

## P/Invoke

- MCP: `inspect_managed_native_boundaries`.
- Mapeia DllImport, `LibraryImport`, implementações nativas e nomes de DLL.
- Útil para achar `SetDisplayConfig`, `QueryDisplayConfig`, `DisplayConfig*`,
  Core Audio, `IPolicyConfig`, Task Scheduler.

## PE

```powershell
npx -y rea-agents@latest inspect-pe-imports "<path>" --format json
npx -y rea-agents@latest inspect-pe-certificates "<path>" --format json
npx -y rea-agents@latest inspect-pe-resources "<path>" --format json
npx -y rea-agents@latest inspect-pe-load-config "<path>" --format json
```

- Não executam o alvo.
- Importações não provam uso em runtime.
- Certificado não é veredito de confiança.

## Comparação

- MCP: `compare_managed_members`.
- Compara tipo, nome e assinatura; nome sozinho não é match.
- Usar para 2.2.0 × DLL pública.

## Evidência

- MCP: `export_evidence_bundle` para um caminho escolhido.
- Guardar IDs `ev_...` no relatório.
- Não repetir a mesma análise; reutilizar o ID.

## Limites

| Limite | Valor/regra |
|---|---|
| Snapshot gerenciado | 128 MiB |
| NativeAOT | Sem CIL; Ghidra para pseudocódigo |
| Ghidra no Windows | P0 somente leitura |
| Runtime nativo no Windows | Não coberto pelo REA |
| Execução | Proibida nesta skill |

## CLI × MCP

- MCP: bom para exploração interativa e views paginadas.
- CLI: bom para uma consulta pontual e para não carregar 229 schemas de
  ferramentas no contexto.
- Em ambos, preservar a saída completa em arquivo/evidência e ler só o resumo.
