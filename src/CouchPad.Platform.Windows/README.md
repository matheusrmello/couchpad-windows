# CouchPad.Platform.Windows

Camada de integração com o Windows (P4–P6).

## Regra de fronteira

- Implementa display (CCD), áudio (Core Audio/endpoint padrão), HDR, rescue,
  bandeja, autostart e Task Scheduler.
- **Não** contém regra de produto, texto de UI ou decisão de fluxo.
- Expõe apenas as interfaces definidas em `CouchPad.Core`.
- Nenhuma implementação nesta fase (P0). O projeto existe para fixar a
  fronteira arquitetural e impedir que a UI cresça acoplada ao hardware.
