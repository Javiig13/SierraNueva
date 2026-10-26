# Entrega y continuidad

Fecha de corte: **26 de octubre de 2026**.

La recuperación operativa de octubre se documenta al final. Los recuentos live
de julio son fotografías históricas, no acreditan continuidad hasta hoy.

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
- Registro JSON de fuentes, 179 municipios editables y blocklist.
- Perfil predeterminado completamente offline y perfil live explícito con 48
  definiciones revisadas: 46 automatizadas y Miraflores Villas/Hirimasa
  retenidas como comprobación manual por bloqueos reiterados.
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
- Instantánea privada de cobertura para los 179 municipios, separando canal
  directo, central, combinado, degradado o no comprobado. `coverage-status`
  muestra solo agregados y puntos ciegos.
- Auditoría temporal privada que estratifica los 179 municipios por señales
  centrales, municipales y comerciales; prioriza señales de un solo canal,
  huecos y controles sin señal, sin exponer la cola ni fingir una estimación de
  exhaustividad.
- Descubrimiento comercial privado mediante 21 sitemaps de dominios oficiales
  ya aprobados. Se limita a HTTPS y hosts permitidos, reconoce municipio y
  señal desde la URL/títulos del sitemap y marca como verificadas las URLs que
  ya figuran en el registro de fuentes.
- Seguimiento HTML adicional para siete índices comerciales cuyo sitemap omite
  fichas conocidas. Usa selectores explícitos, una sola portada, cero
  recursión y la misma validación HTTPS/host.
- Puente privado de descubrimiento sectorial mediante SIMA: un sitemap
  residencial filtrado por los 179 municipios y dos índices acotados para
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
  los 179 municipios, 716 consultas diarias completas, API JSON local,
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
- Los 179 centroides proceden del NGMEP 2026 del IGN; se conserva una fixture
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
Tests Core:          25 correctos
Tests Infrastructure:143 correctos
Tests Web:           5 correctos
Tests Web E2E:       5 correctos
Total:               178/178 correctos
Formato:             sin cambios requeridos
validate-config:     1 fuente, 179 municipios, 179 centroides, 34 radar, 569 actores
validate-config live:48 fuentes, 179 municipios, 179 centroides, 66 radar, 569 actores
Crawl offline:       éxito, 4 promociones de 4 páginas
validate-data:       correcto
Dry-run IA offline:  2 llamadas planificadas, 0 llamadas reales, 0 USD
Publish Web:         smoke correcto; data/public incluido y data/state ausente
Live limitado local: 27 fuentes; 27 promociones válidas y activas, 0 fallos
Mapa live local:     27/27 promociones; 26 centroides municipales y 1 exacta
Radar offline:       35 candidatos; 34/34 fuentes sanas; cobertura 179/179
Matriz web offline:  1 candidato filtrado; sin red ni Docker
Matriz web GitHub:   567 resultados; 340 candidatos; 116/116 consultas
Triaje offline:      34/34 pendientes clasificados; estado original intacto
Triaje GitHub inicial:334 pendientes; 30 alta, 230 media, 70 baja, 4 duplicados
Triaje GitHub final: 334 pendientes; 10 alta, 134 media, 186 baja, 4 duplicados
Triaje GitHub actual:339 pendientes; 2 alta, 146 media, 185 baja, 6 duplicados
BOCM live aislado:   68 entradas, 0 fallos y 0 candidatos el 2026-07-23
Tablones live:       335 entradas, 0 fallos y 0 candidatos el 2026-07-23
Portadas sede live:  37 entradas, 0 fallos y 0 candidatos el 2026-07-23
Fuentes nuevas live: 20 entradas en la cuarta cohorte, 0 fallos y 0 candidatos
Sitemaps live:       839 URLs; 13/13 sanos; 12 candidatos nuevos y 3 conocidos
Sitemap Inara live:  3 URLs; fuente sana y 0 candidatos nuevos
Enlaces live:        12 enlaces; 2/2 sanos; 2 conocidos y 0 pendientes
Nuevos sitemaps live:64 URLs; 3/3 sanos y 0 candidatos nuevos
Enlaces Galivivienda:1 ficha seguida; fuente sana y 1 candidato privado
SIMA live aislado:   3/3 sanas; Orbia y Nevia nuevas; 1 dominio no monitorizado
SIMA ampliado live:  37 fichas actuales; 10 candidatos; 0 fallos
Radar live local:    50/50 sanas; 29/29 municipios; 7 pendientes y 1 dominio nuevo
Candidatos revisados: 0 nuevos; 2 en seguimiento; 0 dominios sin monitorizar
Backfill BOCM live:  1.909 entradas; 1/1 lote y 0 candidatos (24 jun–24 jul)
Radar live conjunto: éxito parcial; PCSP recibió HTML del WAF en lugar de ZIP
CI GitHub real:      correcto en 30278248263 para el commit f14a9d2
Crawl/deploy GitHub: correcto en 30279016501, intento #37
Radar GitHub real:   61 sanas, 0 degradadas, 0 en fallo; 179/179, 28 directas
Cola privada real:   865 pendientes; 91 alta, 260 media, 497 baja, 17 duplicados
Run live local nuevo:20260724T222106154Z; 27/27 fuentes y 27 puntos GeoJSON
Pages real:          correcto; 33 promociones, 32/32 fuentes y 0 fallos
Run live actual:     20260727T153254903Z; 33 puntos GeoJSON, 26 activas
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
  perfil separado `sources.live.json` habilita explícitamente 25 fuentes
  limitadas, conserva dos definiciones manuales deshabilitadas y no se ejecuta
  por accidente.
- El perfil del radar suma 56 definiciones, 55 habilitadas, con
  `zz-web-search-matrix`. La baseline
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
  determinista, superó cinco repeticiones locales consecutivas y quedó
  confirmado por los CI Linux `30121997192` y `30122328706`.
- La exportación final `30122387245`, ya con corte alto en 85 puntos, redujo la
  revisión inmediata a 10 candidatos: 9 páginas comerciales y un anuncio
  administrativo. Los diez URL respondieron HTTP 200; nueve permiten su ruta
  según `robots.txt` y la sede electrónica de Collado Mediano declara
  `Disallow: /*`, por lo que no debe convertirse en fuente rastreada.
- Las nueve páginas comerciales quedaron revisadas el 24 de julio. Inara Homes
  — Residencial El Boalo es la única alta: cinco unifamiliares desde 499.000 €,
  cuatro dormitorios, tres baños y sociedad promotora identificada. Tres
  promociones o resultados quedan `stale`, cuatro candidatos se rechazan por
  producto fuera de alcance o duplicidad y Habitania queda `stale` porque la
  propia ruta ya no encuentra el resultado. Las decisiones reproducibles y la
  evidencia están en
  `docs/source-assessments/high-priority-commercial-review-2026-07-24.md`.
- Inara dispone de fixture reducida y prueba offline. El dry run individual,
  el recorrido live aislado con centroide y dos recorridos conjuntos sobre
  estado/salida aislados fueron correctos. El final
  `20260724T203618752Z` produjo 23/23 fuentes, 23 promociones activas,
  23 elementos GeoJSON y cero fallos; `validate-data` fue correcto. Su sitemap
  oficial procesó tres URLs sin fallos y no creó candidatos nuevos.
- El commit `edbf161` quedó verificado por CI GitHub `30125109587`. La
  ejecución manual `30125211739` terminó al primer intento: 23/23 fuentes,
  23 promociones publicadas —19 activas por continuidad del estado
  histórico—, 23 elementos GeoJSON y cero fallos. El radar dejó 52/52 canales
  sanos, cobertura 29/29 con 28 canales directos y 339 candidatos privados
  pendientes; las 116 consultas SearXNG, el backfill BOCM y la auditoría
  muestral fueron correctos. El piloto IA se omitió y no consumió saldo.
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
  `Europe/Madrid`. Publica solo tras éxito completo de las 25 fuentes
  revisadas; un fallo conserva el último despliegue válido.
- El workflow actualizado ejecuta antes `discover-opportunities` con las 52
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
- La exportación cifrada `30129015499` se verificó y descifró localmente:
  339 pendientes, 2 de prioridad alta, 146 media, 185 baja, 6 duplicados
  probables y 189 dominios. La clave RSA privada efímera se eliminó y el texto
  claro permanece solo en `tmp/`, ignorado por Git.
- La revisión de esa cola no encontró una promoción nueva entre los dos
  candidatos altos. La ampliación posterior admitió cuatro fuentes oficiales:
  Doce Fresnos, Residencial Villa Bárbara, Montelago Residencial y Miraflores
  Villas. Once decisiones negativas o de seguimiento y tres exclusiones de
  host quedaron codificadas para reconciliar el estado previo.
- El smoke live aislado final `20260724T222106154Z` terminó con 27/27 fuentes,
  27 promociones activas, 14 con precio, 27/27 coordenadas por fuente exacta o
  centroide local y cero fallos. Nominatim y Playwright permanecieron
  deshabilitados. `validate-data` confirmó el contrato y el GeoJSON completo.
  La evaluación está en
  `docs/source-assessments/private-triage-expansion-2026-07-24.md`.
- La primera ejecución remota de esa ampliación, `30131105415`, verificó CI y
  desplegó de forma segura, pero quedó en éxito parcial: la portada de
  Miraflores Villas respondió HTTP 403 desde GitHub y Pages conservó las otras
  26 promociones. El sitio mantuvo accesibles `robots.txt`, su sitemap de
  páginas y la API REST pública oficial. Se añadió el adaptador explícito
  `wordpressPageJson`, limitado a `content.rendered` y a una canónica HTTPS del
  mismo host; fixture, prueba de pipeline y smokes live aislados confirmaron
  una promoción y dos entradas de sitemap sin Playwright ni IA.
- La repetición remota `30132324597` demostró que el bloqueo abarca también la
  API REST y el sitemap desde GitHub: volvió a publicar 26 promociones en
  `partialSuccess` y el canal pasó a fallo reiterado. No se añadió proxy,
  navegador evasivo ni intermediario. La ficha y su fixture se conservan para
  revisión manual, pero ambas definiciones quedan deshabilitadas en la
  automatización; el estado operativo honesto pasa a 26 fuentes comerciales y
  55 canales de radar.
- Las tres ejecuciones programadas posteriores sanearon el radar a 55/55
  canales. La más reciente, `30240033410`, dejó un único fallo comercial:
  Hirimasa devolvió HTTP 403 en su ficha. El 27 de julio la página y su API REST
  reprodujeron el bloqueo también en local, mientras el sitemap siguió
  accesible y sano. La extracción directa queda deshabilitada, pero su sitemap
  permanece en el radar; el perfil operativo pasa a 25 fuentes comerciales y
  conserva 55 canales.
- El commit correctivo `931d3ed` superó el CI `30251889184`. La ejecución
  manual posterior `30251963680` validó el perfil operativo definitivo:
  25/25 fuentes comerciales correctas y cero fallos, 26 promociones
  publicadas —24 activas—, 26 elementos GeoJSON y despliegue de Pages
  correcto. El radar dejó 55/55 canales sanos, cero degradados o fallidos,
  29/29 municipios y 28 canales directos; la matriz completó 116/116
  consultas, el backfill BOCM y la auditoría terminaron correctamente y la
  cola privada quedó en 419 candidatos. La IA se omitió y no consumió saldo.
  Hirimasa permanece por continuidad segura del último estado válido con una
  ausencia consecutiva; no se borra ni se marca inactiva hasta alcanzar el
  umbral configurado de tres ausencias.
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
  `30251963680` completó el crawl, validó las 25 fuentes, publicó 26
  promociones y desplegó correctamente
  `https://javiig13.github.io/SierraNueva/`.
- Se comprobó el despliegue y sus contratos por HTTP sin caché. El JSON
  público contiene 26 promociones —24 activas—, el GeoJSON 26 elementos y el
  `runId` es `20260727T091400152Z`. Tanto
  `data/state/opportunity-candidates.json` como
  `data/state/promotion-enrichment.json` y
  `data/state/opportunity-audit.json` devuelven HTTP 404.
- La protección de rama sigue sin configurar.

## Ampliación a toda la Comunidad de Madrid

El 27 de julio de 2026 se amplió el ámbito configurado de los 29 municipios
originales de la Sierra a los **179 municipios oficiales de la Comunidad de
Madrid**. Los nombres, códigos y centroides se regeneran desde
`MUNICIPIOS.csv` del NGMEP 2026 mediante
`scripts/import-madrid-municipalities.ps1`; el ZIP oficial conserva el mismo
SHA-256 ya documentado. Los alias y localidades originales se preservan.

La matriz web live pasa de 116 a **716 consultas diarias** —179 municipios por
cuatro familias—, eleva su límite global a 10.000 resultados y SIMA admite los
179 slugs con un máximo defensivo de 200 fichas. El workflow refleja el nuevo
volumen y amplía su timeout defensivo de 75 a 120 minutos. El radar offline
produjo 35 candidatos desde fixtures y cobertura
central sana 179/179; los canales municipales directos siguen siendo 28, por
lo que 151 municipios dependen inicialmente de los canales centrales y del
descubrimiento web.

El reconocimiento de Madrid capital conserva el término «Madrid», pero excluye
las frases regionales «Comunidad de Madrid» y «Comunidad Autónoma de Madrid»
cuando no existe otra señal fuerte de ciudad. Así se evitan asignaciones
regionales falsas sin perder fichas que solo indican «Madrid».

Se añadió `config/market-actors.json`, un inventario interno de configuración
y no un dataset público. La instantánea saneada contiene 569 organizaciones:
194 registros del listado oficial de ASPRIMA, 378 del directorio de empresas
de SIMA y canales comerciales ya monitorizados que no coincidían con ellos.
Hay 197 webs declaradas y 20 actores enlazados a fuentes live; los otros 549
permanecen como candidatos. No se guardan correos, teléfonos, contactos ni
descripciones. Figurar en ASPRIMA o SIMA no demuestra por sí solo que una
entidad sea promotora ni que tenga oferta unifamiliar vigente en Madrid.

La ampliación está comprobada localmente con fixtures, validación de
configuración y pruebas. Un smoke live aislado siguió 37 fichas SIMA del nuevo
ámbito, produjo 10 candidatos privados y terminó sin fallos tras incorporar el
selector `.entry-content` del formato actual.

La primera ejecución ampliada, `30269094453`, terminó globalmente correcta y
desplegó Pages en 6 min 5 s, con 26 promociones, 25 fuentes comerciales sanas,
179/179 municipios en el estado restaurado y 427 candidatos privados. Sin
embargo, el paso del radar terminó antes de llegar a la matriz: BOCM agotó el
timeout HTTP y `OpportunityFeedReader` propagó el `TaskCanceledException` como
cancelación global. El resumen además etiquetaba el arranque correcto de
SearXNG como si acreditara las 716 consultas.

Se corrigieron ambos defectos: un timeout interno se traduce ahora a
`HttpRequestException` y queda aislado como fallo de esa fuente, mientras una
cancelación externa sigue deteniendo el proceso; una prueba con `HttpClient`
realmente temporizado cubre la diferencia. El resumen separa «arranque
SearXNG» de «matriz web» y comprueba que el `lastRun` sea nuevo y que
`zz-web-search-matrix` haya terminado correctamente antes de acreditar las
716 consultas.

La segunda ejecución ampliada,
[`30270433121`](https://github.com/Javiig13/SierraNueva/actions/runs/30270433121),
verificó el flujo completo sobre `996c9d4`: terminó en 20 min 34 s, ejecutó
las **716 consultas** en 15 min 22 s, conservó 609 resultados de búsqueda y
dejó 833 candidatos privados pendientes. El radar declaró 55 fuentes sanas,
cero degradadas y cero en fallo reiterado, con cobertura 179/179 y 28 canales
municipales directos. El crawler publicó 26 promociones —22 activas— desde
25/25 fuentes comerciales sanas; el `runId` público es
`20260727T134551916Z`. La IA permaneció desactivada.

Pages se desplegó correctamente. La portada, `data/run.json`,
`data/promotions.json` y `data/promotions.geojson` devolvieron HTTP 200 sin
caché. `data/state/opportunity-candidates.json`,
`data/state/promotion-enrichment.json` y
`data/state/opportunity-audit.json` devolvieron HTTP 404. La cobertura live
diaria de los 179 municipios queda así verificada para esta ejecución; sigue
siendo una observación temporal, no una garantía permanente de exhaustividad
del mercado.

El artefacto cifrado de esa ejecución se recuperó mediante el workflow
`30274617555`; el hash local coincidió con GitHub, el JSON autenticado se
validó y la clave privada efímera se eliminó. El informe confirmó 833
pendientes en 367 dominios: 98 altos, 298 medios, 421 bajos y 16 duplicados.

La primera revisión por dominio incorpora cuatro oportunidades oficiales:
Insur Calíope, Nuevo Brunete, Residencial Cielo Arroyomolinos y
NovaCiempozuelos. Una segunda revisión incorpora Los Molinos, Los Molinos II
y Arroyo Homes. El perfil live queda en 34 definiciones, 32 automáticas y
dos manuales por bloqueo. Cada formato dispone de fixture y prueba; las cuatro
primeras fichas y las tres nuevas respondieron HTTP 200, respetaron `robots.txt`
y terminaron correctas en
smokes `dry-run` sin Playwright ni geocodificación.

El radar añade los sitemaps oficiales de Grupo Insur, Soldit Homes y GEMG y el
índice HTML acotado de Grupo Egido; sus smokes procesaron 61, 10, 12 y 13
entradas respectivamente, sin fallos. El radar incorpora además el sitemap de
Grupo Impulsa y el índice HTML acotado de Bimarán, cuyos smokes procesaron 11
y 5 entradas sin fallos. El total queda en 62 canales, 61 activos. El catálogo
interno se sincronizó a 569 actores, 197 con web declarada y 22 monitorizados;
no contiene contactos ni se publica.

La revisión también codifica promociones vendidas, entregadas y una
plurifamiliar fuera de alcance; Cambrija Viridis 3.0 y Larvin Hernán Cortés
quedan en observación por falta de disponibilidad inequívoca. Se corrige el
triaje para que «promoción económica» no sea una señal inmobiliaria y para
penalizar hosts administrativos sin términos residenciales.

El commit `f14a9d2` superó el CI `30278248263`. El workflow `30279016501`
completó 716/716 consultas, conservó 612 resultados, dejó 61/61 canales sanos
y cobertura 179/179. El crawler publicó 33 promociones —26 activas— y 33
puntos GeoJSON desde 32/32 fuentes correctas, sin fallos; incorporó tres altas
y una modificación. Pages y sus contratos públicos devolvieron HTTP 200 y los
tres estados privados comprobados HTTP 404. La IA se omitió.

La exportación cifrada `30281037199` midió 865 candidatos pendientes: 91 alta,
260 media, 497 baja y 17 duplicados. Alta y media bajaron respecto a 98/298,
aunque la matriz incorporó señales nuevas; el crecimiento se concentra en
baja. La clave RSA efímera local se eliminó y el informe detallado no se
publicó. Evidencia y decisiones en
`docs/source-assessments/madrid-expansion-2026-07-27.md`.

Una tercera revisión recuperó y verificó el artefacto cifrado de la ejecución
`30288195132`; su SHA-256 local fue
`e8feb8b66e8429027fc74dba671b50d15cc11a5399907f54e4ae67b08d04ad23`.
Se revisaron por dominio los 91 candidatos altos y se incorporaron ocho fichas
oficiales: Antaro Móstoles Polar, Trinosa Bonart, Kronos Krest, Brosh Villa 2,
las dos fases de Brosh Villa 6 y Naiz Aceris y Pinea. Las ocho produjeron una
promoción en smokes HTTP reales, sin Playwright ni geocodificación. Cada
formato dispone de fixture y aserciones específicas.

El perfil live queda en 42 definiciones, 40 automáticas y dos manuales por
bloqueo. El radar pasa a 64 canales, 63 activos: mantiene 21 sitemaps
comerciales y amplía a siete índices HTML acotados. El índice oficial de Brosh
procesó 26 enlaces; el de Naiz procesó 12 entradas y conservó cinco candidatos
privados tras seguir solo sus fichas españolas. El catálogo interno conserva
569 actores, 197 con web declarada y 24 monitorizados.

La extracción se reforzó con casos observados, no inferidos: `villas` como
inventario, superficies construidas tabulares, estado `Disponible`, «próxima
promoción», fallback al título si el `h1` existe vacío y separación entre
superficie construida y parcela en Bonart. Los avisos de planeamiento continúan
como `monitoring` privado; agregadores, producto plurifamiliar, promociones
vendidas y ruido administrativo quedan descartados mediante reglas
reproducibles. La ampliación quedó verificada en la ejecución remota descrita
abajo.

## Actualización verificada del 28 de julio de 2026

La ejecución programada `30331478110` —commit `5fd377c`, intento 39— publicó el
`runId` `20260728T054032275Z`: 41 promociones, 35 activas, 40/40 fuentes
comerciales correctas y 41 puntos GeoJSON. El radar conservó 179/179
municipios con vigilancia, 27 canales municipales directos, 62 fuentes sanas,
una degradada y ninguna en fallo reiterado. La matriz produjo 628 resultados
de búsqueda; la IA se omitió.

La única degradación fue `noticias-navalafuente`: el RSS oficial agotó un
timeout en esa ejecución. El lector del radar reintenta ahora, con una
petición nueva por intento, timeouts internos y respuestas HTTP transitorias
408/429/500/502/503/504 hasta `MaxRetries`. La prueba offline simula el primer
timeout y la recuperación; un smoke posterior del RSS real respondió HTTP 200.
La ejecución `30354344817` confirmó después la recuperación en GitHub.

La exportación cifrada `30351950285` se descargó y autenticó localmente. El
SHA-256 del artefacto fue
`8a7e3b0f77c8479236f1e8b219b474a00b7f856b3d30511bcc7a8fba33348014`;
la clave RSA efímera se eliminó tras descifrar. La cola contenía 843
pendientes: 18 alta, 276 media, 532 baja y 17 duplicados. De los 18 altos, 14
eran señales ya revisadas en `monitoring` —planeamiento, contratación o
promoción aún insuficiente— y cuatro eran `new`.

Los cuatro nuevos quedaron resueltos: dos URLs de `obrasnuevas.com` devolvían
una portada genérica y quedaron `rejected`; el índice regional de Kronos queda
`rejected` con una regla anclada que no afecta a fichas hijas; Bonart pasa a
`verifiedSource` mediante un alias explícito entre su REST de extracción y su
URL pública. Las reglas ancladas conservan la semántica histórica de
coincidencia parcial y solo interpretan `$` como final de URL.

La revisión encontró seis fichas comerciales oficiales aptas: Kronos Mirage,
Cambrija Viridiana y cuatro promociones de Larvin en Boadilla, Rivas y
Villaviciosa de Odón. Pozuelo se descartó porque la propia ficha indica
`PROMOCIÓN VENDIDA`. Las seis altas disponen de fixture, prueba específica y
smoke HTTP real correcto sin Playwright ni geocodificación. La extracción
agrega rangos enteros observados y reconoce “5 dormitorios configurables a 4”
como 4–5; el radar prioriza título y URL para el municipio antes que un bloque
de “otras promociones”.

El perfil live queda en 48 fuentes revisadas —46 automáticas— y el radar en 66
canales —65 activos—, 30 de ellos comerciales: 23 sitemaps y 10 índices HTML
acotados. Cambrija aportó una URL ya verificada; el índice Larvin procesó cinco
fichas y las clasificó como cuatro verificadas y una vendida. Las URLs
canónicas distintas de la URL técnica de extracción se declaran y validan
contra `allowedHosts`; nunca se verifican por dominio completo.

La baseline posterior quedó verde con SDK `10.0.301`: restore, build Release
sin advertencias, 178/178 tests —25 Core, 143 Infrastructure, 5 Web y 5 E2E—,
formato, configuración, radar offline 34/34, crawl offline de cuatro
promociones, validación de datos y publish con datos públicos presentes y
estado privado ausente.

El commit `dcb68fc` superó el CI `30354283967`. La ejecución manual
[`30354344817`](https://github.com/Javiig13/SierraNueva/actions/runs/30354344817)
terminó correctamente y desplegó Pages con `runId`
`20260728T114649927Z`: 47 promociones —43 activas—, seis altas, dos cambios,
46/46 fuentes comerciales y cero fallos. Las seis fichas nuevas aparecen en
el dataset publicado. La matriz ejecutó 716 consultas, conservó 634 resultados
y dejó 883 candidatos privados pendientes; la IA se omitió y no consumió
saldo.

Navalafuente terminó sano gracias al reintento. El radar mantuvo 179/179
municipios, 27 canales directos, 64 fuentes sanas, una degradada y ninguna en
fallo reiterado. La única incidencia fue `tablon-los-molinos`, que respondió
HTTP 403 desde el runner aunque localmente devolvía 200. Cambiar a la ruta
equivalente `action=inicioTablon` no alteró el bloqueo por IP: la ejecución
`30356769502` volvió a recibir 403 y marcó el canal como fallo reiterado, sin
impedir 46/46 fuentes comerciales, 47 promociones ni Pages.

No se evade el control de la sede. El canal se traslada a la publicación
oficial y pública de bandos y avisos del propio Ayuntamiento de Los Molinos,
en `www.ayuntamiento-losmolinos.es/?post_type=lsvr_notice`. El selector
acotado `.post__title` produjo 15 entradas y fuente sana en un smoke HTTP real;
BOCM, BOE, PCSP y la matriz web mantienen la redundancia central.

La ejecución
[`30359180713`](https://github.com/Javiig13/SierraNueva/actions/runs/30359180713)
confirmó después el nuevo canal: `tablon-los-molinos` terminó sano con 15
entradas. Publicó 47 promociones desde 46/46 fuentes comerciales, mantuvo
179/179 municipios, ejecutó las 716 consultas y desplegó Pages con `runId`
`20260728T124855983Z`; la IA siguió omitida. El único parcial fue un HTTP 500
aislado de `tablon-san-lorenzo`, sano en los dos pases anteriores: 64 fuentes
sanas, una degradada y ninguna en fallo reiterado.

Ese pase reveló además un falso negativo del crawler comercial: una respuesta
`304 Not Modified` dejaba cero páginas para extraer y contaba como ausencia.
Tras tres pases podía desactivar una promoción aunque el servidor confirmase
que la ficha no había cambiado. El merge reconoce ahora las URLs
`HTTP_NOT_MODIFIED` como vistas, actualiza `LastSeenUtc`, reinicia ausencias y
reactiva el estado previo erróneo. La prueba reproduce tres 304 consecutivos y
la recuperación; una ausencia real sigue desactivando tras el umbral.

La ejecución
[`30361782414`](https://github.com/Javiig13/SierraNueva/actions/runs/30361782414)
confirmó la corrección sobre el estado real: `TOMILLAR`, previamente inactiva
por el mismo defecto, se publicó como `reactivated` al recibir su 304. El
cuarto crawl manual consecutivo saturó varias webs: terminó `partialSuccess`
con 31 fuentes correctas y 15 fallidas por timeout o páginas intermedias sin el
selector esperado. La escritura segura conservó las 47 promociones y Pages;
no se sustituyó el dataset por una salida vacía. Los Molinos, Los Molinos II y
Quercus Dorf siguen inactivos porque sus tres fuentes estuvieron entre las
fallidas, no porque la corrección de 304 fallase. Deben recuperarse en la
siguiente ventana diaria, sin encadenar más peticiones.

La interfaz se reorganizó después como un espacio de exploración de altura
completa en escritorio. El documento ya no se desplaza: el mapa permanece
visible y el listado usa su propio scroll con `overscroll-behavior` acotado.
Leaflet admite zoom con la rueda y expone su nivel únicamente para la prueba
E2E. Dos regresiones de navegador comprueban que la rueda sobre el listado no
mueve la página ni el mapa y que la rueda sobre el mapa cambia el zoom. En
móvil se conserva la navegación por pestañas y el scroll normal del documento.

El aviso ancho de «actualización parcial» se sustituyó por información
operativa junto al recuento: separa fuentes verificadas en la ejecución de
fuentes cuyo último dato válido ha quedado protegido. No se oculta ni se
reclasifica un fallo; se elimina la impresión incorrecta de que el dataset
conservado está incompleto. La paleta pasa de monocromo verde a una combinación
de azul noche, esmeralda, coral, violeta y ámbar, con contraste medido por la
suite E2E y sin imágenes ni dependencias externas.

El commit `d257955` superó la baseline local con 178/178 pruebas y se publicó
mediante la ejecución
[`30382540723`](https://github.com/Javiig13/SierraNueva/actions/runs/30382540723).
El resultado comercial volvió a ser completo: 46/46 fuentes correctas, cero
fallos, 47 promociones publicadas y 46 activas en el `runId`
`20260728T174035404Z`. Se reactivaron Quercus Dorf, Névola, Los Molinos, Los
Molinos II y Los Pinarejos. Pages muestra el nuevo diseño y los tres caminos
privados comprobados continúan en HTTP 404. La IA se omitió y no consumió
saldo.

## Recuperación operativa del 26 de octubre de 2026

La revisión detectó una interrupción real, no solo una fecha de cambios antigua:
GitHub marcó `Crawl and deploy` como `disabled_inactivity` el 27 de septiembre,
tras 60 días sin actividad del repositorio. El último pase fue `36294554006`,
con `runId` `20260927T045523938Z`: 47 promociones, 46 activas y 44/46 fuentes
correctas. Quercus Dorf conservaba su comprobación del 31 de agosto y Osnola la
del 1 de septiembre. El resto se comprobó el 27 de septiembre.

La API de Actions confirmó cero cachés privadas disponibles. Se reactivó el
workflow y el primer despacho `40395538827` se canceló antes del crawl al
confirmar la caducidad: el código anterior habría empezado sin histórico y
podía omitir precisamente las fichas de fuentes fallidas. Se protegieron los
cinco contratos de Pages y se conservó ese dataset de 47 promociones en la rama
`public-data`, sin candidatos privados ni cambios en `main`.

El refuerzo implementa:

- Checkpoints públicos reales después del despliegue, con allowlist de cinco
  archivos, identidad de ejecución y cobertura GeoJSON comprobadas. Sin
  commits vacíos ni sustitución por fixtures.
- Restauración anterior al crawl y recuperación comercial si faltan todas las
  copias privadas, conservando identidad, fechas, actividad y ausencias. Una
  corrupción privada sigue abortando; un fallo total no reemplaza la salida.
- Frescura calculada al leer la web, no congelada en `isStale` durante el build;
  umbral live de 36 horas y fecha/aviso por tarjeta y detalle.
- Layout de escritorio que absorbe el aviso sin desplazar la página ni perder
  el scroll independiente del listado.
- Triaje diario y una incidencia operativa reutilizable con fallos y candidatos
  nuevos de prioridad alta pendientes, exclusivamente mediante agregados. La
  entrega de la alerta no bloquea un dataset válido y sus errores son visibles.

La validación local incluye build sin advertencias, 208 pruebas —25 Core,
160 Infrastructure, 17 Web y 6 E2E—, recuperación parcial/total desde un
checkpoint sin caché y pruebas offline de automatización sin credenciales.
El E2E reproduce un JSON de siete días con `isStale=false`, comprueba el aviso,
la fecha propia de las tarjetas, el detalle y la recarga con datos recientes;
una prueba verifica que recargar renueve el token de los cuatro recursos.
Las regresiones de scroll, mapa,
móvil y accesibilidad también pasan. El crawl aislado mantiene las cuatro
fixtures, el formato es válido y el publish/prepare-pages excluye estado
privado.

Límites observados: Gilmar devuelve actualmente una página intermedia con
JavaScript incluso en `robots.txt`; no se ejecuta ese desafío ni se amplía el
selector para aceptar una ficha inexistente. Osnola no aparece en el sitemap
oficial actual, mientras Essentia sí; no se inventa una URL alternativa ni se
deduce que esté vendida. Sus últimos datos deben conservarse como no
verificados. La cola privada histórica caducada no puede recuperarse desde
datos públicos; la nueva ejecución debe reconstruir señales actuales y su
backfill, sin prometer recuperación exhaustiva de oportunidades pasadas.

El refuerzo inicial `2b22bc2` superó el CI Linux
[`40399726051`](https://github.com/Javiig13/SierraNueva/actions/runs/40399726051).
La recuperación real
[`40400108357`](https://github.com/Javiig13/SierraNueva/actions/runs/40400108357)
completó crawl, despliegue y checkpoint: `runId` `20261026T213557759Z`, 47
promociones, 46 activas y 42/46 fuentes correctas. La ausencia de caché activó
la recuperación de las 47 identidades originales; ninguna desapareció. Los
cinco blobs de `public-data` coinciden byte a byte con Pages y el mapa contiene
las 47 fichas. Los tres caminos privados comprobados siguen en HTTP 404 y el
navegador real muestra el nuevo resumen, fechas propias y mapa sin scroll de
documento en escritorio.

El radar completó la matriz de 716 consultas, con 612 elementos conservados,
y el backfill BOCM de 31 días. La cobertura registró 179/179 municipios con
vigilancia sana, 28 canales directos y 65 fuentes sanas. El triaje reconstruido
contiene 449 pendientes: 35 altos, 139 medios, 269 bajos y 6 duplicados probables.
La incidencia [#1](https://github.com/Javiig13/SierraNueva/issues/1), asignada al
propietario, avisa de la caché caducada, los fallos comerciales y 32 altas nuevas
de prioridad alta sin revisar, sin publicar sus títulos, dominios ni URLs.

Ese pase detectó además dos fallos nuevos: Móstoles Polar devuelve 404 tanto en
la ruta configurada como en la antigua, y Nuevo Brunete redirige a Albor.
Antaro ya no muestra Polar en su índice comercial; no se deduce una venta ni se
inventa un reemplazo. Para Soldit se corrigió la exigencia del antiguo `ul` y se
lee el precio de cabecera. El smoke live aislado captura Albor con 28 adosados,
4–5 dormitorios y 566.000 €, manteniendo `sn-90d3952f4dae056cbffa` y la primera
observación del 27 de julio. La evidencia de alias canónico permite conservar
identidad y enriquecimiento revisado incluso tras varios cambios de nombre, sin
confundir una página de origen JSON-LD compartida ni fusionar identidades
ambiguas. Estas regresiones elevan la suite local a 208 pruebas.

El cierre remoto quedó verificado sobre `fcb649c`, con CI Linux
[`40409990597`](https://github.com/Javiig13/SierraNueva/actions/runs/40409990597)
y despliegue completo
[`40410451805`](https://github.com/Javiig13/SierraNueva/actions/runs/40410451805).
Pages sirve `20261026T224314513Z`, finalizado a las 23:46 hora de Madrid:
47 promociones, 46 activas, 24 con precio, 47 puntos de mapa y 43/46 fuentes
correctas. Albor aparece con los datos actuales y el identificador original,
sin alta duplicada ni reinicio de la primera observación. Permanecen fallidas
Móstoles Polar, Quercus Dorf y Osnola, con su último dato protegido y advertencia
de antigüedad por ficha; no se confirma su disponibilidad.

El segundo pase restauró correctamente `crawler-state-40400108357` y registró
`PRIVATE_STATE_MISSING=false`: se comprobó tanto la recuperación sin caché del
primer pase como la continuidad con estado privado del siguiente. La matriz
completó de nuevo las 716 consultas, conservando 615 elementos; radar, backfill
de 31 días, auditoría y triaje terminaron correctamente. La cola contiene 530
pendientes: 37 altos, 169 medios, 318 bajos y 6 duplicados probables. La
incidencia #1 se actualizó a 43/46 y 32 altas nuevas prioritarias sin revisar,
retiró el aviso de caché caducada y añadió un único comentario por los cambios
reales de salud. La IA volvió a omitirse.

El checkpoint final es `9c30ebbcffac7ff283d68d3ffab931ae442d6387`,
descendiente del checkpoint anterior: contiene solo los cinco contratos y sus
blobs coinciden byte a byte con Pages. Las 47 identidades del dataset de
septiembre se mantienen también en GeoJSON. El navegador real verifica el
resumen 43/46, Albor y mapa visible, sin desbordamiento de documento a
1440×1000. Los tres caminos privados siguen en 404 y el workflow está
`active`, con horario diario 06:17 `Europe/Madrid`. Todavía no se ha observado
el próximo disparo automático; no se garantiza continuidad de terceros ni
cobertura exhaustiva del mercado.

## Próximo trabajo recomendado

Prioridad inmediata: exportar el nuevo recuento cifrado de la cola y continuar
con los candidatos medios; la matriz y la recuperación comercial ya están
comprobadas y no deben sustituir esa revisión.

1. Vigilar las 46 fichas comerciales automatizadas, las fichas manuales de
   Miraflores Villas/Hirimasa y las decisiones de triaje. Reabrir un
   descarte solo ante una ficha oficial nueva, un cambio real de producto o
   disponibilidad verificable.
2. Mantener en observación el estudio de detalle de Collado Mediano y la
   licitación de ocho parcelas municipales de Los Pinarejos. Solo promover una
   ficha cuando aparezcan promotora, inventario y comercialización oficial.
3. Vigilar la variabilidad de Apremya, STANCE y Los Molinos: conservar la
   redundancia y no tratar un 403 o un fallo TLS aislado como autorización
   para evadir controles.
4. Continuar el triaje de los candidatos medios restantes por dominio y
   registrar evidencia independiente antes de convertir cualquier discrepancia
   en una nueva fuente o promoción.
5. Mantener verde la baseline offline y vigilar el backfill móvil semanal.
6. Revalidar las evaluaciones antes de cada cambio operativo o
   automatización; conservar la salida y el estado live separados.
7. Revalidar PCSP y mantener la fuente como fallo parcial mientras el endpoint
   oficial devuelva la página WAF en vez del ZIP.
8. Resolver Robledo de Chavela solo cuando exista un canal municipal público
   apto para `HttpClient` o se justifique un adaptador JavaScript revisable.
9. Ampliar la ventana histórica BOCM solo cuando aporte una pregunta concreta
   de producto y siempre sobre un estado privado aislado.
10. Mantener la matriz municipal: reevaluar descartes solo cuando aparezca una
   ficha oficial vigente o se corrija la carencia documentada.
11. Ensayar Playwright o Nominatim solo cuando una fuente revisada realmente
   los necesite.
12. Recuperar la cola privada del piloto `30094391984` en un entorno local,
    generar el informe con `review-enrichment --report` y decidir sus ocho
    campos uno a uno tras comprobar cada cita. Contrastar además el coste
    estimado acumulado de 0,013350 USD con el panel de OpenAI. No habilitar
    todavía una programación periódica de IA. El secreto de GitHub quedó
    confirmado por HTTP 200; la clave no está cargada ni se expone en la
    terminal local.
13. Definir la protección y política de ramas cuando el propietario decida el
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
