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
- **Hecho:** fixture PDF sintética y reducida dentro de `test-data/pdfs`,
  comprobada con PdfPig y el extractor por capas.
- **Hecho:** smoke automatizado del directorio publicado, incluida la exclusión
  de `data/state`.
- **Hecho:** E2E en navegador real para filtros, detalle, mapa y URL
  compartible; todo tráfico externo se aborta antes de salir.
- **Hecho:** rediseño responsive con mapa dominante, precios sobre marcadores,
  filtros compactos/avanzados y resaltado bidireccional entre mapa y tarjetas.
- **Hecho:** auditoría básica responsive, teclado, contraste y semántica de
  lector, incluida navegación por flechas en tabs y cierre de diálogo con
  Escape.
- **Hecho:** explicación estructurada de señales de `SourceConfidence` en
  contrato y ficha.
- **Hecho:** retirada de ajustes de concurrencia que no gobernaban el pipeline;
  el recorrido secuencial queda explícito.
- **Hecho:** resolución DNS validada y fijada por conexión frente a rebinding.
- **Hecho:** 29 centroides derivados del NGMEP 2026 del IGN, con fixture
  reducida, licencia CC-BY 4.0, hash del ZIP, registro de origen y validación
  automática.

## P2 — Incorporar cobertura real

- **Hecho:** seleccionada con autorización del propietario la fuente corporativa
  oficial EXXACON — Living Natura, promoción unifamiliar en Galapagar.
- **Hecho:** identidad, condiciones, `robots.txt`, User-Agent, acceso y
  frecuencia revisados y documentados antes de crear el perfil live.
- **Hecho:** dry run de una página, revisión manual del contrato y fixture
  sintética reducida para municipio, superficies, dormitorios y disponibilidad.
- **Hecho:** ejecución live limitada y `validate-data` correctos usando salida y
  estado aislados, sin modificar `data/public` ni `data/state`.
- **Hecho:** búsqueda sistemática y matriz de decisión para los 29 municipios,
  con fuente, falso positivo o motivo de descarte fechados.
- **Hecho:** siete fuentes adicionales revisadas e incorporadas; el perfil live
  suma 8 fuentes, 8 promociones y 7 municipios con cobertura.
- **Hecho:** municipio fijo revisado y selector de contenido que falla de forma
  cerrada para evitar contaminación por navegación o promociones relacionadas.
- **Hecho:** siete fixtures reducidas y pruebas offline para los formatos
  nuevos, incluidos rangos, habitaciones, licencia solicitada y
  precomercialización.
- **Hecho:** dry runs por fuente, ejecución live conjunta aislada y
  `validate-data` correctos: 8/8 fuentes y 8/8 promociones válidas.
- **Hecho:** métricas de la fotografía live: 24,1 % de municipios con al menos
  una fuente, 75 % de promociones con precio y 100 % de fuentes/promociones
  finales válidas. La matriz no promete cobertura exhaustiva del mercado.

## P3 — Preparación operativa local

- **Hecho:** radar privado de oportunidades con perfiles offline/live
  separados para BOCM, BOE, PCSP y Portal del Suelo 4.0.
- **Hecho:** adaptadores RSS, JSON BOE, Atom/ZIP y HTML acotado, cada uno con
  fixture y pruebas sin Internet.
- **Hecho:** filtro por los 29 municipios, señal, contexto inmobiliario y
  exclusiones; la prueba incluye ruido administrativo que no debe pasar.
- **Hecho:** identidad estable, deduplicación, estados de revisión y escritura
  atómica con dos backups exclusivamente bajo `data/state`.
- **Hecho:** smoke live aislado de los cuatro canales: 68 entradas BOCM, 184
  BOE, 16.815 PCSP y 26 bloques del Portal del Suelo; cero fallos y un candidato
  final después del filtrado.
- **Hecho:** backfill oficial BOCM mediante calendario diario y sumario XML,
  con intervalo acotado, fixture y prueba del salto HTML → XML.
- **Hecho:** primera cohorte municipal `eAdmin` para Galapagar, Alpedrete, Los
  Molinos, Moralzarzal y San Lorenzo de El Escorial; 335 entradas live
  procesadas sin fallos y sin candidatos el día de revisión.
- **Hecho:** descarte de la familia `sedelectronica.es/board` donde
  `robots.txt` prohíbe `/board`; no se fuerza ni se evita esa restricción.
- **Hecho:** segunda cohorte municipal basada exclusivamente en las portadas
  permitidas de 13 sedes `sedelectronica.es`; 37 entradas live, cero fallos y
  cero candidatos el día de revisión. Las cookies de sesión necesarias para
  sus redirecciones son efímeras y exclusivas del cliente del radar.
- **Hecho:** tercera cohorte con cinco portadas oficiales adicionales,
  Bustarviejo por tablón de transparencia y Cercedilla por RSS municipal.
  Los smokes individuales procesaron 65 entradas live, cero fallos y un
  candidato; la cobertura municipal alcanza 25/29.
- **Pendiente:** integrar Collado Villalba, Guadalix de la Sierra, Navalafuente
  y Robledo de Chavela. Las sedes observadas requieren JavaScript o sesión, o
  están inactivas; cualquier adaptador nuevo exige fixture y prueba offline.
- **Pendiente:** revalidar PCSP: el último smoke recibió una denegación WAF en
  HTML con HTTP 200. El lector la rechaza explícitamente como no-ZIP.

- **Pendiente:** ensayar Playwright en una fuente autorizada que realmente lo
  necesite.
- **Pendiente:** ensayar caché y límites de Nominatim con identidad de contacto
  real, solo si hace falta.
- **Hecho:** Leaflet 1.9.4 se sirve desde el propio artefacto con licencia
  BSD-2-Clause; el E2E usa la librería real sin depender de CDN.
- **Hecho:** dos copias atómicas del estado, recuperación ordenada, advertencia
  operativa y fallo seguro probado cuando todas las copias están corruptas.

## P4 — GitHub y hosting

El propietario autorizó el 23 de julio de 2026 el repositorio, Actions, la
ejecución diaria, GitHub Pages y el cambio de visibilidad a público.

- **Hecho:** repositorio público `Javiig13/SierraNueva`, remoto `origin`,
  historial completo y `main` siguiendo `origin/main`.
- **Pendiente:** definir protección y política de ramas.
- **Hecho:** `ci.yml` reproduce restore, build, tests, formato, configuración,
  radar/crawl de fixtures, validación y publish sin crawling live.
- **Hecho:** primera ejecución real de CI correcta en GitHub para `9690959`
  (2 min 36 s).
- **Hecho:** `crawl-and-deploy.yml` con `workflow_dispatch` y ejecución diaria
  a las 06:17 `Europe/Madrid`, sin crawling provocado por cada push.
- **Hecho:** permisos mínimos, concurrencia, timeout, caché privada y step
  summary; las acciones están fijadas por SHA.
- **Hecho:** artefacto Pages con datos live solo tras éxito completo y
  comprobación de ausencia de `data/state`.
- **Hecho:** `base href` `/SierraNueva/`, `.nojekyll` y `404.html` preparados y
  comprobados localmente.
- **Hecho:** Pages activado con GitHub Actions como fuente; ejecución manual
  `30033934500` correcta y SPA disponible en
  `https://javiig13.github.io/SierraNueva/`.
- **Hecho:** portada, mapa, filtro directo `?q=Galapagar`, dataset live de ocho
  promociones y exclusión pública de `data/state` comprobados.

## Matriz del encargo original

| # | Criterio | Estado | Evidencia o siguiente paso |
|---:|---|---|---|
| 1 | Compila en .NET 10 | Hecho | SDK fijado y build Release correcto |
| 2 | Todos los tests pasan | Hecho | 82/82 en la entrega |
| 3 | Crawler ejecutable localmente | Hecho | CLI y scripts |
| 4 | Crawler offline contra fixtures | Hecho | 4 promociones sintéticas |
| 5 | Fuente real permitida con Internet | Hecho | 8 fuentes revisadas, perfil manual limitado |
| 6 | `promotions.json` válido | Hecho | `validate-data` |
| 7 | `promotions.csv` válido | Hecho | pruebas de persistencia |
| 8 | `promotions.geojson` válido | Hecho | pruebas y publicación |
| 9 | `run.json` | Hecho | salida versionada |
| 10 | `changes.json` | Hecho | salida versionada |
| 11 | Frontend carga archivos | Hecho | servicio y pruebas de componentes |
| 12 | Filtros funcionan | Hecho | modelo y componentes probados |
| 13 | Mapa funciona | Hecho | E2E real y offline con lista/filtro compartido |
| 14 | Mapa y lista comparten filtro | Hecho | colección única en la UI |
| 15 | Ubicación exacta/aproximada | Hecho | contrato, UI y mapa |
| 16 | Enlaces a webs originales | Parcial | UI hecha; fixtures usan `.test` |
| 17 | Action manual | Hecho | `workflow_dispatch` validado por actionlint |
| 18 | Action programada | Hecho | diaria 06:17 `Europe/Madrid` |
| 19 | Deploy Pages | Hecho | ejecución `30033934500` y URL pública verificadas |
| 20 | Subpath del repositorio | Hecho | `/SierraNueva/` en artefacto |
| 21 | `.nojekyll` | Hecho | generado y verificado |
| 22 | Fallback SPA | Hecho | `404.html` generado y verificado |
| 23 | Sin API keys obligatorias | Hecho | baseline offline |
| 24 | Sin secretos en el repo | Hecho | configuración no sensible |
| 25 | Portales excluidos bloqueados | Hecho | blocklist y pruebas |
| 26 | Fallo parcial no destruye dataset | Hecho | reglas y pruebas de estado |
| 27 | README permite ejecutar desde cero | Hecho | scripts y comandos manuales |
| 28 | Sin código esencial pendiente | Hecho | vertical local, cobertura P1/P2 y radar central completos; ampliación municipal incremental |
| 29 | Repo limpio y estructurado | Hecho | monorepo y Git local |
| 30 | `dotnet test` ejecutado e informado | Hecho | 82/82 en la entrega |

## Fuera de esta hoja de ruta inmediata

Notificaciones, usuarios, administración web, API pública, base cloud,
aplicación móvil, OCR, IA, imágenes, hipotecas, inversión y comparación con
portales siguen fuera del MVP.
