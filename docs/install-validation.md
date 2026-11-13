# Validação da distribuição v1.0.0

## Regressão de entrada

- Base `6bf07b5`, árvore limpa. O diff desde `v0.6.0-hardware` confirmou que a P7a preservou o Core, o backend Windows e o rescue.
- Em 13/11/2026, builds isolados Debug e Release com 0 avisos e 0 erros. Cada configuração passou 259 testes (232 Core e 27 App) e smoke.
- Passaram os testes de processo de startup e watchdog com filho falso e rescue com backend falso, incluindo concorrência, falhas, timeout, recuperação assistida e morte do processo pai.
- A UI aceita na P7a é a referência congelada desta distribuição.
