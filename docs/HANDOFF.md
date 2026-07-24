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
- Perfil predeterminado completamente offline y perfil live explícito con 21
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
- Reglas de revisión por URL que aplican `monitoring`, `rejected` o `stale` a
  candidatos nuevos o ya existentes sin poder otorgar `verifiedSource`.
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
Tests Core:          21 correctos
Tests Infrastructure:90 correctos
Tests Web:           5 correctos
Tests Web E2E:       3 correctos
Total:               119/119 correctos
Formato:             sin cambios requeridos
validate-config:     1 fuente, 29 municipios, 29 centroides y 33 fuentes de radar
Crawl offline:       éxito, 4 promociones de 4 páginas
validate-data:       correcto
Publish Web:         smoke correcto; data/public incluido y data/state ausente
Live limitado:       21 fuentes; 21 promociones válidas, 0 fallos
Mapa live:           21/21 promociones; 20 centroides municipales y 1 exacta
Radar offline:       33 candidatos; 33/33 fuentes sanas; cobertura 29/29
BOCM live aislado:   68 entradas, 0 fallos y 0 candidatos el 2026-07-23
Tablones live:       335 entradas, 0 fallos y 0 candidatos el 2026-07-23
Portadas sede live:  37 entradas, 0 fallos y 0 candidatos el 2026-07-23
Fuentes nuevas live: 20 entradas en la cuarta cohorte, 0 fallos y 0 candidatos
Sitemaps live:       839 URLs; 13/13 sanos; 12 candidatos nuevos y 3 conocidos
Enlaces live:        12 enlaces; 2/2 sanos; 2 conocidos y 0 pendientes
Backfill BOCM live:  1.909 entradas; 1/1 lote y 0 candidatos (24 jun–24 jul)
Radar live conjunto: éxito parcial; PCSP recibió HTML del WAF en lugar de ZIP
CI GitHub real:      correcto en 1 min 57 s para el commit ba2a186
Crawl/deploy GitHub: correcto en 30086510831, intento #2 (9 min 29 s)
Radar GitHub real:   46 sanas, 0 degradadas, 1 en fallo; 29/29, 27 directas
Cola privada real:   4 candidatos pendientes
Pages real:          correcto; 21 promociones, 21/21 fuentes y 0 fallos
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
  perfil separado `sources.live.json` habilita explícitamente 21 fuentes
  limitadas y no se ejecuta por accidente.
- El 24 de julio de 2026 se revisaron identidad, condiciones, `robots.txt`,
  acceso y vigencia, se ejecutaron dry runs individuales y una ejecución
  conjunta con salida/estado aislados. Las 21 fuentes terminaron sin fallos,
  se obtuvieron 21 promociones válidas y `validate-data` fue
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
  `Europe/Madrid`. Publica solo tras éxito completo de las 21 fuentes
  revisadas; un fallo conserva el último despliegue válido.
- El workflow actualizado ejecuta antes `discover-opportunities` con las 47
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

1. Mantener en observación el estudio de detalle de Collado Mediano y las tres
   páginas NUVARE; solo promover una ficha si aparece evidencia independiente
   de promoción vigente.
2. Vigilar la variabilidad de Apremya, STANCE y Los Molinos: conservar la
   redundancia y no tratar un 403 o un fallo TLS aislado como autorización
   para evadir controles.
3. Revisar las muestras privadas generadas y registrar evidencia independiente
   antes de convertir cualquier discrepancia en una nueva fuente o promoción.
4. Mantener verde la baseline offline y vigilar el backfill móvil semanal.
5. Revalidar las 21 evaluaciones antes de cada cambio operativo o
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
11. Configurar `OPENAI_API_KEY` solo en un entorno privado, ejecutar
    `enrich-promotions` sobre una cohorte pequeña, revisar cada cita y medir
    precisión/coste antes de cualquier programación. No aceptar propuestas en
    bloque.
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
