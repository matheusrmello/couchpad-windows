# Checklist de release

## Código

- [ ] `dotnet clean` e `restore --locked-mode`.
- [ ] `dotnet build -c Release` sem avisos novos.
- [ ] `dotnet test -c Release` passa.
- [ ] Versão em `Directory.Build.props`/csproj atualizada.
- [ ] `CHANGELOG.md` atualizado.
- [ ] Tag criada e enviada.

## Qualidade

- [ ] Nenhuma string hardcoded.
- [ ] pt-BR e inglês completos.
- [ ] Contraste e foco verificados.
- [ ] Navegação por teclado.
- [ ] DPI 100/150/200.
- [ ] Screenshots dos 3 breakpoints × 2 idiomas.
- [ ] Nenhum TODO crítico em aberto.
- [ ] Nenhum segredo no repositório.

## Hardware

- [ ] Matriz mínima de `couchpad-hardware` executada.
- [ ] Rollback comprovado.
- [ ] Journal atômico comprovado.
- [ ] Rescue testado no logon.
- [ ] Nenhum processo de terceiro encerrado.

## Instalador

- [ ] Instalação per-user em `%LOCALAPPDATA%\Programs\CouchPad`.
- [ ] Atalho no Start Menu.
- [ ] Tarefa `CouchPadRescue` criada no logon.
- [ ] Upgrade preserva config/journal.
- [ ] Desinstalação remove tarefa e atalhos.
- [ ] Instalador idempotente.
- [ ] Assinatura aplicada (ou decisão registrada).

## Smoke test em máquina limpa

- [ ] Instalar.
- [ ] Abrir; automação desligada.
- [ ] Configurar TV e áudio.
- [ ] Conectar gamepad; tela muda.
- [ ] Áudio muda; volume/mute corretos.
- [ ] HDR reaplicado.
- [ ] Desconectar; tudo restaura.
- [ ] Reiniciar; rescue não entra em loop.
- [ ] Desinstalar; sistema limpo.

## Publicação

- [ ] Notas de release com limitações.
- [ ] Hash do instalador publicado.
- [ ] Link de download.
- [ ] Procedimento de rollback.
- [ ] Suporte/contato.

## Pós-release

- [ ] Monitorar falhas por 48h.
- [ ] Coletar logs sem PII.
- [ ] Registrar incidentes no ledger.
- [ ] Preparar patch se necessário.

## Assinatura

- [ ] Certificado válido e não expirado.
- [ ] `signtool verify /pa` no `.exe` e no instalador.
- [ ] Timestamp aplicado.
- [ ] Chave privada fora do repositório.
