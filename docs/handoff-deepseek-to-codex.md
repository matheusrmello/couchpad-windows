# Handoff — P7a (DeepSeek) → P7b (Codex)

- Data: 2026-11-13
- Origem: P7a — a11y, i18n, screenshots, README e notas de release
- Destino: P7b — regressão final, instalador, assinatura e release v1.0.0
- Estado da P7a: **aceita pelo usuário em 13/11/2026**; P7b liberada
- Build: 0 avisos/0 erros
- Testes: 259 (232 Core + 27 App)
- Versão de desenvolvimento: 0.6.0
- Tag de hardware: `v0.6.0-hardware` (P6)
- Nenhuma tag de release criada

---

## 1. O que a P7a entregou

- Checklist de acessibilidade em `docs/ui-a11y-checklist.md`:
  - contraste AA, incluindo `DangerMuted #35191B` (4,62:1);
  - foco visível no ActionButton/Toggle/ComboBox/link;
  - ordem de foco alinhada à ordem visual nos 3 breakpoints;
  - UIAutomation confirmou ordem e nomes em pt/en e 3/2/1 colunas;
  - live regions em status, recuperação, startup, launcher e descoberta de
    display;
  - bandeja com headers localizados e tooltip via `app.name`.
- i18n: 157 chaves em paridade pt-BR/en; nenhuma string visível hardcoded.
- Seis capturas oficiais em `docs/screenshots/` (pt/en × 1020/780/640),
  simulação, versão 0.6.0.
- README, `docs/ui-spec.md`, `docs/release-notes-draft.md` e
  `docs/screenshots/README.md` atualizados.
- `Directory.Build.props` alinhado para `0.6.0`.
- Nenhuma alteração em `Platform.Windows` nesta fase.

### Pendências registradas (não bloquear P7b, se aceitas)

- Narrator/NVDA completo não executado; UIAutomation foi validado.
- Menu nativo da bandeja não foi percorrido por leitor de tela.
- Anúncio da contagem em atraso de 5 s pode ser verboso.
- Reconexão do watcher durante a contagem ainda carece de evidência
  independente.
- HDR e notificações nativas adiados.
- `SetDisplayConfig` travado não foi reproduzido (limitação aceita na P4).

---

## 2. O que o Codex deve fazer na P7b

1. **Aceite da P7a recebido em 13/11/2026.** Iniciar a P7b.
2. **Regressão final**:
   - build Debug/Release 0 avisos/0 erros;
   - `dotnet test CouchPad.slnx -c Debug` (259+);
   - smoke;
   - testes de processo do rescue/watchdog que já existem;
   - verificar que a P7a não alterou contratos do Core.
3. **Instalador**:
   - per-user, sem admin por padrão;
   - incluir App, `CouchPad.Rescue.exe`/DLL/deps/runtimeconfig, assets e
     localização;
   - não incluir notificações experimentais nem HDR;
   - registrar/remover a tarefa de logon e o autostart próprio no
     install/uninstall, com verificação;
   - atalhos e desinstalação limpa.
4. **Assinatura**: seguir a decisão registrada; se não houver certificado,
   documentar explicitamente.
5. **Release notes v1.0.0**:
   - substituir o rascunho por notas finais;
   - manter limitações e o aviso 100% free/sem telemetria;
   - não declarar HDR/notificações.
6. **Versão/tag**:
   - subir `Version` para `1.0.0`;
   - tag `v1.0.0`;
   - commit final.
7. **Smoke de instalação**:
   - instalar em usuário limpo, abrir, bandeja, autostart, rescue, desinstalar;
   - verificar ausência de admin/telemetria;
   - confirmar que o App abre em simulação por padrão;
   - confirmar que o rescue existe ao lado do App.
8. Parar para aceite do usuário; não iniciar HDR/notificações.

---

## 3. Contratos a preservar

- Não reescrever a UI; manter os nomes acessíveis, live regions e i18n.
- Não alterar `Platform.Windows` além de correções de release; se alterar,
  rodar a matriz de hardware relevante e registrar decisão.
- Manter `AllowDisplayWrites`/`AllowAudioWrites` desligados por padrão.
- Manter HDR e notificações desativados.
- Manter o watcher XInput opt-in e a simulação como padrão.
- Nenhuma licença, ativação, telemetria ou contato com servidor.
- Nenhum asset/código do ShiftPad.

---

## 4. Verificação rápida

```powershell
$dotnet = "C:\Users\Usuario\Documents\couchpad\.build\dotnet-sdk\dotnet.exe"

& $dotnet build CouchPad.slnx -c Debug
& $dotnet test CouchPad.slnx -c Debug
& $dotnet src\CouchPad.App\bin\Debug\net10.0\CouchPad.dll --smoke
```

- Smoke imprime uma linha e fecha.
- Screenshots oficiais: `docs/screenshots/`.
- Checklist: `docs/ui-a11y-checklist.md`.
- Fila: `docs/validation-queue.md`.
- Ledger: `HANDOFF.md`.

---

## 5. Próximo handoff

Após P7b, o Codex deve:

1. atualizar `HANDOFF.md` (ledger + evidência);
2. registrar decisões de instalador/assinatura em `docs/decisions.md`;
3. rodar a regressão final;
4. criar a tag `v1.0.0`;
5. escrever `docs/handoff-codex-to-deepseek.md` com o resultado do release;
6. parar para aceite do usuário.
