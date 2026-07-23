# Entrega y continuidad

Fecha de corte: **23 de julio de 2026**.

SierraNueva dispone de una vertical local completa y determinista: fixtures →
descubrimiento → extracción → normalización → geolocalización → cambios →
archivos públicos → SPA Blazor. Este documento separa lo comprobado de lo que
aún requiere trabajo para evitar que el siguiente agente dé por terminadas
integraciones que no se han ensayado en condiciones reales.

## Contexto de producto

La petición original incluía producto e infraestructura. El propietario decidió
terminar primero el repositorio y dejar GitHub, Actions, Pages, hosting y
operación remota para el final. No es un olvido: es una decisión explícita de
secuencia.

La SPA está hecha con **Blazor WebAssembly standalone sobre .NET 10**. Es
estática y compatible conceptualmente con GitHub Pages. Aún no está adaptada al
subpath de un repositorio ni tiene los archivos/workflows de Pages.

## Qué está implementado

### Solución y contratos

- Solución .NET 10 con cinco proyectos de producto y cuatro de pruebas.
- Paquetes centralizados, nullable, analizadores y advertencias como errores.
- Contrato público JSON 1.0 con promociones, ejecución, calidad, cambios,
  evidencias, configuración y enums serializados como texto.
- JSON determinista, CSV y GeoJSON; escritura temporal, reemplazo posterior y
  reintentos breves ante bloqueos transitorios de archivos en Windows.

### Crawler

- CLI `crawl`, `validate-config` y `validate-data` con opciones y códigos de
  salida documentados.
- Registro JSON de fuentes, 29 municipios editables y blocklist.
- Perfil predeterminado completamente offline y perfil live explícito y
  limitado para EXXACON — Living Natura.
- Descubrimiento configurado, manual, sitemap/index y enlaces internos.
- Fuente de páginas por fixtures para ejecución completamente offline.
- Prueba de integración del pipeline mediante un servidor HTTP real en
  loopback, con fixture versionada y sin acceso externo.
- Rastreo HTTP con validación de esquema/host/red, robots, demora, timeout,
  reintentos, límites, ETag y Last-Modified.
- Extracción por JSON-LD, metadatos, OpenGraph, HTML/patrones, selectores y PDF.
  Los municipios obtenidos mediante selector se normalizan contra el catálogo;
  las superficies admiten formatos decimales españoles y límites plausibles.
- Playwright aislado y condicionado como fallback.
- Geocodificación por centroide y Nominatim opcional con caché y rate limit.
- Normalización, identidad determinista, deduplicación conservadora, confianza,
  validación de calidad y change detection.
- Tres ausencias completas para desactivar; un fallo parcial no cuenta bajas.
- Conservación del último dataset válido si no hay resultados publicables.
- Dos backups atómicos del estado; lectura con fallback y fallo sin
  sobrescritura cuando todas las copias están corruptas.

### Datos y frontend

- Dataset sintético versionado con cuatro promociones y estado asociado.
- Los 29 centroides proceden del NGMEP 2026 del IGN; se conserva una fixture
  reducida, registro municipal, origen, hash del ZIP y atribución CC-BY 4.0.
  `validate-config` exige coincidencia entre coordenadas y procedencia.
- SPA en español con estados de carga/error/vacío y avisos de frescura.
- Filtros completos, ordenaciones y query parameters compartibles.
- Una misma colección filtrada alimenta tarjetas y mapa.
- Detalle con datos, evidencias, cambios, advertencias y enlaces.
- Explicación estructurada del score de confianza, con base y señales.
- Leaflet/OpenStreetMap mediante JS interop, marcadores por precisión y
  degradación al listado si falla el mapa. Leaflet 1.9.4 está vendorizado con
  su licencia.
- E2E en navegador real con Kestrel loopback, Leaflet local real y bloqueo de
  toda solicitud externa, incluidas teselas.
- Tabs responsive con flechas, skip link funcional, diálogo cerrable con
  Escape y comprobaciones de semántica y contraste.
- Salida `data/public` enlazada al `wwwroot/data` durante build/publish; el
  estado interno no se incluye y el publish lo comprueba automáticamente.
- Scripts de ejecución local para PowerShell y Bash.

## Baseline comprobado

La última comprobación completa antes de esta entrega obtuvo:

```text
SDK usado y fijado:  10.0.301
Build Release:       correcto, 0 advertencias, 0 errores
Tests Core:          13 correctos
Tests Infrastructure:35 correctos
Tests Web:           4 correctos
Tests Web E2E:       3 correctos
Total:               55/55 correctos
Formato:             sin cambios requeridos
validate-config:     1 fuente, 29 municipios y 29 centroides trazables
Crawl offline:       éxito, 4 promociones de 4 páginas
validate-data:       correcto
Publish Web:         smoke correcto; data/public incluido y data/state ausente
Live limitado:       EXXACON — Living Natura; 1 promoción válida, 0 fallos
```

`global.json` fija la feature band instalada `10.0.301` con `latestPatch`. Los
scripts locales ejecutan ahora la misma secuencia de build, tests, formato,
configuración, crawl, validación de datos y publish que esta entrega.

`data/public/run.json` conserva el último `runId` concreto. Una nueva ejecución
offline actualizará marcas temporales y normalmente informará cero altas
porque el estado sintético ya está sembrado.

## Lo que no está verificado o está incompleto

### Prioridad de producto

- La baseline sigue teniendo una única fuente activa, `fixtures-locales`. El
  perfil separado `sources.live.json` habilita manualmente solo
  `exxacon-living-natura`.
- El 23 de julio de 2026 se verificaron en vivo `robots.txt` y una página de
  EXXACON, primero con dry run y después con salida/estado aislados. Se obtuvo
  una promoción válida de Galapagar y `validate-data` fue correcto.
- La revisión y sus límites están en
  `docs/source-assessments/exxacon-living-natura.md`; no equivale a cobertura
  periódica ni garantiza que la web o sus condiciones no cambien.
- Playwright, Nominatim y ETag/Last-Modified tienen implementación y pruebas
  aisladas, pero no una prueba operacional live. La fuente incorporada no
  necesita Playwright ni Nominatim.
- El procesamiento es deliberadamente secuencial. Se retiraron los ajustes de
  concurrencia sin efecto para no prometer paralelismo.
- `appsettings.Development.json` no se superpone automáticamente: la CLI carga
  el archivo indicado por `--config`.

### Frontend y validación visual

- La auditoría automatizada cubre una baseline básica de accesibilidad, no una
  certificación WCAG ni pruebas con lectores de pantalla físicos.
- Los enlaces sintéticos usan el dominio reservado `.test`; el comportamiento
  de enlace está implementado, pero su destino no es navegable.

### Infraestructura aplazada

- No existe carpeta `.github` ni remotos configurados.
- Faltan CI, crawling programado, despliegue, permisos y resúmenes.
- Faltan `base href` de project site, `.nojekyll` y fallback `404.html`.
- No se ha decidido nombre/slug final del repositorio ni estrategia de estado
  a largo plazo.

## Próximo trabajo recomendado

1. Mantener verde la baseline offline.
2. Revalidar la evaluación de EXXACON antes de cada cambio operativo o
   automatización; conservar la salida y el estado live separados mientras se
   revisa su calidad.
3. Incorporar pocas fuentes oficiales adicionales, cada una con revisión,
   dry run y fixture sintética/reducida, y medir cobertura y falsos positivos.
4. Ensayar Playwright o Nominatim solo cuando una fuente autorizada realmente
   los necesite.
5. Solo cuando el propietario lo indique, ejecutar la fase GitHub/Pages
   descrita en `docs/ROADMAP.md`.

## Cómo retomar en otro equipo

1. Instalar el SDK indicado por `global.json`.
2. Abrir la raíz del repositorio, no un subproyecto.
3. Leer `AGENTS.md` y los documentos enlazados.
4. Ejecutar la baseline de `AGENTS.md` antes de cambiar código.
5. Comparar el nuevo resultado con `data/public/run.json`.
6. Trabajar en una prioridad concreta de `docs/ROADMAP.md` y actualizar su
   estado al terminar.

No hace falta Docker, npm, backend ni servicios externos para la baseline.

## Decisiones que no deben rehacerse sin motivo

- Persistencia JSON es deliberada por trazabilidad y portabilidad.
- El contrato público desacopla crawler, web y futuro hosting.
- Las fuentes reales están fuera del perfil predeterminado para evitar crawling
  accidental; el perfil live siempre requiere rutas explícitas en la CLI.
- Los centroides nulos son preferibles a coordenadas inventadas.
- Playwright y Nominatim son fallbacks, no el camino principal.
- La lista sigue siendo plenamente útil si el mapa falla.
- La infraestructura espera al nombre y configuración final del repositorio.
