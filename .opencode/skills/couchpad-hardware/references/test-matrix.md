# Matriz de teste de hardware

## Legenda

- **Pré:** pré-condição.
- **Ação:** o que fazer.
- **Esperado:** resultado observável.
- **Rollback:** o que deve acontecer em falha.
- **Evidência:** o que registrar.

## Display

### D01 — Um monitor, sem TV

- Pré: apenas monitor.
- Ação: conectar gamepad.
- Esperado: nenhuma mudança de topologia; estado “TV não encontrada”.
- Rollback: não aplicável.
- Evidência: JSON `display.detect`, topologia inalterada.

### D02 — Dois monitores, TV como alvo

- Pré: monitor + TV, modo estender.
- Ação: conectar gamepad.
- Esperado: TV vira alvo conforme modo; monitor preservado.
- Rollback: topologia anterior.
- Evidência: `stateBefore`, `stateAfter`, código de retorno.

### D03 — TV como único display

- Pré: TV ativa.
- Ação: conectar/desconectar.
- Esperado: aplica/restaura; sem tela preta.
- Rollback: volta ao monitor.
- Evidência: topologia + confirmação visual.

### D04 — Estender

- Ação: ativar modo estender.
- Esperado: TV como extensão; posição consistente.
- Rollback: topologia anterior.

### D05 — Espelhar

- Ação: ativar modo clone.
- Esperado: mesma imagem; resolução compatível.
- Rollback: topologia anterior.
- Observação: registrar se o modo é suportado.

### D06 — Desconectar durante a aplicação

- Ação: remover HDMI no meio do `SetDisplayConfig`.
- Esperado: erro tratado; rollback; sem travar.
- Rollback: topologia anterior.

### D07 — DPI/escala

- Pré: 100%, 150%, 200%.
- Ação: aplicar e restaurar.
- Esperado: escala preservada ou restaurada.
- Evidência: valores antes/depois.

## HDR

### H01 — HDR ligado antes

- Ação: trocar topologia.
- Esperado: HDR reaplicado no alvo.
- Rollback: estado anterior.

### H02 — HDR desligado

- Ação: trocar topologia.
- Esperado: permanece desligado.
- Evidência: `advancedColor` antes/depois.

### H03 — TV sem suporte a HDR

- Ação: conectar.
- Esperado: não força HDR; estado claro.
- Rollback: não aplicável.

## Áudio

### A01 — HDMI disponível antes

- Ação: conectar gamepad.
- Esperado: endpoint HDMI vira padrão; volume/mute capturados.
- Rollback: endpoint anterior.

### A02 — HDMI aparece depois

- Ação: conectar gamepad com TV desligada e ligar depois.
- Esperado: retry até o endpoint aparecer; timeout definido.
- Evidência: tentativas, tempo, resultado.

### A03 — Restauração no disconnect

- Ação: desconectar gamepad.
- Esperado: endpoint, volume e mute anteriores.
- Rollback: se falhar, registrar e orientar.

### A04 — Múltiplos endpoints HDMI

- Ação: conectar com dois candidatos.
- Esperado: escolha determinística/documentada.
- Evidência: lista e critério.

## Controles

### C01 — Dois controles iguais

- Ação: conectar dois; definir primário.
- Esperado: apenas o primário dispara.
- Evidência: identidade estável.

### C02 — Combo de ativação

- Ação: sequência correta e incorreta.
- Esperado: só a correta ativa; timeout limpa.
- Evidência: sequência e tempo.

### C03 — Atraso e cancelamento

- Ação: ativar com atraso; cancelar pela bandeja.
- Esperado: nenhuma mudança após cancelar.
- Evidência: timestamps.

## Launcher

### L01 — Steam Big Picture

- Ação: ativar/desativar.
- Esperado: abre/fecha somente o processo próprio.
- Evidência: PID + boot ID + hora de início.

### L02 — Launcher customizado

- Ação: caminho válido e inválido.
- Esperado: erro claro no inválido.

## Rescue

### R01 — Journal pendente

- Pré: journal de sessão interrompida.
- Ação: logon.
- Esperado: restaura topologia e limpa journal.
- Evidência: log do rescue + estado final.

### R02 — Journal ausente

- Ação: logon sem journal.
- Esperado: nenhuma ação; sem loop.

### R03 — Journal corrompido

- Ação: arquivo inválido.
- Esperado: não restaura; preserva o arquivo; registra erro.

### R04 — Duas sessões

- Ação: dois usuários/sessões.
- Esperado: exclusão mútua; sem corrida.
- Evidência: lock e logs.

## Critério de conclusão

Cada cenário deve ter:

- JSON de diagnóstico;
- resultado observado;
- rollback (quando aplicável);
- confirmação do usuário;
- risco residual registrado.
