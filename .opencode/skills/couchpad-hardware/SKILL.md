---
name: couchpad-hardware
description: Use antes de qualquer teste que altere tela, áudio, HDR ou rescue no CouchPad Windows (P4–P6). Define fail-closed, captura antes/depois, rollback, JSON de diagnóstico e matriz de teste.
---

# CouchPad Hardware

## Quando usar

- P4 (display), P5 (áudio/HDR), P6 (rescue/bandeja/autostart).
- Qualquer código que chame `SetDisplayConfig`, Core Audio, COM de política de
  áudio, HDR ou Task Scheduler.
- Antes de pedir ao usuário para testar em hardware.

## Quando NÃO usar

- UI, tema, i18n ou simulação (use `couchpad-ui`).
- Análise estática do ShiftPad (use `couchpad-rea`).

## Invariantes de segurança

1. **Fail-closed:** se não for possível capturar o estado ou aplicar com
   verificação, não altere nada.
2. **Capturar antes:** topologia, display primário, HDR, endpoint, volume, mute.
3. **Aplicar com timeout** e verificar o resultado.
4. **Rollback automático** em qualquer falha.
5. **Journal atômico** antes da mutação; nunca apagar campos desconhecidos.
6. **Matar somente processo próprio**, identificado por PID + boot ID + hora de
   início. Nunca encerrar jogo, launcher de terceiro ou processo do sistema.
7. **Sem admin** por padrão; se precisar, justificar e pedir aprovação.
8. **Intervenção humana** quando o display sumir ou o áudio não voltar.

## Pré-voo (obrigatório)

- [ ] Monitor alternativo ou `Win+P` disponível.
- [ ] Estado atual anotado: topologia, resolução, escala, HDR, endpoint,
      volume, mute.
- [ ] Journal com escrita atômica implementado.
- [ ] Rollback testado com fake antes do hardware.
- [ ] Launchers desativados no primeiro teste.
- [ ] Teste começa com automação desligada.
- [ ] Log em JSON, sem PII.
- [ ] Você sabe como recuperar manualmente.

Se qualquer item faltar, **não execute**.

## Procedimento de teste

1. Registrar estado inicial (`stateBefore`).
2. Escrever journal com a fase e o estado.
3. Aplicar a mudança com timeout.
4. Verificar o resultado via API (não só pela aparência).
5. Registrar `stateAfter`.
6. Se falhar: rollback imediato e registrar `rollback`.
7. Se o rollback falhar: parar, preservar journal/log e pedir intervenção.

## Contrato de diagnóstico

Cada operação gera um JSON pequeno:

```json
{
  "ts": "2026-11-15T14:03:22.123Z",
  "phase": "display.apply",
  "api": "SetDisplayConfig",
  "result": "error",
  "errorCode": 87,
  "durationMs": 412,
  "stateBefore": { "topology": "extend", "primary": "DISPLAY1", "hdr": false },
  "stateAfter": { "topology": "extend", "primary": "DISPLAY1", "hdr": false },
  "rollback": "not_needed"
}
```

- Sem serial de monitor, IP, usuário ou caminho completo.
- Nome de display pode ser genérico (`DISPLAY1`, `TV`).
- Detalhes em `references/diagnostic-json.md`.

## Matriz de teste

Obrigatória antes de considerar P4–P6 concluídas. Detalhes em
`references/test-matrix.md`.

Cenários mínimos:

- um monitor; dois monitores; TV como único; estender; espelhar;
- HDR ligado/desligado; endpoint HDMI que aparece depois;
- desconectar durante a operação; crash; reboot;
- dois controles; combo; atrasos; launcher;
- rescue no logon; journal corrompido; journal ausente.

## Playbook de falha

### Tela preta / TV sem sinal

1. Não repetir a operação.
2. Preservar journal e log.
3. Tentar rollback.
4. Se falhar, orientar `Win+P` ou recuperação manual.
5. Só continuar após o usuário confirmar.

### Áudio não volta

1. Restaurar endpoint anterior.
2. Verificar endpoint padrão via API.
3. Se não voltar, orientar Configurações > Som.
4. Registrar como falha, não como sucesso.

### HDR errado

1. Reaplicar estado capturado.
2. Se não estabilizar, desligar HDR e registrar.
3. Não alternar repetidamente.

### Rescue em loop

1. Desabilitar a tarefa.
2. Preservar journal.
3. Corrigir a condição de “pendente”.
4. Reabilitar só após teste controlado.

### App travou

1. Encerrar somente o processo próprio identificado.
2. Verificar journal.
3. Restaurar estado.
4. Registrar.

## Definição de pronto

1. Matriz mínima executada.
2. Rollback comprovado.
3. Journal atômico comprovado.
4. Nenhum processo de terceiro encerrado.
5. Logs sem PII.
6. Ledger atualizado com evidências.
7. Usuário aceitou.

## Erros comuns

- Testar direto no hardware sem fake/rollback.
- Confiar só no visual, sem verificar via API.
- Não capturar volume/mute.
- Encerrar processo por nome, sem identidade.
- Deixar automação ligada no primeiro teste.
- Apagar journal “para limpar”.
- Repetir a operação após falha sem investigar.

## Referências

- `references/test-matrix.md` — cenários e critérios.
- `references/diagnostic-json.md` — schema do log.
- `couchpad-rea` — evidência de APIs do ShiftPad.
