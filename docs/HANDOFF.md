# Entrega y continuidad

Fecha de corte: **24 de julio de 2026**.

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

- CLI `crawl`, `validate-config`, `validate-data`, `discover-opportunities`,
  `backfill-opportunities`, `audit-opportunities`, `review-opportunity` y
  `coverage-status`, más `enrich-promotions` y `review-enrichment`, con
  opciones y códigos de salida documentados.
- Registro JSON de fuentes, 29 municipios editables y blocklist.
- Perfil predeterminado completamente offline y perfil live explícito con 22
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
- El texto HTML preserva límites entre nodos para que un contador de galería no
  se concatene con el número de viviendas. También reconoce rangos
  “desde/hasta”, “última vivienda” y la negación “no adosadas”.
- Las fichas live de una promoción fijan el municipio tras revisión y limitan
  el texto mediante un selector de contenido. Un cambio estructural hace fallar
  la fuente sin sustituir el último dataset válido.
- Playwright aislado y condicionado como fallback.
- Geocodificación por centroide y Nominatim opcional con caché y rate limit.
- Normalización, identidad determinista, deduplicación conservadora, confianza,
  validación de calidad y change detection.
- Tres ausencias completas para desactivar; un fallo parcial no cuenta bajas.
- Un timeout HTTP interno agotado se convierte en fallo de esa fuente, no en
  cancelación global. La integración offline conserva una segunda fuente
  válida y produce `PartialSuccess`; solo una cancelación externa detiene todo.
- Conservación del último dataset válido si no hay resultados publicables.
- Dos backups atómicos del estado; lectura con fallback y fallo sin
  sobrescritura cuando todas las copias están corruptas.
- Radar privado para BOCM, BOE, PCSP, Portal del Suelo y 28 fuentes
  municipales, con configuración offline/live separada, deduplicación y
  estados de revisión.
- Backfill BOCM por calendario oficial y sumario XML diario, con lotes de hasta
  367 días inclusivos y salto HTML → XML probado sin Internet.
- Planificador de backfill para rangos arbitrarios: exige fuente temporal y
  estado privado explícito, evita huecos/solapes y escribe un resumen atómico
  por lote sin borrar candidatos previos.
- Adaptador común de tablón `eAdmin` para Galapagar, Alpedrete, Los Molinos,
  Moralzarzal y San Lorenzo de El Escorial; extrae solo filas y enlaces de
  detalle, fija un municipio validado y no descarga adjuntos.
- Adaptador HTML acotado para las portadas públicas permitidas de 18 sedes
  `sedelectronica.es`; no accede a `/board`, conserva solo enlaces de vista
  previa y usa cookies públicas de sesión únicamente en memoria.
- Filtro de radar por municipio, señal y contexto inmobiliario, con exclusiones
  para ruido administrativo. Los candidatos viven únicamente en `data/state`.
- Registro privado de salud por fuente con último intento/éxito/fallo,
  respuestas vacías consecutivas, siguiente revisión prevista y escalado de
  degradación a fallo reiterado.
- Instantánea privada de cobertura para los 29 municipios, separando canal
  directo, central, combinado, degradado o no comprobado. `coverage-status`
  muestra solo agregados y puntos ciegos.
- Auditoría temporal privada que estratifica los 29 municipios por señales
  centrales, municipales y comerciales; prioriza señales de un solo canal,
  huecos y controles sin señal, sin exponer la cola ni fingir una estimación de
  exhaustividad.
- Descubrimiento comercial privado mediante 13 sitemaps de dominios oficiales
  ya aprobados. Se limita a HTTPS y hosts permitidos, reconoce municipio y
  señal desde la URL/títulos del sitemap y marca como verificadas las URLs que
  ya figuran en el registro de fuentes.
- Seguimiento HTML adicional para dos índices comerciales cuyo sitemap omite
  fichas conocidas. Usa selectores explícitos, una sola portada, cero
  recursión y la misma validación HTTPS/host.
- Puente privado de descubrimiento sectorial mediante SIMA: un sitemap
  residencial filtrado por los 29 municipios y dos índices acotados para
  Collado Villalba y El Escorial, donde los slugs no incluyen la localidad.
  Solo sigue fichas seleccionadas, conserva enlaces externos normalizados sin
  visitarlos y contabiliza dominios aún no monitorizados. El directorio nunca
  es fuente canónica ni publica candidatos.
- Reglas de revisión por URL que aplican `monitoring`, `rejected` o `stale` a
  candidatos nuevos o ya existentes sin poder otorgar `verifiedSource`.
- Triaje privado determinista de candidatos pendientes por señales, confianza,
  actualidad, evidencia geográfica y dominio, con penalización explicable de
  referencias históricas, documentos genéricos y listados excluidos, además de
  detección conservadora de duplicados por título y municipio. Solo genera un
  informe; no cambia decisiones humanas.
- Exportación manual cifrada del triaje desde la caché de Actions, con clave
  RSA-3072 efímera local, AAD independiente, artefacto de un día y borrado del
  texto claro en el runner.
- Radar web privado mediante SearXNG efímero: cuatro familias por cada uno de
  los 29 municipios, 116 consultas diarias completas, API JSON local,
  deduplicación por URL y exclusión de portales/redes. Los resultados parten de
  confianza reducida, viven solo en la cola privada y requieren fuente oficial.
- Descarga temporal acotada para los ZIP mensuales de PCSP y dos backups
  atómicos adicionales para la cola de oportunidades.
- CI offline en GitHub Actions y workflow manual/diario de crawl y despliegue,
  con acciones fijadas, permisos mínimos, concurrencia y resumen de ejecución.
  El workflow actualizado ejecuta primero el radar, un backfill móvil de 31
  días cada lunes y una auditoría diaria de diez municipios. Conserva todo el
  estado y los informes en la caché privada, sin publicar candidatos.
- Preparación de Pages con base `/SierraNueva/`, `.nojekyll`, fallback
  `404.html` y rechazo explícito de cualquier estado privado.
- Enriquecimiento opcional por OpenAI Responses API sobre evidencia oficial
  acotada. Genera propuestas privadas con esquema estricto, cita literal, URL,
  confianza, hash y caché; sin clave no se ejecuta y el producto normal no la
  necesita. Una propuesta solo pasa al crawl tras aceptación humana, no
  sobrescribe datos deterministas y caduca a los 30 días.
- El perfil de coste usa Luna sin razonamiento, verbosidad baja, `store=false`,
  tres llamadas, tres páginas, 8.000 caracteres, 800 tokens de salida y 0,05
  USD como máximos predeterminados. Comprueba un límite superior antes de
  enviar, registra uso y coste en estado privado y aplica la caché antes del
  límite de llamadas. `--dry-run` ya no invoca la API.
- Actions solo puede ejecutar un piloto IA mediante despacho manual explícito
  de una a tres promociones. El cron diario lo omite incluso si existe el
  secreto `OPENAI_API_KEY`. Un resultado parcial del piloto se informa sin
  bloquear Pages ni impedir la conservación de su estado privado.
- La fuente efímera de evidencia exige cuerpo completo y deshabilita solo los
  validadores HTTP condicionales; evita que el `304` del crawl anterior deje
  una página sin texto, conservando robots, host, demora y límites.
- `review-enrichment` muestra la cola pendiente y genera opcionalmente un
  informe HTML local dentro de `--state`. La revisión es campo a campo, sin
  aceptación masiva: una propuesta solo queda resuelta cuando todos sus campos
  tienen decisión, y el crawl aplica únicamente los aceptados. El esquema
  privado 1.2 conserva la lectura y aplicación de aceptaciones 1.1.
- `export-private-enrichment.yml` permite recuperar esa cola desde la caché de
  Actions sin hacerla pública. Un par RSA-3072 efímero se crea localmente; el
  runner usa RSA-OAEP-SHA256 + AES-256-GCM, sube solo el sobre autenticado
  durante un día y elimina el texto claro. El descifrado local valida JSON,
  escribe atómicamente y borra opcionalmente la clave privada.
- `review-private-enrichment.yml` devuelve solo identificadores, campos y
  decisiones a la caché; nunca reenvía valores, citas ni URLs. Usa
  `review-enrichment --quiet`, valida hasta 100 decisiones y comparte
  concurrencia con el crawl antes de guardar una nueva caché privada.
- El prompt distingue explícitamente viviendas totales/disponibles,
  `priceFrom`/`priceTo` y régimen/nombre de cooperativa. La primera cola real
  recuperada contenía cuatro aciertos y cuatro falsos positivos; todos quedaron
  revisados campo a campo sin publicar el estado.
- La ejecución `30100286300` verificó la exportación cifrada real y produjo un
  único artefacto de 5,5 KB cuyo SHA-256 coincidió tras la descarga. La
  ejecución `30101016895` aplicó 4 aceptaciones y 4 rechazos, dejó 0 campos
  pendientes y guardó `crawler-state-30101016895` (29 KB). `upload-artifact`
  quedó actualizado y fijado a v6/Node 24 después del aviso observado en la
  primera exportación.

### Datos y frontend

- Dataset sintético versionado con cuatro promociones y estado asociado.
- Los 29 centroides proceden del NGMEP 2026 del IGN; se conserva una fixture
  reducida, registro municipal, origen, hash del ZIP y atribución CC-BY 4.0.
  `validate-config` exige coincidencia entre coordenadas y procedencia.
- SPA en español con estados de carga/error/vacío y avisos de frescura.
- Interfaz de consulta compacta: cabecera de 50 px, resumen operativo inferior
  a 90 px en lugar del hero, lienzo fluido hasta 1.980 px, filtros completos en
  barra/panel avanzado, ordenaciones y query parameters compartibles.
- Una misma colección filtrada alimenta tarjetas y mapa. Los marcadores muestran
  el precio y el hover/foco destaca bidireccionalmente marcador y tarjeta. Las
  opciones sin precio son puntos compactos y las coordenadas coincidentes se
  separan solo visualmente para que ninguna quede oculta.
- El resaltado conserva por separado el origen tarjeta y el origen mapa. Esto
  evita que un `mouseleave` tardío de Blazor borre el `mouseover` del marcador
  durante la transición entre ambos elementos.
- Las tarjetas omiten atributos vacíos y muestran el porcentaje de campos
  principales realmente publicados; no se rellenan guiones ni estimaciones.
- Detalle con datos, evidencias, cambios, advertencias y enlaces.
- Explicación estructurada del score de confianza, con base y señales.
- Leaflet/OpenStreetMap mediante JS interop, marcadores por precisión y
  degradación al listado si falla el mapa. Leaflet 1.9.4 está vendorizado con
  su licencia.
- E2E en navegador real con Kestrel loopback, Leaflet local real y bloqueo de
  toda solicitud externa, incluidas teselas; cubre también los marcadores de
  precio y su enlace visual con las tarjetas.
- Tabs responsive con flechas, skip link funcional, diálogo cerrable con
  Escape y comprobaciones de semántica y contraste.
- Salida `data/public` enlazada al `wwwroot/data` durante build/publish; el
  estado interno no se incluye y el publish lo comprueba automáticamente.
- Las cuatro lecturas públicas añaden un token común por arranque para evitar
  que el navegador reutilice JSON o GeoJSON de un despliegue anterior.
- Scripts de ejecución local para PowerShell y Bash.

## Baseline comprobado

La última comprobación completa antes de esta entrega obtuvo:

```text
SDK usado y fijado:  10.0.301
Build Release:       correcto, 0 advertencias, 0 errores
Tests Core:          24 correctos
Tests Infrastructure:107 correctos
Tests Web:           5 correctos
Tests Web E2E:       3 correctos
Total:               139/139 correctos
Formato:             sin cambios requeridos
validate-config:     1 fuente, 29 municipios, 29 centroides y 34 fuentes de radar
Crawl offline:       éxito, 4 promociones de 4 páginas
validate-data:       correcto
Dry-run IA offline:  2 llamadas planificadas, 0 llamadas reales, 0 USD
Publish Web:         smoke correcto; data/public incluido y data/state ausente
Live limitado local: 22 fuentes; 22 promociones válidas y activas, 0 fallos
Mapa live local:     22/22 promociones; 21 centroides municipales y 1 exacta
Radar offline:       34 candidatos; 34/34 fuentes sanas; cobertura 29/29
Matriz web offline:  1 candidato filtrado; sin red ni Docker
Matriz web GitHub:   567 resultados; 340 candidatos; 116/116 consultas
Triaje offline:      34/34 pendientes clasificados; estado original intacto
Triaje GitHub inicial:334 pendientes; 30 alta, 230 media, 70 baja, 4 duplicados
BOCM live aislado:   68 entradas, 0 fallos y 0 candidatos el 2026-07-23
Tablones live:       335 entradas, 0 fallos y 0 candidatos el 2026-07-23
Portadas sede live:  37 entradas, 0 fallos y 0 candidatos el 2026-07-23
Fuentes nuevas live: 20 entradas en la cuarta cohorte, 0 fallos y 0 candidatos
Sitemaps live:       839 URLs; 13/13 sanos; 12 candidatos nuevos y 3 conocidos
Enlaces live:        12 enlaces; 2/2 sanos; 2 conocidos y 0 pendientes
SIMA live aislado:   3/3 sanas; Orbia y Nevia nuevas; 1 dominio no monitorizado
Radar live local:    50/50 sanas; 29/29 municipios; 7 pendientes y 1 dominio nuevo
Candidatos revisados: 0 nuevos; 2 en seguimiento; 0 dominios sin monitorizar
Backfill BOCM live:  1.909 entradas; 1/1 lote y 0 candidatos (24 jun–24 jul)
Radar live conjunto: éxito parcial; PCSP recibió HTML del WAF en lugar de ZIP
CI GitHub real:      correcto en 30119641350 para el commit 46bc036
Crawl/deploy GitHub: correcto en 30119685200, intento #1
Radar GitHub real:   50 sanas, 1 degradada, 0 en fallo; 29/29, 27 directas
Cola privada real:   334 candidatos pendientes
Pages real:          correcto; 22 promociones, 22/22 fuentes y 0 fallos
Run live actual:     20260724T191619295Z; 22 puntos GeoJSON
Estado IA web:       correcto; promotion-enrichment.json devuelve 404
Workflow P5 real:    backfill y auditoría correctos en 30086510831
Estado privado web:  correcto; data/state/opportunity-audit.json devuelve 404
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
  perfil separado `sources.live.json` habilita explícitamente 22 fuentes
  limitadas y no se ejecuta por accidente.
- El perfil del radar suma 51 fuentes con `zz-web-search-matrix`. La baseline
  usa una respuesta SearXNG reducida y la matriz live queda fijada en 116
  consultas. GitHub Actions `30119685200` verificó la imagen real, `/healthz`,
  las 116 consultas y la destrucción del contenedor: SearXNG devolvió 567
  resultados y el filtro creó 340 candidatos. El daemon Docker local no estaba
  iniciado, por lo que ese mismo smoke no se repitió en la máquina de
  desarrollo.
- La exportación cifrada del triaje y su separación criptográfica están
  verificadas offline y en la ejecución GitHub `30121191738`: 334 candidatos
  pendientes de 183 dominios, 30 en prioridad alta, 230 media, 70 baja y 4
  duplicados probables. El artefacto descargado coincidió con su SHA-256 y se
  descifró/validó localmente; la clave privada efímera fue eliminada.
- La primera distribución mostró agregadores y resultados geográficamente
  débiles. `globaliza.com`, `housage.es`, `mitula.com`, `nestoria.es`,
  `nuevosvecinos.com`, `nuroa.es`, `properstar.es` y `terrenos.es` quedan
  excluidos de búsquedas futuras y rechazados por regla reproducible. Las
  reglas nuevas se aplican también al estado pendiente preexistente.
- El CI `30121125358` descubrió una intermitencia Linux en el hover sintético
  del marcador del mapa. La prueba ahora despacha el evento de Leaflet de forma
  determinista y superó cinco repeticiones locales consecutivas. Falta
  confirmar el siguiente CI Linux junto con el triaje recalibrado.
- El 24 de julio de 2026 se revisaron identidad, condiciones, `robots.txt`,
  acceso y vigencia, se ejecutaron dry runs individuales y una ejecución
  conjunta con salida/estado aislados. Las primeras 21 fuentes terminaron sin
  fallos, se obtuvieron 21 promociones válidas y `validate-data` fue
  correcto.
- La búsqueda cubrió sistemáticamente los 29 municipios. La fotografía ofrece
  fuente en 16 (55,2 %); los otros 13 constan con candidato descartado o
  ausencia de candidata apta en `docs/source-coverage.md`.
- La segunda ampliación añade Antaro — Prado de Noria y Los Trigales, Grupo
  Index — Sierra Bonita y tres cooperativas de Vesari. La ficha de Luar conserva
  una frase residual contradictoria, documentada, mientras título, URL,
  descripción y listado oficial coinciden en Robledo de Chavela.
- La tercera ampliación añade La Bellota en Alpedrete y C/ Pradillos en
  Moralzarzal. Hirimasa mezcla varias promociones en una página: tres selectores
  obligatorios aíslan la ficha y cualquier cambio estructural falla cerrado.
  La señal visual de última vivienda se genera por CSS y no se publica como
  estado comercial porque el rastreo HTML no puede revalidarla.
- La cuarta ampliación añade Cumbres de Navalafuente, Claveles y Osnola en
  Zarzalejo, Essentia en Galapagar y Montemilano en Bustarviejo. Montemilano
  excluye los formularios porque sus bandas de inversión no son precios; la
  fixture comprueba que el precio queda nulo.
- La quinta ampliación añade Névola Homes en Guadalix de la Sierra: 16 chalets,
  14 pareados y dos independientes, cuatro dormitorios, dos plazas y parcelas
  de 310 a 319 m². La prueba y el dry run live aislado son correctos. El bloque
  de pagos queda fuera para no publicar los 5.000 € de reserva como precio.
  Abantos Home se descartó por ser plurifamiliar.
- La ejecución conjunta aislada `20260724T151724214Z` terminó con 22/22 fuentes,
  22 promociones activas y cero fallos; `validate-data` fue correcto. Névola
  conservó precio nulo y extrajo 16 unidades, cuatro dormitorios, dos plazas,
  parcelas 310–319 m² y ambas tipologías.
- La primera ejecución de la matriz, `30118808511`, descubrió una discrepancia
  de orquestación: el código `1` documentado para `PartialSuccess` se trataba
  como fallo fatal aunque existieran datos publicables. `46bc036` acepta solo
  los códigos `0` y `1`, mantiene `validate-data` como barrera y continúa
  fallando ante estados graves. La ejecución completa posterior
  `30119685200` terminó verde con 22/22 fuentes comerciales; la rama live de
  `PartialSuccess` sigue cubierta por la integración offline, pero no volvió a
  darse en esa segunda ejecución.
- Las tablas Nuvare motivaron correcciones para no concatenar columnas como
  precios, distinguir unidades totales y disponibles y admitir millares en
  parcelas. Osnola añade la variante explícita “licencia obtenida”.
- Vesari respondió HTTP 429 al probar cinco, diez y finalmente veinte segundos
  exactos entre fichas. La configuración usa ahora treinta segundos, una
  prueba impide reducir el mínimo y la repetición conjunta 21/21 fue correcta.
- La ejecución GitHub `30047521921` reveló que un timeout de Gilmar en el último
  reintento se propagaba como cancelación global. El timeout live se elevó de
  30 a 60 segundos y el crawler distingue ahora el agotamiento interno de una
  cancelación externa; la prueba de integración demuestra que las demás
  fuentes continúan y el resultado queda parcial, nunca falsamente completo.
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
- La cuarta cohorte añade la actualidad oficial de Collado Villalba y los RSS
  de Guadalix de la Sierra y Navalafuente. Procesó 20 entradas live sin fallos
  ni candidatos y eleva la vigilancia municipal directa a 28/29 (96,6 %).
- El orquestador de backfill se comprobó offline con dos lotes contiguos entre
  2024 y 2025 y live, sobre estado aislado, del 24 de junio al 24 de julio de
  2026: BOCM entregó 1.909 entradas, 1/1 lote terminó correcto y el filtro no
  produjo candidatos. Los informes quedaron fuera de Git.
- El radar genera ahora una fotografía operativa: la baseline offline obtiene
  29/29 municipios con vigilancia sana, 28 con canal directo y Robledo de
  Chavela como cobertura exclusivamente central. Esto mide ejecución y puntos
  ciegos; no convierte candidatos en promociones ni garantiza disponibilidad.
- Trece sitemaps comerciales oficiales amplían el catálogo live a 45 fuentes.
  El smoke aislado procesó 839 URLs sin fallos, encontró 15 coincidencias y
  separó tres URLs ya conocidas de 12 candidatos pendientes. Entre estos
  últimos aparece una ficha específica de STANCE en Torrelodones y varias
  páginas municipales de obra nueva que todavía no demuestran una promoción.
- El lector sigue ahora índices `sitemapindex` con límites de profundidad,
  documentos, HTTPS y host. Siete fuentes conocidas se migraron a sus índices
  con filtros de sub-sitemap; los smokes aislados terminaron 7/7 sanos y
  procesaron 745 URLs en total. Esto detecta particiones futuras sin abrir
  blogs, autores o formularios.
- La fotografía de cobertura incorpora recuentos por estado del embudo,
  canales y dominios comerciales sanos y municipios con señal comercial. Son
  métricas de observación, no un porcentaje inventado de exhaustividad.
- La primera ejecución integrada en GitHub, `30054208393`, recorrió las 45
  fuentes en 3 min 40 s. Encontró 17 coincidencias: tres correspondían a URLs
  comerciales conocidas y 14 quedaron pendientes en la cola privada. PCSP y
  Collado Mediano aportaron una coincidencia cada uno; los sitemaps aportaron
  las quince restantes.
- Cinco canales municipales quedaron degradados en esa ejecución: las portadas
  de El Boalo, El Escorial, Guadarrama y Torrelodones respondieron HTTP 503, y
  el tablón de Los Molinos respondió HTTP 403. Las otras 40 fuentes terminaron
  sanas. La redundancia central mantuvo 29/29 municipios vigilados, aunque solo
  23 tuvieron canal municipal directo sano en esa fotografía.
- La repetición individual posterior recuperó los cinco canales sin cambios:
  El Boalo, El Escorial, Guadarrama y Torrelodones respondieron HTTP 200 con
  tres entradas cada uno; Los Molinos respondió HTTP 200 con 19 entradas.
- Los 14 candidatos pendientes quedaron clasificados de forma reproducible:
  cuatro `monitoring`, ocho `rejected` y dos `stale`. Las tres coincidencias
  conocidas conservan `verifiedSource`; el estado privado no se publica.
- La comparación entre registro y sitemaps detectó dos omisiones aptas para
  seguimiento HTML: Puerta de Villalba en Apremya y Etria en Trinosa. Los dos
  índices añadidos procesaron 12 enlaces y solo reprodujeron esas dos fuentes
  conocidas, sin crear pendientes.
- Dos respuestas vacías consecutivas tras observar datos degradan una fuente.
  El primer fallo también degrada y el segundo consecutivo marca fallo
  reiterado; una recuperación limpia ambos contadores. La prueba usa un lector
  secuenciado completamente offline.
- Robledo de Chavela sigue sin canal municipal directo: el RSS devuelve 403 al
  cliente identificado, la sede anterior declara estar inactiva y la nueva
  sede es una aplicación JavaScript sin avisos en el HTML. No se evade el
  filtro; BOCM, BOE y PCSP mantienen cobertura central.
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

- El rediseño compacto del 24 de julio de 2026 se comprobó en navegador real:
  la segunda iteración concentra métricas y frescura en una franja de unos
  63 px, adelanta el mapa aproximadamente al píxel 271 en una ventana de
  1280×720 y aprovecha prácticamente todo el ancho. No añade recursos visuales
  ni dependencias externas.
- El E2E fija una regresión máxima de 90 px para el resumen superior y mantiene
  las comprobaciones a 390×844 para tabs, teclado y ausencia de overflow.
- Una prueba unitaria comprueba que promociones, cambios, informe y GeoJSON
  usan el mismo token de versión y no quedan mezclados entre despliegues.
- La auditoría automatizada cubre una baseline básica de accesibilidad, no una
  certificación WCAG ni pruebas con lectores de pantalla físicos.
- Los enlaces sintéticos usan el dominio reservado `.test`; el comportamiento
  de enlace está implementado, pero su destino no es navegable.

### Infraestructura y hosting

- Existe el remoto público `origin` en
  `https://github.com/Javiig13/SierraNueva.git`; `main` conserva todo el
  historial y sigue `origin/main`.
- CI #20 se ejecutó para `ecf8602` y terminó correctamente.
- El workflow live se ejecuta manualmente o cada día a las 06:17
  `Europe/Madrid`. Publica solo tras éxito completo de las 22 fuentes
  revisadas; un fallo conserva el último despliegue válido.
- El workflow actualizado ejecuta antes `discover-opportunities` con las 50
  fuentes live y escribe salud/cobertura solo bajo `.runtime/state`. Un fallo
  del radar queda visible y no bloquea el último dataset comercial válido. La
  ejecución real `30054208393` verificó el aislamiento: el paso del radar
  informó resultado `failure` por cinco canales degradados, pero el job
  completó 21/21 fuentes comerciales, publicó el artefacto y desplegó Pages.
- La ejecución integral `30077326296` comprobó por primera vez 47/47 fuentes
  sanas, 29/29 municipios y 28 canales directos. Encontró además un falso
  positivo nuevo en El Boalo por el sentido laboral de “promoción interna”;
  quedó rechazado mediante su URL exacta.
- La ejecución final `30078678411` verificó que la cola privada quedó en cuatro
  candidatos, todos ya clasificados como `monitoring`. La fotografía terminó
  con 44 fuentes sanas y tres degradadas: Apremya y STANCE por errores TLS
  transitorios, y Los Molinos por HTTP 403. La repetición aislada inmediata
  recuperó las tres sin cambiar URL, selector ni cabeceras: 4, 163 y 19
  entradas respectivamente. No se oculta esta variabilidad ni se intenta
  evadirla.
- La ejecución `30081195579` sobre `9e90c84` verificó en GitHub los dos pasos
  P5. El backfill móvil BOCM procesó 1.909 entradas en 1/1 lote y la auditoría
  generó una muestra 10/29: ocho municipios con señal de un solo canal, dos
  huecos de cobertura y 21 controles sin señal. El job completo terminó
  correcto en 9 min 17 s, con 21/21 fuentes comerciales, cuatro candidatos
  pendientes y Pages desplegado. El informe privado devolvió HTTP 404 en la
  URL pública.
- La ejecución `30086510831` sobre `9808d0f` publicó el rediseño compacto. El
  primer intento conservó Pages al obtener 20/21 fuentes comerciales; un único
  reintento, sin cambiar código ni configuración, completó 21/21 y desplegó en
  9 min 29 s. El radar quedó visible como fallo parcial con 46 fuentes sanas,
  cero degradadas y una en fallo reiterado; backfill y auditoría terminaron
  correctamente.
- La ejecución `30089518646` sobre `102a7b5` publicó la segunda compactación y
  el pipeline privado de enriquecimiento al primer intento. Crawl y Pages
  terminaron correctos con 21/21 fuentes, 21 promociones, 17 activas y 21
  puntos GeoJSON (`runId` `20260724T113101930Z`). La CI
  `30089475710` del mismo commit también fue correcta. Las rutas
  `data/state/promotion-enrichment.json`, `data/promotion-enrichment.json` y
  `data/state/opportunity-audit.json` devolvieron HTTP 404.
- La ejecución `30105161699` sobre `8c2bd2f` publicó la quinta ampliación:
  22/22 fuentes comerciales, 22 promociones, 19 activas, cero fallos y run
  `20260724T152946510Z`. Névola aparece en lista con 16 unidades, ambas
  tipologías, cuatro dormitorios y parcela 310–319 m²; el precio sigue como no
  publicado. El radar conservó 29/29 municipios, 27 canales directos sanos y
  cuatro candidatos pendientes; solo `tablon-los-molinos` quedó degradado por
  HTTP 403. Backfill y auditoría terminaron correctos, la IA se omitió y el
  estado privado devolvió HTTP 404. CI `30104995452` también fue correcta.
- El 24 de julio se validó localmente el nuevo embudo SIMA. Su sitemap siguió
  tres fichas ya gobernadas por reglas y los índices municipales detectaron
  `Orbia` en Collado Villalba y `Nevia` en El Escorial, ambos como candidatos
  privados `new`. La ficha de Nevia expuso `aurora-homes.es`; la instantánea
  contó un dominio referido y no monitorizado. No se visitó el dominio, no se
  alteró `data/public` y la evaluación jurídica/técnica está en
  `docs/source-assessments/sima-discovery-2026-07-24.md`.
- La ejecución integrada local posterior terminó con 50/50 fuentes sanas,
  29/29 municipios vigilados, 28 canales municipales directos y 15/15 canales
  comerciales sanos. El embudo quedó en tres candidatos `new`, cuatro
  `monitoring`, cinco `verifiedSource`, doce `rejected` y dos `stale`. Además
  de Orbia y Nevia apareció una señal oficial del Portal del Suelo sobre ocho
  parcelas residenciales en Miraflores de la Sierra. Los siete pendientes
  siguen solo en el estado aislado `.runtime`; no se versionaron ni publicaron.
- La revisión posterior de esos siete candidatos resolvió cinco falsos
  positivos del embudo. Orbia —fuente canónica AEDAS Homes— y Nevia —Aurora
  Homes— son promociones plurifamiliares y quedan fuera del alcance
  unifamiliar; Nevia declara además adjudicación al 100 %. Las tres páginas
  territoriales NUVARE reutilizan el dossier y la distribución de Cumbres de
  Navalafuente, por lo que no representan promociones independientes. Las
  cinco URLs quedan `rejected`. El estudio de detalle de Collado Mediano y la
  licitación de ocho parcelas municipales en Los Pinarejos permanecen
  `monitoring`.
- La repetición live privada `op-20260724T173446775Z` aplicó las reglas también
  al estado existente: 50/50 fuentes sanas, 29/29 municipios, cero candidatos
  nuevos, dos en seguimiento y cero dominios referidos sin monitorizar. No se
  modificaron `sources.live.json`, `data/public` ni `data/state`. La evidencia
  está en
  `docs/source-assessments/candidate-review-2026-07-24.md`.
- `Crawl and deploy` `30111181981` sobre `7882251` terminó correcto en 11 min
  18 s: 22/22 fuentes comerciales, 22 promociones, cero fallos, IA omitida y
  Pages desplegado. Los tres canales SIMA quedaron sanos. El radar conservó
  aislamiento al recibir HTTP 403 únicamente en `tablon-los-molinos`: 49/50
  fuentes sanas, 29/29 municipios, 27 canales directos y seis pendientes.
  `CI 30111091606` detectó además una carrera intermitente tarjeta → marcador
  que no apareció en Windows; la corrección separa ambos orígenes de resaltado
  y pasó cinco repeticiones E2E y la suite local completa de 134 pruebas.
- El primer piloto OpenAI real fue `30093553895` sobre `1273a73`. El secreto
  funcionó y Responses devolvió HTTP 200 para dos promociones: 3.175 tokens de
  entrada, 412 de salida, nueve campos propuestos y 0,006439 USD estimados,
  frente a una reserva máxima de 0,022264 USD. Una ficha adicional recibió
  `304` del caché HTTP y elevó el resultado a parcial; el job se detuvo pese a
  conservar las propuestas. La corrección posterior fuerza cuerpo completo y
  hace que el paso opcional no bloquee Pages. Ninguna propuesta fue aceptada.
- La repetición `30094391984` sobre `5bb9b4e` validó la corrección de extremo a
  extremo: workflow correcto en 9 min 43 s, dos promociones procesadas, ocho
  campos propuestos, cero fallos y Pages desplegado. Consumió 3.567 tokens de
  entrada y 409 de salida, con 0,006911 USD estimados frente a una reserva
  máxima de 0,022815 USD. El coste acumulado estimado de ambos pilotos es
  0,013350 USD. Ninguna propuesta fue aceptada ni publicada.
- El workflow aplica el fallback local de centroides municipales con Nominatim
  deshabilitado y exige que todas las promociones publicadas estén presentes
  en GeoJSON. El smoke live aislado `20260723T224152829Z` confirmó 21/21
  puntos: veinte centroides municipales y uno exacto.
- Al cargar estado histórico, el pipeline completa coordenadas ausentes antes
  de conservar promociones no modificadas (por ejemplo, tras HTTP 304). Una
  prueba de integración cubre esta migración sin borrar estado ni historial.
- El estado live se restaura mediante caché privada de Actions y nunca se
  incorpora al artefacto ni se confirma en Git.
- Pages usa GitHub Actions como fuente. La ejecución manual
  `30086510831` completó el crawl, validó las 21 fuentes, publicó 21
  promociones y desplegó correctamente
  `https://javiig13.github.io/SierraNueva/`.
- Se comprobó en navegador la portada y el mapa. La vista por defecto muestra
  17 promociones comerciales activas y el resumen conserva las 21. El JSON
  público contiene 21 promociones, el GeoJSON 21 elementos, el `runId` es
  `20260724T105210752Z` y el estado privado devuelve 404.
- La protección de rama sigue sin configurar.

## Próximo trabajo recomendado

1. Mantener en observación el estudio de detalle de Collado Mediano y la
   licitación de ocho parcelas municipales de Los Pinarejos. Solo promover una
   ficha cuando aparezcan promotora, inventario y comercialización oficial.
2. Vigilar la variabilidad de Apremya, STANCE y Los Molinos: conservar la
   redundancia y no tratar un 403 o un fallo TLS aislado como autorización
   para evadir controles.
3. Revisar las muestras privadas generadas y registrar evidencia independiente
   antes de convertir cualquier discrepancia en una nueva fuente o promoción.
4. Mantener verde la baseline offline y vigilar el backfill móvil semanal.
5. Revalidar las evaluaciones antes de cada cambio operativo o
   automatización; conservar la salida y el estado live separados.
6. Revalidar PCSP y mantener la fuente como fallo parcial mientras el endpoint
   oficial devuelva la página WAF en vez del ZIP.
7. Resolver Robledo de Chavela solo cuando exista un canal municipal público
   apto para `HttpClient` o se justifique un adaptador JavaScript revisable.
8. Ampliar la ventana histórica BOCM solo cuando aporte una pregunta concreta
   de producto y siempre sobre un estado privado aislado.
9. Mantener la matriz municipal: reevaluar descartes solo cuando aparezca una
   ficha oficial vigente o se corrija la carencia documentada.
10. Ensayar Playwright o Nominatim solo cuando una fuente revisada realmente
   los necesite.
11. Recuperar la cola privada del piloto `30094391984` en un entorno local,
    generar el informe con `review-enrichment --report` y decidir sus ocho
    campos uno a uno tras comprobar cada cita. Contrastar además el coste
    estimado acumulado de 0,013350 USD con el panel de OpenAI. No habilitar
    todavía una programación periódica de IA. El secreto de GitHub quedó
    confirmado por HTTP 200; la clave no está cargada ni se expone en la
    terminal local.
12. Definir la protección y política de ramas cuando el propietario decida el
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
- El repositorio remoto, la automatización diaria y GitHub Pages ya están
  configurados y verificados; cualquier infraestructura adicional requiere una
  decisión operativa explícita.
