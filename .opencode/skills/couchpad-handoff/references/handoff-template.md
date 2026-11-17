# Template de fechamento de fase

Use em `docs/handoff-<fase>.md` quando o usuário pedir um documento separado.
O ledger em `HANDOFF.md` continua sendo a fonte principal.

```md
# Fechamento — <fase>

## Identidade

- Fase: <P0/P1/.../roadmap>
- Dono: OpenCode
- Tag: <v...>
- Commit: <sha>
- Data: AAAA-MM-DD
- Estado: implementado / validado / aceito

## O que foi entregue

- <item verificável>
- <item verificável>

## O que ficou de fora

- <item> — motivo.
- <item> — motivo.

## Contratos e arquivos

- Core: <interfaces/modelos>
- App: <views/viewmodels>
- Platform.Windows: <APIs/implementações>
- Docs: <caminhos>

## Validação

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"
& $dotnet build CouchPad.slnx -c Debug
& $dotnet test CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --smoke
```

- Build: <0/0>
- Testes: <n/n>
- Smoke: <resultado>
- Hardware: <matriz/cenários>
- Evidências: <screenshots, hashes, logs>

## Pendências e riscos

- <pendência> — dono OpenCode; próximo passo.
- <risco> — mitigação/limite.

## Próximo passo

<uma frase com a próxima ação concreta>
```
