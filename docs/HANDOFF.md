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

- Solución .NET 10 con cinco proyectos de producto y tres de pruebas.
- Paquetes centralizados, nullable, analizadores y advertencias como errores.
- Contrato público JSON 1.0 con promociones, ejecución, calidad, cambios,
  evidencias, configuración y enums serializados como texto.
- JSON determinista, CSV y GeoJSON; escritura temporal, reemplazo posterior y
  reintentos breves ante bloqueos transitorios de archivos en Windows.

### Crawler

- CLI `crawl`, `validate-config` y `validate-data` con opciones y códigos de
  salida documentados.
- Registro JSON de fuentes, 29 municipios editables y blocklist.
- Descubrimiento configurado, manual, sitemap/index y enlaces internos.
- Fuente de páginas por fixtures para ejecución completamente offline.
- Rastreo HTTP con validación de esquema/host/red, robots, demora, timeout,
  reintentos, límites, ETag y Last-Modified.
- Extracción por JSON-LD, metadatos, OpenGraph, HTML/patrones, selectores y PDF.
- Playwright aislado y condicionado como fallback.
- Geocodificación por centroide y Nominatim opcional con caché y rate limit.
- Normalización, identidad determinista, deduplicación conservadora, confianza,
  validación de calidad y change detection.
- Tres ausencias completas para desactivar; un fallo parcial no cuenta bajas.
- Conservación del último dataset válido si no hay resultados publicables.

### Datos y frontend

- Dataset sintético versionado con cuatro promociones y estado asociado.
- Tres centroides con procedencia; los no verificados permanecen nulos.
- SPA en español con estados de carga/error/vacío y avisos de frescura.
- Filtros completos, ordenaciones y query parameters compartibles.
- Una misma colección filtrada alimenta tarjetas y mapa.
- Detalle con datos, evidencias, cambios, advertencias y enlaces.
- Leaflet/OpenStreetMap mediante JS interop, marcadores por precisión y
  degradación al listado si falla el mapa.
- Salida `data/public` enlazada al `wwwroot/data` durante build/publish; el
  estado interno no se incluye.
- Scripts de ejecución local para PowerShell y Bash.

## Baseline comprobado

La última comprobación completa antes de esta entrega obtuvo:

```text
SDK:                 10.0.103
Build Release:       correcto, 0 advertencias, 0 errores
Tests Core:          12 correctos
Tests Infrastructure:18 correctos
Tests Web:           4 correctos
Total:               34/34 correctos
Formato:             sin cambios requeridos
validate-config:     1 fuente y 29 municipios válidos
Crawl offline:       éxito, 4 promociones de 4 páginas
validate-data:       correcto
Publish Web:         correcto; data/public incluido y data/state ausente
Script PowerShell:   comprobado con fixtures y sin Playwright
```

`data/public/run.json` conserva el último `runId` concreto. Una nueva ejecución
offline actualizará marcas temporales y normalmente informará cero altas
porque el estado sintético ya está sembrado.

## Lo que no está verificado o está incompleto

### Prioridad de producto

- No existe todavía ninguna fuente live habilitada y verificada. La única
  fuente activa es `fixtures-locales`.
- No se ha hecho una ejecución extremo a extremo contra una web real permitida.
- Playwright, Nominatim, ETag/Last-Modified y robots tienen implementación y
  pruebas aisladas, pero no una prueba operacional live.
- Solo hay tres centroides verificados de 29 municipios.
- La integración del pipeline usa un `IPageSource` en memoria, no un servidor
  HTTP local real como pedía el encargo original.
- La prueba de PDF genera el documento en memoria; `test-data/pdfs` no contiene
  aún una fixture binaria versionada.
- El procesamiento de fuentes es deliberadamente secuencial. Los ajustes de
  concurrencia se validan, pero aún no gobiernan un planificador paralelo real.
- El score de confianza implementa un subconjunto de señales y el contrato no
  expone todavía la explicación resumida solicitada.
- La defensa SSRF valida URLs y direcciones conocidas, pero una operación
  pública debería fijar/validar DNS en la conexión para mitigar DNS rebinding.
- `appsettings.Development.json` no se superpone automáticamente: la CLI carga
  el archivo indicado por `--config`.

### Frontend y validación visual

- Hay pruebas de componentes y del modelo de filtros, pero no una suite E2E en
  navegador real.
- No se ha realizado una auditoría formal de accesibilidad o responsive.
- Leaflet se carga desde CDN con versión fija; falta decidir si se vendoriza.
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
2. Cerrar las lagunas de pruebas locales: servidor HTTP real, fixture PDF,
   smoke del artefacto publicado y navegador E2E.
3. Completar y documentar el modelo de confianza con explicación de señales.
4. Verificar centroides restantes con procedencia reproducible.
5. Incorporar **una** fuente oficial real después de revisar URL, aviso legal,
   términos, robots, límites y selectores; empezar con `--dry-run --max-pages
   3 --verbose`.
6. Crear fixtures reducidas a partir del comportamiento observado, sin guardar
   HTML completo ni contenido no permitido.
7. Repetir con pocas fuentes y medir calidad antes de añadir concurrencia.
8. Solo cuando el propietario lo indique, ejecutar la fase GitHub/Pages
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
- Las fuentes reales están deshabilitadas para evitar crawling accidental.
- Los centroides nulos son preferibles a coordenadas inventadas.
- Playwright y Nominatim son fallbacks, no el camino principal.
- La lista sigue siendo plenamente útil si el mapa falla.
- La infraestructura espera al nombre y configuración final del repositorio.
