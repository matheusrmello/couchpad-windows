# Runbook — matriz P4 em hardware

- Data: 2026-11-10
- Dono da execução: Codex, com o usuário presente
- Objetivo: validar display real, rollback e watchdog
- Regra: se o rollback falhar ou a tela ficar preta, parar imediatamente

---

## 1. Pré-condições

- Build 0 avisos/0 erros; 144 testes; smoke OK.
- TV Samsung SAM7167 conectada e detectada; AOC 27G2G4 é o monitor interno.
- `CouchPad.Rescue.exe` presente na saída do app.
- Nenhum `SDC_APPLY` executado até este runbook.
- Usuário na frente da máquina.

Targets atuais (podem mudar de porta/reboot; use a UI para redescobrir):

| Display | EDID | Target |
|---|---|---|
| Samsung | `11596/29031` | `41015` |
| AOC | `58117/9986` | `24646` |

Path Samsung:

```text
\\?\DISPLAY#SAM7167#7&350abd5e&1&UID41015#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
```

---

## 2. Configuração

1. Abra o CouchPad normalmente.
2. No card **Display**, clique em **Atualizar alvos**.
3. Selecione a Samsung em **TV** e o AOC em **Monitor interno**.
4. Em **Parâmetros**, ative **Permitir escrita de display**.
5. Feche e reabra o CouchPad (o seletor só é aplicado no startup).
6. Confirme no card Display que os alvos continuam selecionados.

Se a descoberta falhar, use `settings.json` com:

```json
{
  "allowDisplayWrites": true,
  "tvDevicePath": "\\\\?\\DISPLAY#SAM7167#7&350abd5e&1&UID41015#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}",
  "tvEdidManufacturer": 11596,
  "tvEdidProduct": 29031,
  "tvExactName": "SAMSUNG",
  "internalDevicePath": "\\\\?\\DISPLAY#AOC2702#7&350abd5e&1&UID24646#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}",
  "internalEdidManufacturer": 58117,
  "internalEdidProduct": 9986,
  "internalExactName": "27G2G4"
}
```

O caminho exato do AOC deve ser confirmado com **Atualizar alvos**; não invente.

---

## 3. Baseline

1. Tela atual: AOC ativo, Samsung inativa.
2. Capture a topologia de baseline pelo probe read-only ou pelo card Display.
3. Registre: data, operador, conexões, resolução, HDR, áudio.

---

## 4. Matriz

Para cada cenário, registre resultado e evidência. Não avance se o anterior
falhar.

| # | Cenário | Ação | Verificação |
|---|---|---|---|
| 1 | TvOnly | Modo de tela = Somente TV; conectar gamepad simulado; automação ligada | imagem só na Samsung; AOC desligado; estado `Ativo` |
| 2 | Rollback TvOnly | Desconectar gamepad ou desligar automação | AOC volta; baseline restaurado; estado de espera/idle |
| 3 | Extend | Com automação ativa, modo = Estender | AOC + Samsung ativos; área estendida; depois rollback |
| 4 | Espelhamento | Fora do escopo por decisão do usuário | Não executar |
| 5 | Modo indisponível | Forçar modo não suportado (se aplicável) | recusa antes de mudar |
| 6 | Sem TV/ambíguo | Remover/ambiguar seletor | nenhuma alteração |
| 7 | Falha/cancelamento | Cancelar/desligar durante apply | rollback confirmado |
| 8 | Kill durante apply | Encerrar apenas o PID do processo CouchPad iniciado para este teste, durante o apply | watchdog restaura o baseline |
| 9 | Reinício com journal pendente | Deixar journal `pending` e reiniciar/logon | evidência P6; registrar separadamente |

Cenário 8: faça apenas depois dos cenários 1–7 verdes. O watchdog tem timeout
de 60 s; aguarde o restore. Se não restaurar, use `Win+P`/Configurações de tela
ou reinicie; registre a falha e pare.

---

## 5. Evidência obrigatória

Para cada linha:

- topologia inicial e final;
- resultado de `SetDisplayConfig` e `QueryDisplayConfig`;
- imagem visível (Samsung/AOC);
- tempo de apply/rollback;
- estado do journal (`pending`/`active`/limpo);
- operador e horário;
- limitações encontradas.

Uma falha de rollback não pode ser tratada como aprovação.

---

## 6. Critério de aceite P4

- TvOnly e Extend com imagem visível e rollback;
- falha/cancelamento sem tela preta;
- kill durante apply com restore do watchdog;
- comparador semântico sem falso negativo no driver real;
- matriz registrada em `docs/p4-hardware-matrix.md`;
- tag de hardware **não** é criada aqui; só ao final da P6.
