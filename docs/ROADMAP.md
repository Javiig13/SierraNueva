# Hoja de ruta y criterios de aceptación

Estados usados: **Hecho** significa comprobado en local; **Parcial** significa
implementado pero sin toda la verificación exigida; **Pendiente acordado**
corresponde a la infraestructura que el propietario aplazó; **Pendiente**
requiere trabajo de producto.

## P0 — Entrega portable

- **Hecho:** estructura de solución, configuración, fixtures, datos y scripts.
- **Hecho:** documentación de arquitectura y operación local.
- **Hecho:** guía `AGENTS.md`, especificación consolidada y este handoff.
- **Hecho:** repositorio Git local con una baseline reproducible.

## P1 — Cerrar la baseline local

- **Hecho:** integración de pipeline contra un servidor HTTP local real,
  totalmente offline y basada en una fixture versionada.
- **Pendiente:** fixture PDF reducida dentro de `test-data/pdfs`.
- **Pendiente:** smoke test automatizado del directorio publicado.
- **Pendiente:** prueba E2E de navegador para filtros, detalle y mapa.
- **Pendiente:** auditoría básica responsive, teclado, contraste y lector.
- **Pendiente:** explicación estructurada de señales de `SourceConfidence`.
- **Pendiente:** decidir e implementar concurrencia real o retirar los ajustes
  que aparentan gobernarla.
- **Pendiente:** endurecer la resolución DNS frente a rebinding antes de una
  operación no supervisada.
- **Pendiente:** completar centroides solo con fuentes trazables.

## P2 — Incorporar cobertura real

- **Pendiente:** seleccionar con el propietario una primera fuente oficial.
- **Pendiente:** revisar aviso legal, condiciones, `robots.txt`, User-Agent y
  frecuencia antes de activarla.
- **Pendiente:** dry run limitado, revisión manual de evidencias y creación de
  fixtures sintéticas/reducidas para sus formatos.
- **Pendiente:** demostrar una ejecución live permitida sin degradar el último
  dataset válido.
- **Pendiente:** ampliar gradualmente cobertura y métricas de calidad.

## P3 — Preparación operativa local

- **Pendiente:** ensayar Playwright en una fuente autorizada que realmente lo
  necesite.
- **Pendiente:** ensayar caché y límites de Nominatim con identidad de contacto
  real, solo si hace falta.
- **Pendiente:** decidir si Leaflet se sirve localmente o desde CDN.
- **Pendiente:** documentar recuperación ante estado corrupto y rotación del
  histórico con una prueba de fallo.

## P4 — GitHub y hosting

No iniciar hasta que el propietario lo autorice y exista repositorio/slug
definitivo.

- **Pendiente acordado:** crear remoto y política de ramas.
- **Pendiente acordado:** `ci.yml` para restore, build, test, formato y
  configuración sin crawling live.
- **Pendiente acordado:** `crawl-and-deploy.yml` con `workflow_dispatch`,
  lunes/jueves 06:17 Madrid y push de código sin crawling accidental.
- **Pendiente acordado:** permisos mínimos, concurrencia, timeout, commits de
  `github-actions[bot]` y step summary.
- **Pendiente acordado:** Pages con acciones oficiales, artefacto estático,
  datos públicos y exclusión comprobada del estado.
- **Pendiente acordado:** configurar `base href` según el slug, `.nojekyll` y
  `404.html`.
- **Pendiente acordado:** documentar cambio de nombre, activación de Pages y
  actualización segura de acciones.
- **Pendiente acordado:** prueba real del URL publicado y rutas profundas.

## Matriz del encargo original

| # | Criterio | Estado | Evidencia o siguiente paso |
|---:|---|---|---|
| 1 | Compila en .NET 10 | Hecho | SDK fijado y build Release correcto |
| 2 | Todos los tests pasan | Hecho | 35/35 en la entrega |
| 3 | Crawler ejecutable localmente | Hecho | CLI y scripts |
| 4 | Crawler offline contra fixtures | Hecho | 4 promociones sintéticas |
| 5 | Fuente real permitida con Internet | Pendiente | Fase P2 |
| 6 | `promotions.json` válido | Hecho | `validate-data` |
| 7 | `promotions.csv` válido | Hecho | pruebas de persistencia |
| 8 | `promotions.geojson` válido | Hecho | pruebas y publicación |
| 9 | `run.json` | Hecho | salida versionada |
| 10 | `changes.json` | Hecho | salida versionada |
| 11 | Frontend carga archivos | Hecho | servicio y pruebas de componentes |
| 12 | Filtros funcionan | Hecho | modelo y componentes probados |
| 13 | Mapa funciona | Parcial | integración hecha; falta E2E real |
| 14 | Mapa y lista comparten filtro | Hecho | colección única en la UI |
| 15 | Ubicación exacta/aproximada | Hecho | contrato, UI y mapa |
| 16 | Enlaces a webs originales | Parcial | UI hecha; fixtures usan `.test` |
| 17 | Action manual | Pendiente acordado | Fase P4 |
| 18 | Action programada | Pendiente acordado | Fase P4 |
| 19 | Deploy Pages | Pendiente acordado | Fase P4 |
| 20 | Subpath del repositorio | Pendiente acordado | Requiere slug final |
| 21 | `.nojekyll` | Pendiente acordado | Fase P4 |
| 22 | Fallback SPA | Pendiente acordado | Fase P4 |
| 23 | Sin API keys obligatorias | Hecho | baseline offline |
| 24 | Sin secretos en el repo | Hecho | configuración no sensible |
| 25 | Portales excluidos bloqueados | Hecho | blocklist y pruebas |
| 26 | Fallo parcial no destruye dataset | Hecho | reglas y pruebas de estado |
| 27 | README permite ejecutar desde cero | Hecho | scripts y comandos manuales |
| 28 | Sin código esencial pendiente | Parcial | vertical local completa; ver P1/P2 |
| 29 | Repo limpio y estructurado | Hecho | monorepo y Git local |
| 30 | `dotnet test` ejecutado e informado | Hecho | 34/34 en la entrega |

## Fuera de esta hoja de ruta inmediata

Notificaciones, usuarios, administración web, API pública, base cloud,
aplicación móvil, OCR, IA, imágenes, hipotecas, inversión y comparación con
portales siguen fuera del MVP.
