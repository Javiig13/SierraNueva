# Entrega y continuidad

Fecha de corte: **23 de julio de 2026**.

SierraNueva dispone de una vertical local completa y determinista: fixtures →
descubrimiento → extracción → normalización → geolocalización → cambios →
archivos públicos → SPA Blazor. Este documento separa lo comprobado de lo que
aún requiere trabajo para evitar que el siguiente agente dé por terminadas
integraciones que no se han ensayado en condiciones reales.

También existe un radar administrativo paralelo: genera candidatos privados a
partir de fuentes oficiales, pero nunca los publica como promociones.

## Contexto de producto

La petición original incluía producto e infraestructura. Tras completar la
fase local, el propietario autorizó el 23 de julio de 2026 crear y subir el
repositorio `Javiig13/SierraNueva`, añadir Actions, programar el rastreo diario
y publicar la SPA en GitHub Pages.

La SPA está hecha con **Blazor WebAssembly standalone sobre .NET 10**. Es
estática, se adapta al subpath `/SierraNueva/` y está publicada en
`https://javiig13.github.io/SierraNueva/`.

## Qué está implementado

### Solución y contratos

- Solución .NET 10 con cinco proyectos de producto y cuatro de pruebas.
- Paquetes centralizados, nullable, analizadores y advertencias como errores.
- Contrato público JSON 1.0 con promociones, ejecución, calidad, cambios,
  evidencias, configuración y enums serializados como texto.
- JSON determinista, CSV y GeoJSON; escritura temporal, reemplazo posterior y
  reintentos breves ante bloqueos transitorios de archivos en Windows.

### Crawler

- CLI `crawl`, `validate-config`, `validate-data`, `discover-opportunities` y
  `review-opportunity` con opciones y códigos de salida documentados.
- Registro JSON de fuentes, 29 municipios editables y blocklist.
- Perfil predeterminado completamente offline y perfil live explícito con ocho
  fuentes revisadas, una URL y una página por fuente.
- Descubrimiento configurado, manual, sitemap/index y enlaces internos.
- Fuente de páginas por fixtures para ejecución completamente offline.
- Prueba de integración del pipeline mediante un servidor HTTP real en
  loopback, con fixture versionada y sin acceso externo.
- Rastreo HTTP con validación de esquema/host/red, robots, demora, timeout,
  reintentos, límites, ETag y Last-Modified.
- Extracción por JSON-LD, metadatos, OpenGraph, HTML/patrones, selectores y PDF.
  Los municipios obtenidos mediante selector se normalizan contra el catálogo;
  las superficies admiten formatos decimales españoles y límites plausibles.
- Las fichas live de una promoción fijan el municipio tras revisión y limitan
  el texto mediante un selector de contenido. Un cambio estructural hace fallar
  la fuente sin sustituir el último dataset válido.
- Playwright aislado y condicionado como fallback.
- Geocodificación por centroide y Nominatim opcional con caché y rate limit.
- Normalización, identidad determinista, deduplicación conservadora, confianza,
  validación de calidad y change detection.
- Tres ausencias completas para desactivar; un fallo parcial no cuenta bajas.
- Conservación del último dataset válido si no hay resultados publicables.
- Dos backups atómicos del estado; lectura con fallback y fallo sin
  sobrescritura cuando todas las copias están corruptas.
- Radar privado para BOCM, BOE, PCSP, Portal del Suelo y 25 fuentes
  municipales, con configuración offline/live separada, deduplicación y
  estados de revisión.
- Backfill BOCM por calendario oficial y sumario XML diario, con lotes de hasta
  367 días inclusivos y salto HTML → XML probado sin Internet.
- Adaptador común de tablón `eAdmin` para Galapagar, Alpedrete, Los Molinos,
  Moralzarzal y San Lorenzo de El Escorial; extrae solo filas y enlaces de
  detalle, fija un municipio validado y no descarga adjuntos.
- Adaptador HTML acotado para las portadas públicas permitidas de 18 sedes
  `sedelectronica.es`; no accede a `/board`, conserva solo enlaces de vista
  previa y usa cookies públicas de sesión únicamente en memoria.
- Filtro de radar por municipio, señal y contexto inmobiliario, con exclusiones
  para ruido administrativo. Los candidatos viven únicamente en `data/state`.
- Descarga temporal acotada para los ZIP mensuales de PCSP y dos backups
  atómicos adicionales para la cola de oportunidades.
- CI offline en GitHub Actions y workflow manual/diario de crawl y despliegue,
  con acciones fijadas, permisos mínimos, concurrencia y resumen de ejecución.
- Preparación de Pages con base `/SierraNueva/`, `.nojekyll`, fallback
  `404.html` y rechazo explícito de cualquier estado privado.

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
Tests Infrastructure:62 correctos
Tests Web:           4 correctos
Tests Web E2E:       3 correctos
Total:               82/82 correctos
Formato:             sin cambios requeridos
validate-config:     1 fuente, 29 municipios, 29 centroides y 29 fuentes de radar
Crawl offline:       éxito, 4 promociones de 4 páginas
validate-data:       correcto
Publish Web:         smoke correcto; data/public incluido y data/state ausente
Live limitado:       8 fuentes; 8 promociones válidas, 0 fallos
Radar offline:       29 candidatos de fixtures; 29/29 fuentes
BOCM live aislado:   68 entradas, 0 fallos y 0 candidatos el 2026-07-23
Tablones live:       335 entradas, 0 fallos y 0 candidatos el 2026-07-23
Portadas sede live:  37 entradas, 0 fallos y 0 candidatos el 2026-07-23
Fuentes nuevas live: 65 entradas, 0 fallos y 1 candidato el 2026-07-23
Radar live conjunto: éxito parcial; PCSP recibió HTML del WAF en lugar de ZIP
CI GitHub real:      correcto en 1 min 38 s para el commit fe252db
Crawl/deploy GitHub: correcto en la ejecución 30033934500
Pages real:          correcto; 8 promociones, 8/8 fuentes y 0 fallos
Estado privado web:  correcto; data/state/promotions-state.json devuelve 404
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
  perfil separado `sources.live.json` habilita manualmente ocho fuentes
  limitadas y no se ejecuta por accidente.
- El 23 de julio de 2026 se revisaron identidad, condiciones, `robots.txt`,
  acceso y vigencia, se ejecutaron dry runs individuales y una ejecución
  conjunta con salida/estado aislados. Las ocho fuentes terminaron sin fallos,
  se obtuvieron ocho promociones válidas y `validate-data` fue correcto.
- La búsqueda cubrió sistemáticamente los 29 municipios. La fotografía ofrece
  fuente en 7 (24,1 %); los otros 22 constan con candidato descartado o ausencia
  de candidata apta en `docs/source-coverage.md`.
- Las evaluaciones están en `docs/source-assessments`; no equivalen a cobertura
  periódica ni garantizan que las webs, disponibilidad o condiciones no
  cambien.
- El radar reduce puntos ciegos, pero no garantiza exhaustividad. El smoke
  original procesó 68 entradas BOCM, 184 BOE, 16.815 PCSP y 26 bloques del
  Portal del Suelo; tras corregir falsos positivos quedó un candidato para
  revisión.
- BOCM ya dispone de backfill oficial por calendario y sumario XML. La primera
  cohorte de cinco tablones `eAdmin` procesó 335 entradas sin fallos. Una
  segunda cohorte aprovecha solo la portada explícitamente permitida por
  `robots.txt` de 13 sedes `sedelectronica.es`: procesó 37 entradas sin fallos.
  Una tercera cohorte añadió cinco portadas del mismo formato, el tablón de
  transparencia de Bustarviejo y el RSS oficial de Cercedilla. Sus smokes
  individuales procesaron 65 entradas sin fallos y elevaron la vigilancia
  municipal a 25/29 (86,2 %).
- Esas portadas muestran solo los dos o tres anuncios más recientes. El
  histórico `/board` sigue prohibido y no se consulta; por tanto, la ampliación
  reduce el punto ciego pero no ofrece exhaustividad histórica.
- En la repetición del smoke conjunto PCSP respondió HTTP 200 con HTML de
  denegación WAF en vez del ZIP. El lector lo informa ahora explícitamente y
  conserva el resto como éxito parcial; no se intenta sortear la protección.
- Un candidato del radar no demuestra disponibilidad comercial. Debe resolverse
  hasta una web oficial vigente y seguir la evaluación técnica/jurídica normal
  antes de incorporarse a `sources.live.json`.
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

### Infraestructura y hosting

- Existe el remoto público `origin` en
  `https://github.com/Javiig13/SierraNueva.git`; `main` conserva todo el
  historial y sigue `origin/main`.
- CI ya se ejecutó realmente en GitHub y terminó correctamente.
- El workflow live se ejecuta manualmente o cada día a las 06:17
  `Europe/Madrid`. Publica solo tras éxito completo de las ocho fuentes
  revisadas; un fallo conserva el último despliegue válido.
- El estado live se restaura mediante caché privada de Actions y nunca se
  incorpora al artefacto ni se confirma en Git.
- Pages usa GitHub Actions como fuente. La ejecución manual
  `30033934500` completó el crawl, validó las ocho fuentes, generó el artefacto
  y desplegó correctamente `https://javiig13.github.io/SierraNueva/`.
- Se comprobó en navegador la portada, el mapa y la URL directa
  `?q=Galapagar`; también se verificó que el estado privado devuelve 404.
- La protección de rama sigue sin configurar.

## Próximo trabajo recomendado

1. Mantener verde la baseline offline.
2. Revalidar las ocho evaluaciones antes de cada cambio operativo o
   automatización; conservar la salida y el estado live separados.
3. Revalidar PCSP y mantener la fuente como fallo parcial mientras el endpoint
   oficial devuelva la página WAF en vez del ZIP.
4. Resolver los cuatro ayuntamientos restantes por formatos reutilizables:
   Collado Villalba, Guadalix de la Sierra, Navalafuente y Robledo de Chavela.
5. Ejecutar el histórico BOCM en lotes anuales solo cuando se decida la ventana
   operativa y siempre sobre estado privado aislado.
6. Mantener la matriz municipal: reevaluar descartes solo cuando aparezca una
   ficha oficial vigente o se corrija la carencia documentada.
7. Ensayar Playwright o Nominatim solo cuando una fuente revisada realmente
   los necesite.
8. Definir la protección y política de ramas cuando el propietario decida el
   flujo de contribución.

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
- El repositorio remoto ya está fijado; la automatización y el hosting siguen
  requiriendo una decisión operativa explícita.
