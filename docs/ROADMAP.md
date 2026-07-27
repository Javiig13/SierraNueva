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
- **Hecho:** rediseño responsive y compacto con mapa dominante, precios sobre
  marcadores, filtros compactos/avanzados y resaltado bidireccional entre mapa
  y tarjetas. La segunda iteración del 24 de julio sustituyó el hero por una
  franja operativa inferior a 90 px, amplió el lienzo hasta 1.980 px, ocultó
  celdas vacías, muestra completitud y separa visualmente marcadores
  coincidentes, protegido por E2E.
- **Hecho:** invalidación por versión de la caché de los cuatro recursos
  públicos para mostrar cada dataset desplegado sin mezclar revisiones.
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
- **Hecho:** segunda ampliación con Antaro, Grupo Index y Vesari: seis fixtures
  y promociones adicionales. El perfil suma 14 fuentes/promociones en 11
  municipios, un 75 % más de oportunidades y un 37,9 % de cobertura municipal.
- **Hecho:** corrección de límites entre nodos HTML para impedir totales
  concatenados como `1718`, rangos “desde/hasta”, estado singular “última
  vivienda” y negación “no adosadas”, todo cubierto offline.
- **Hecho:** control de frecuencia Vesari reforzado a diez segundos después de
  observar HTTP 429 con cinco; el recorrido conjunto conserva las tres fichas
  sin peticiones paralelas.
- **Hecho:** tercera ampliación con La Bellota (Alpedrete) y C/ Pradillos
  (Moralzarzal): 16 fuentes/promociones en 13 municipios, 44,8 % de cobertura
  municipal y 16/16 elementos GeoJSON en el recorrido aislado
  `20260723T214132164Z`.
- **Hecho:** página mixta de Hirimasa acotada a tres bloques obligatorios; la
  fixture incluye promociones ajenas y comprueba que no contaminan nombre,
  totales, superficies ni estado.
- **Hecho:** reconocimiento normalizado de “chalet individual” y de piscina en
  “zona común” o “urbanización”, con cobertura offline.
- **Hecho:** pausa Vesari elevada de diez a veinte segundos tras reproducir un
  nuevo HTTP 429 en la tercera ficha; una prueba fija el mínimo observado y la
  repetición 16/16 terminó sin fallos.
- **Hecho:** cuarta ampliación con Nuvare — Cumbres y Claveles, STANCE —
  Essentia y Osnola, y Residencial Montemilano: 21 fuentes/promociones en 16
  municipios, 55,2 % de cobertura municipal y cinco fixtures reducidas.
- **Hecho:** tablas live acotadas por bloques para impedir truncamiento y
  contaminación de formularios; correcciones con regresión para precios entre
  columnas, millares de parcela, licencia obtenida y contadores de
  disponibilidad.
- **Hecho:** Montemilano excluye deliberadamente las bandas de presupuesto de
  sus formularios; precio permanece nulo y un cambio de sus bloques
  obligatorios falla de forma cerrada.
- **Hecho:** pausa compartida de Vesari elevada a treinta segundos después de
  observar un nuevo HTTP 429 con veinte segundos en el recorrido de 21 fuentes.
- **Hecho:** quinta ampliación con Névola Homes en Guadalix de la Sierra:
  22 fuentes/promociones en 16 municipios. El extractor incorpora plazas de
  aparcamiento y rangos derivados de valores explícitos de parcela; la fixture
  demuestra que la reserva de 5.000 € queda fuera y el precio sigue nulo.
- **Hecho:** recorrido live conjunto aislado `20260724T151724214Z`: 22/22
  fuentes correctas, 22 promociones activas, cero fallos y datos válidos.
- **Hecho:** Abantos Home reevaluada y descartada porque su producto vigente
  son bajos con jardín y áticos plurifamiliares. La decisión y la identidad de
  Névola están en
  `docs/source-assessments/nevola-abantos-2026-07-24.md`.
- **Hecho:** sexta ampliación con Inara Homes — Residencial El Boalo. La
  sociedad promotora, `robots.txt`, vigencia y ficha unifamiliar quedaron
  revisados; la fixture prueba cinco viviendas, precio desde 499.000 €, cuatro
  dormitorios, tres baños, piscina privada opcional y nombre de promotora.
- **Verificado live:** recorrido conjunto aislado
  `20260724T203618752Z`: 23/23 fuentes, 23 promociones activas, 23 elementos
  GeoJSON, cero fallos y `validate-data` correcto. Nominatim permaneció
  deshabilitado y Residencial El Boalo usa centroide municipal trazable.

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
- **Hecho:** cuarta cohorte municipal con actualidad HTML de Collado Villalba
  y RSS oficiales de Guadalix de la Sierra y Navalafuente; 20 entradas live,
  cero fallos y cero candidatos. El radar directo alcanza 28/29 municipios y
  el catálogo suma 32 fuentes.
- **Pendiente:** resolver Robledo de Chavela por un canal municipal apto. El
  RSS devuelve 403 al cliente identificado, la antigua sede responde que está
  inactiva y la nueva sede entrega una aplicación JavaScript sin avisos en el
  HTML. No se evade la restricción; los canales centrales siguen cubriéndolo.
- **Pendiente:** revalidar PCSP: el último smoke recibió una denegación WAF en
  HTML con HTTP 200. El lector la rechaza explícitamente como no-ZIP.

- **Pendiente:** ensayar Playwright en una fuente autorizada que realmente lo
  necesite.
- **Pendiente:** ensayar caché y límites de Nominatim con identidad de contacto
  real, solo si hace falta.
- **Hecho:** timeout HTTP agotado aislado como fallo de fuente, sin convertirlo
  en cancelación global; integración offline con una fuente lenta y otra válida
  que termina `PartialSuccess`. El perfil live usa 60 segundos tras observar
  una respuesta de Gilmar superior al límite anterior de 30.
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
- **Hecho:** cobertura de mapa exigida en el workflow; el crawler exige que
  todas las promociones live aparezcan en GeoJSON mediante centroides
  trazables o ubicación exacta, sin activar Nominatim.
- **Hecho:** `base href` `/SierraNueva/`, `.nojekyll` y `404.html` preparados y
  comprobados localmente.
- **Hecho:** Pages activado con GitHub Actions como fuente; ejecución manual
  `30054208393` correcta y SPA disponible en
  `https://javiig13.github.io/SierraNueva/`.
- **Hecho:** portada, mapa, dataset live de 21 promociones en 16 municipios, 21
  elementos GeoJSON y exclusión pública de `data/state` comprobados. La vista
  activa muestra 17 opciones; las cinco promociones de la cuarta ampliación
  están presentes en listado y mapa.
- **Hecho:** ejecución `30089518646` sobre `102a7b5` al primer intento:
  21/21 fuentes, 21 promociones, 17 activas, 21 puntos GeoJSON y Pages
  actualizado. CI `30089475710` correcta y cola privada de enriquecimiento
  ausente de la web mediante HTTP 404.
- **Hecho:** ejecución `30105161699` sobre `8c2bd2f`: 22/22 fuentes,
  22 promociones, 19 activas, 22 puntos de mapa y Pages actualizado. Névola
  figura en lista y mapa; IA omitida y estado privado ausente por HTTP 404.
  CI `30104995452` correcta.
- **Hecho:** CI `30125109587` correcta sobre `edbf161` y despliegue
  `30125211739` al primer intento: 23/23 fuentes, cero fallos, 23 promociones
  publicadas —19 activas— y 23 puntos GeoJSON. Residencial El Boalo está
  presente con sus campos estructurados. Los dos estados privados comprobados
  devuelven HTTP 404 y el piloto IA quedó omitido, sin consumo.

## P5 — Cobertura continua

- **Hecho:** proveedor SearXNG privado con formato JSON aislado, municipio
  asociado a la consulta sin contaminar evidencias, destinos exclusivamente
  HTTPS, blocklist de portales/redes, confianza reducida y deduplicación por
  URL.
- **Hecho:** matriz completa configurable de 29 municipios por cuatro familias
  —producto, promotor/cooperativa, planeamiento/licencias y suelo/parcelas—:
  116 consultas en cada ejecución diaria, sin rotación ni muestreo.
- **Hecho offline:** fixture SearXNG y dos pruebas específicas para parseo,
  filtros, expansión íntegra de municipio × consulta, cliente aislado y
  deduplicación. El radar offline pasa a 34 fuentes.
- **Hecho live:** GitHub Actions `30119685200` levantó la imagen oficial fijada
  por digest, validó `/healthz`, ejecutó las 116 consultas y destruyó el
  contenedor. SearXNG entregó 567 resultados y el filtro añadió 340 candidatos;
  la cola terminó con 334 pendientes, 29/29 municipios cubiertos y 27 con canal
  directo. La única fuente degradada fue el tablón de Los Molinos por HTTP 403,
  no la matriz web.
- **Hecho:** el workflow acepta los códigos documentados `0` (éxito) y `1`
  (`PartialSuccess`) del crawler, pero mantiene `validate-data`, cobertura de
  mapa, rechazo de fixtures y ausencia de estado privado como barreras antes
  de Pages. Los estados graves continúan deteniendo el despliegue.

- **Hecho:** registro privado de salud para todas las fuentes del radar con
  último intento, éxito, fallo, respuesta no vacía, contadores consecutivos e
  incidencia saneada; la siguiente revisión prevista permite detectar canales
  atrasados.
- **Hecho:** degradación ante el primer fallo, fallo reiterado a partir del
  segundo y anomalía tras dos respuestas vacías consecutivas cuando la fuente
  había proporcionado datos.
- **Hecho:** instantánea de cobertura para los 29 municipios con estados
  directo, central, combinado, degradado o no comprobado y recuento de
  candidatos pendientes.
- **Hecho:** `coverage-status` permite consultar agregados y puntos ciegos sin
  revelar la cola privada.
- **Hecho:** `coverage-status` expone además canales y dominios comerciales
  sanos, municipios con señales comerciales y el embudo por estado; continúa
  sin mostrar títulos ni URLs privadas.
- **Hecho:** integración del radar en el workflow diario antes del crawl. El
  estado se conserva solo en la caché privada y el resumen informa su resultado;
  un fallo administrativo parcial no sustituye ni bloquea el dataset comercial.
- **Hecho:** prueba offline de integración con 33/33 fuentes sanas, 29/29
  municipios vigilados y 28 con canal municipal directo, además de regresión
  para vacíos, fallos reiterados y recuperación.
- **Hecho:** descubrimiento acotado mediante 13 sitemaps declarados en
  `robots.txt` o comprobados en dominios comerciales oficiales ya aprobados.
  Solo admite HTTPS y hosts permitidos; una fixture cubre URLs válidas,
  externas y no seguras.
- **Hecho:** smoke live aislado de los 13 sitemaps: 839 URLs procesadas, 13/13
  fuentes sanas, 15 coincidencias, tres URLs ya verificadas y 12 candidatos
  pendientes de revisión. Evaluación en
  `docs/source-assessments/continuous-discovery-2026-07-24.md`.
- **Hecho:** primera ejecución real del workflow ampliado (`30054208393`):
  40/45 fuentes sanas, cinco municipales degradadas, 29/29 municipios
  vigilados, 23 con canal directo sano y 14 candidatos pendientes. El radar
  informó fallo parcial, pero el aislamiento permitió publicar 21/21 fuentes
  comerciales; Pages terminó correctamente y `data/state` devolvió 404.
- **Hecho:** revisión reproducible de los 14 candidatos pendientes: cuatro en
  monitorización, ocho rechazados y dos obsoletos. Las reglas por URL migran
  también candidatos `new` ya conservados en el estado privado.
- **Hecho:** recuperación aislada de los cinco canales municipales degradados:
  cuatro portadas volvieron con HTTP 200 y tres entradas, y Los Molinos con
  HTTP 200 y 19 entradas; no fue necesario cambiar ni evadir ningún canal.
- **Hecho:** seguimiento acotado de enlaces internos para Apremya y Trinosa,
  cuyas fichas canónicas conocidas faltan en sus sitemaps. Una fixture prueba
  selectores, HTTPS y host; el smoke live produjo solo Puerta de Villalba y
  Etria como `verifiedSource`, sin nuevos pendientes.
- **Hecho:** soporte acotado de índices `sitemapindex`, con límite de 50
  documentos, profundidad máxima dos, HTTPS, allowlist de host y filtros
  `sitemapIncludes`. Siete fuentes conocidas pasan a descubrir dinámicamente
  sus sub-sitemaps pertinentes; la prueba offline descarta colecciones no
  incluidas y hosts externos.
- **Hecho:** smoke live aislado de esos siete índices: Grupo Index 63 URLs,
  Hirimasa 5, Nuvare 24, STANCE 163, Trinosa 13, Altter 18 y Kronos 459; las
  siete fuentes terminaron sanas. Nuvare y STANCE reprodujeron candidatos ya
  gobernados por reglas y revisión privada.
- **Hecho:** ejecución integral `30077326296` con 47/47 fuentes sanas, 29/29
  municipios y 28 canales directos. El falso positivo adicional de El Boalo
  por “promoción interna” quedó rechazado mediante URL exacta.
- **Hecho:** ejecución final `30078678411` con la cola reducida a los cuatro
  candidatos en monitorización, 21/21 fuentes comerciales y Pages desplegado.
  Tres fallos transitorios del radar quedaron aislados y la repetición
  individual recuperó Apremya, STANCE y Los Molinos sin evasión.
- **Hecho:** ejecución `30105161699` tras migrar siete índices: 46/47 fuentes
  sanas, 29/29 municipios, 27 canales directos y cuatro pendientes. El único
  degradado fue `tablon-los-molinos` por HTTP 403; el aislamiento permitió
  publicar 22/22 fuentes comerciales sin ocultar la incidencia.
- **Hecho:** puente privado desde directorio sectorial hacia nuevas fuentes:
  SIMA se consulta mediante su sitemap residencial y dos índices municipales
  acotados, sigue únicamente las fichas seleccionadas y captura enlaces
  externos sin visitarlos ni publicarlos. Las fixtures cubren sitemap con
  espacio inicial, filtro de ficha, índice HTML, allowlist, selectores y
  eliminación de tracking. El smoke live descubrió `Orbia` en Collado Villalba
  y `Nevia` en El Escorial; esta última referencia `aurora-homes.es` como
  dominio todavía no monitorizado. Evaluación en
  `docs/source-assessments/sima-discovery-2026-07-24.md`.
- **Verificado live:** ejecución local integrada con 50/50 fuentes sanas,
  29/29 municipios, 28 canales directos y 15/15 canales comerciales. El embudo
  privado quedó en siete pendientes: tres nuevos y cuatro en seguimiento. Los
  tres nuevos son Orbia, Nevia y una señal oficial de ocho parcelas
  residenciales en Miraflores de la Sierra; ninguno se publicó.
- **Hecho:** revisión jurídica, técnica y de producto de los siete candidatos.
  Orbia y Nevia se descartan por ser plurifamiliares; Nevia está además
  adjudicada al 100 %. Las tres páginas territoriales NUVARE reutilizan la
  ficha de Cumbres de Navalafuente y no son promociones independientes. El
  estudio de detalle de Collado Mediano y la licitación de parcelas de Los
  Pinarejos permanecen en seguimiento.
- **Verificado live:** `op-20260724T173446775Z` aplicó las decisiones al estado
  previo con 50/50 fuentes sanas, cero candidatos nuevos, dos en seguimiento y
  cero dominios externos sin monitorizar. La evaluación está en
  `docs/source-assessments/candidate-review-2026-07-24.md`.
- **Hecho:** `backfill-opportunities` divide rangos arbitrarios sin
  huecos/solapes, exige una ruta privada explícita, conserva los lotes
  correctos ante fallos parciales y deja un informe atómico agregado. La
  comprobación live aislada del 24 de junio al 24 de julio procesó 1.909
  entradas BOCM, 1/1 lote correcto y cero candidatos.
- **Hecho:** `audit-opportunities` genera para una ventana temporal una muestra
  determinista de municipios con señal en un solo canal, huecos y controles
  sin señal. Compara familias central, municipal y comercial, excluye
  candidatos rechazados/obsoletos y no publica títulos ni URLs.
- **Hecho:** el workflow diario crea la muestra privada y cada lunes ejecuta
  un backfill BOCM móvil de 31 días; ambos fallos quedan aislados del dataset
  público. La auditoría sirve para buscar omisiones con evidencia
  independiente y no presenta como métrica una exhaustividad no observada. La
  ejecución real `30081195579` verificó ambos pasos, 21/21 fuentes comerciales,
  Pages correcto y HTTP 404 para el informe privado.
- **Hecho offline:** `triage-opportunities` ordena candidatos pendientes por
  evidencia explicable, incluyendo confirmación geográfica, antigüedad,
  documentos administrativos genéricos y hosts excluidos; resume dominios y
  marca duplicados probables sin cambiar estados. El contrato privado 1.0 y la
  escritura atómica tienen pruebas deterministas.
- **Hecho offline:** exportación manual del triaje con RSA-OAEP-SHA256 +
  AES-256-GCM, AAD separado del enriquecimiento, clave privada efímera local,
  artefacto cifrado de un día y borrado del texto claro en el runner.
- **Hecho live:** `export-private-opportunities.yml` se ejecutó como
  `30121191738` sobre 334 pendientes de 183 dominios: 30 alta, 230 media, 70
  baja y 4 duplicados. El hash del ZIP coincidió, el sobre se descifró y validó
  localmente y la clave privada se eliminó.
- **Hecho:** ocho agregadores que no son fuente primaria se excluyen de futuras
  búsquedas y se rechazan por reglas configuradas que también se aplican al
  estado previo.
- **Hecho live:** la exportación final `30122387245` fijó 10 candidatos de
  prioridad alta, 134 media, 186 baja y 4 duplicados sobre los mismos 334. Los
  diez URL respondieron HTTP 200; nueve rutas son compatibles con
  `robots.txt`. La sede electrónica de Collado Mediano bloquea su ruta y queda
  solo como señal, no como fuente rastreable.
- **Hecho:** revisión de las nueve páginas comerciales de prioridad alta. Solo
  Inara Homes supera identidad, vigencia y alcance unifamiliar; se incorporó
  como fuente directa. Las otras ocho quedan `stale` o `rejected` por fin de
  comercialización, ausencia del resultado, producto plurifamiliar o
  duplicidad con una fuente canónica ya rastreada.
- **Hecho:** las ocho decisiones se codifican como `reviewRules` del canal web
  y el sitemap oficial de Inara se añade al radar, limitado al
  `page-sitemap`. El smoke live procesó tres URLs, cero fallos y cero candidatos
  nuevos. Evaluación en
  `docs/source-assessments/high-priority-commercial-review-2026-07-24.md`.
- **Hecho live:** `30125211739` ejecutó las 116 consultas de la matriz,
  el backfill semanal BOCM y la auditoría muestral. Resultado: 52/52 fuentes
  sanas, 29/29 municipios cubiertos, 28 con canal directo y 339 candidatos
  privados pendientes.
- **Hecho:** la exportación cifrada `30129015499` permitió revisar localmente
  la cola de 339 candidatos sin publicar estado. Los dos de prioridad alta no
  aportaron una fuente nueva; se codificaron once decisiones adicionales
  `stale`, `rejected` o `monitoring` y tres hosts de ruido quedan excluidos.
- **Hecho live:** se admitieron Doce Fresnos, Villa Bárbara, Montelago y
  Miraflores Villas tras comprobar identidad, `robots.txt`, vigencia, alcance
  unifamiliar y extracción HTTP aislada. El perfil comercial pasa a 27 fuentes
  y el radar a 56 canales, con tres sitemaps y un seguimiento interno acotado
  de Galivivienda. Evaluación en
  `docs/source-assessments/private-triage-expansion-2026-07-24.md`.
- **Hecho local:** tras observar HTTP 403 para la portada de Miraflores Villas
  en el runner `30131105415`, se incorporó un adaptador explícito para una
  página de la API REST pública de WordPress. Solo acepta
  `content.rendered`, valida una canónica HTTPS del mismo host y reutiliza los
  selectores cerrados existentes. Una fixture y una integración de pipeline
  elevan la baseline a 145 pruebas; los smokes live aislados recuperan la
  promoción y dejan sano su sitemap directo de dos páginas.
- **Resuelto sin evasión:** `30132324597` confirmó que GitHub recibe HTTP 403
  también en la API REST y el sitemap. La definición comercial, su fixture y
  su evidencia se conservan para comprobación manual, pero se deshabilitan
  tanto la extracción como el canal de radar automáticos. El estado operativo
  queda en 26 fuentes comerciales y 55 canales; no se usa proxy, CAPTCHA,
  navegador evasivo ni copia estática presentada como live.
- **Resuelto sin evasión:** la ejecución programada `30240033410` dejó
  Hirimasa como único fallo comercial. La ficha y la API REST devolvieron
  después HTTP 403 también en local, pero su sitemap sigue sano. Se deshabilita
  únicamente la extracción directa y se conserva el sitemap en el radar; el
  estado operativo pasa a 25 fuentes comerciales y mantiene 55 canales.
- **Verificado live:** el commit `931d3ed` superó el CI `30251889184` y la
  ejecución manual `30251963680` terminó correctamente con 25/25 fuentes
  comerciales, cero fallos, 26 promociones publicadas —24 activas— y 26
  elementos GeoJSON. El radar quedó en 55/55 canales sanos, 29/29 municipios,
  28 canales directos y 419 candidatos privados; las 116 consultas web, el
  backfill BOCM, la auditoría y Pages también finalizaron correctamente. La IA
  se omitió. La promoción histórica de Hirimasa se conserva con una ausencia
  y solo pasará a inactiva tras el umbral seguro de tres.

## P6 — Enriquecimiento verificable de fichas

- **Hecho:** flujo opcional `enrich-promotions` sobre páginas oficiales, con
  seguimiento interno acotado a profundidad uno, máximo tres páginas y
  8.000 caracteres de fragmentos relevantes por promoción.
- **Hecho:** proveedor OpenAI Responses API sin SDK adicional, desactivado si
  falta `OPENAI_API_KEY`, modelo configurable y salida JSON estricta por
  esquema. La baseline, el crawl y la web no requieren clave.
- **Hecho:** perfil económico por defecto con `gpt-5.6-luna`,
  `reasoning.effort=none`, `text.verbosity=low`, `store=false`, máximo de 800
  tokens de salida, tres llamadas y 0,05 USD por ejecución. La estimación
  conservadora se comprueba antes de cada llamada y el uso real queda auditado
  en estado privado.
- **Hecho:** el límite de llamadas se aplica después de la caché de contenido;
  una promoción sin cambios no bloquea la siguiente nueva o modificada.
  `--dry-run` no invoca la API ni escribe estado.
- **Hecho:** cada propuesta exige campo ausente, valor tipado, confianza mínima
  de 0,8, URL exacta y cita literal presente en la evidencia descargada. Los
  campos de identidad no se admiten y el modelo nunca escribe directamente en
  el contrato público.
- **Hecho:** cola atómica y estable en
  `data/state/promotion-enrichment.json`, ignorada por Git y rechazada
  explícitamente por el smoke de publicación.
- **Hecho:** piloto de Actions exclusivamente manual mediante
  `workflow_dispatch`; la programación diaria no consume API. El operador debe
  activar `run_enrichment` y elegir una cohorte de una a tres promociones. Un
  fallo parcial de esta fase queda informado sin bloquear Pages.
- **Hecho:** la evidencia fuerza respuesta completa y no reutiliza el `304`
  condicional del crawl previo; conserva el resto de límites HTTP y dispone de
  una prueba específica.
- **Hecho:** revisión explícita mediante `review-enrichment`; solo propuestas
  aceptadas completan huecos en un crawl posterior, nunca sobrescriben una
  extracción determinista y caducan tras 30 días. Un hash de contenido nuevo
  convierte la aceptación anterior en `stale`.
- **Hecho:** revisión local campo a campo: el comando lista pendientes y puede
  generar un informe HTML compacto dentro de `--state`, con valor, confianza,
  cita literal, fuente y comandos de aceptar/rechazar. No admite aceptación en
  bloque; una propuesta solo queda resuelta cuando todos sus campos tienen
  decisión y el crawl aplica exclusivamente los aceptados. El contrato privado
  avanza a 1.2 y mantiene compatibilidad con decisiones 1.1.
- **Hecho:** recuperación segura desde la caché de Actions mediante un workflow
  manual de solo lectura. Usa RSA-OAEP-SHA256 + AES-256-GCM, sube únicamente
  un sobre autenticado con retención de un día y elimina el texto claro del
  runner. La clave privada efímera permanece local; el descifrado valida JSON,
  escribe atómicamente y puede borrarla tras el éxito.
- **Hecho:** devolución segura de la revisión a la caché mediante un segundo
  workflow manual. Solo recibe identificador, campo y decisión, valida hasta
  100 entradas, usa salida silenciosa y guarda una nueva caché privada sin
  exponer valores, citas ni URLs.
- **Hecho:** prompt endurecido a partir de la primera revisión real: distingue
  viviendas totales de disponibles, precio «desde» de precio máximo y régimen
  de cooperativa de razón social, sin llamadas adicionales.
- **Verificado live:** `30100286300` exportó el sobre cifrado y
  `30101016895` aplicó 4 aceptaciones y 4 rechazos, dejó 0 pendientes y creó
  `crawler-state-30101016895`. Ningún valor, cita o URL de la cola apareció en
  el canal de devolución.
- **Hecho:** fixture de respuesta estructurada y once pruebas offline para
  esquema, parseo, evidencia literal, caducidad, precedencia, caché previa al
  límite, presupuesto, `dry-run` gratuito y persistencia privada.
- **Hecho:** la ejecución real `30093553895` confirmó el secreto y dos
  respuestas HTTP 200: 3.175 tokens de entrada, 412 de salida, nueve campos
  propuestos y 0,006439 USD estimados. Una ficha previa recibió `304` sin cuerpo
  y dejó el job parcial; se corrigió forzando cuerpo y aislando el piloto de
  Pages.
- **Hecho:** la repetición `30094391984` sobre la corrección terminó correcta:
  dos promociones, ocho campos propuestos, cero fallos, 3.567 tokens de entrada,
  409 de salida y 0,006911 USD estimados. Pages también se desplegó y ninguna
  propuesta fue aceptada o publicada. Queda como operación manual revisar
  citas, falsos positivos y contrastar los 0,013350 USD acumulados con el panel.

## P8 — Cobertura de toda la Comunidad de Madrid

- **Hecho local:** catálogo de 179 municipios importado desde el
  `MUNICIPIOS.csv` oficial del NGMEP 2026, con códigos, centroides ETRS89,
  trazabilidad y fixture reducida completa. Los alias y localidades del
  alcance original se conservan.
- **Hecho local:** términos de reconocimiento y exclusión contextual para
  Madrid capital; una mención genérica a la Comunidad de Madrid ya no se
  clasifica como ciudad, mientras que «Madrid» sin contexto regional y la
  pista municipal explícita de SearXNG se conservan.
- **Hecho local:** matriz live ampliada a 179 × 4 = 716 consultas diarias,
  límite global de 10.000 resultados y filtro SIMA con 179 slugs y hasta 200
  fichas. El timeout defensivo del job pasa de 75 a 120 minutos.
- **Hecho local:** catálogo saneado y versionado de 569 actores, con 194
  asociados de ASPRIMA, 378 empresas de SIMA y los canales comerciales ya
  configurados. Solo guarda nombre, web, evidencia, procedencia y estado de
  revisión; 22 están monitorizados y 547 siguen como candidatos. Ningún dato
  de contacto se persiste.
- **Hecho local:** importadores repetibles para geografía, actores y
  sincronización del radar, validación CLI y pruebas offline.
- **Verificado live aislado:** SIMA cambió su ficha a `.entry-content`; se
  añadió fixture y compatibilidad sin retirar selectores anteriores. El smoke
  siguió 37 fichas, generó 10 candidatos privados y terminó con cero fallos.
- **Hecho live:** la ejecución `30270433121` completó la matriz 716/716 en
  15 min 22 s, conservó 609 resultados y dejó 833 candidatos privados. El
  workflow completo terminó en 20 min 34 s con 55 fuentes de radar sanas,
  cobertura 179/179, 28 canales directos, 26 promociones y 25/25 fuentes
  comerciales correctas. La IA permaneció desactivada.
- **Hecho live:** Pages se desplegó y los contratos públicos devolvieron HTTP
  200; los tres estados privados comprobados devolvieron HTTP 404. La interfaz
  cargó 22 promociones activas.
- **Hecho local y smoke live:** se descifró de forma segura el triaje de 833
  candidatos y se revisó el primer lote por dominio. Se incorporan Insur
  Calíope, Nuevo Brunete, Cielo Arroyomolinos y NovaCiempozuelos con cuatro
  fixtures específicas; sus cuatro fichas y sus canales de radar oficiales
  respondieron sin fallos en ejecuciones secas.
- **Hecho local (primer lote):** el radar pasa a 60 canales —59 activos— y el
  perfil comercial a 31 fuentes —29 automáticas—. El catálogo interno se
  sincroniza a 569 actores, 197 con web y 20 monitorizados. No contiene
  contactos ni se publica.
- **Hecho local y smoke live:** una segunda revisión incorpora Los Molinos y
  Los Molinos II de Grupo Impulsa en Getafe y Arroyo Homes de Bimarán en
  Arroyomolinos. Las tres fichas respondieron HTTP 200 y extrajeron una
  promoción cada una; el sitemap de Impulsa procesó 11 entradas y el índice
  HTML acotado de Bimarán cinco, sin fallos.
- **Hecho local:** el perfil live queda en 34 fuentes —32 automáticas— y el
  radar en 62 canales —61 activos—, con 21 sitemaps y cinco índices HTML
  comerciales oficiales. El catálogo conserva 569 actores, 197 con web y 22
  monitorizados.
- **Hecho local:** se codifican decisiones para promociones vendidas,
  entregadas o plurifamiliares y se mantienen dos fichas ambiguas en
  `monitoring`. El triaje deja de elevar «promoción económica» como promoción
  inmobiliaria y penaliza resultados administrativos sin evidencia
  residencial.
- **Verificado live:** el commit `f14a9d2` superó el CI `30278248263` y la
  ejecución `30279016501` completó 716/716 consultas, conservó 612 resultados,
  dejó 61/61 canales sanos, cobertura 179/179 y 32/32 fuentes comerciales
  correctas. Publicó 33 promociones —26 activas— y 33 puntos GeoJSON sin
  fallos; la IA se omitió.
- **Verificado privado:** la exportación cifrada `30281037199` clasificó 865
  pendientes en 91 alta, 260 media, 497 baja y 17 duplicados. Alta y media
  bajaron desde 98/298 pese a incorporar señales nuevas. El informe detallado
  no se publicó y la clave efímera local se eliminó.
- **Verificado Pages:** `runId` `20260727T153254903Z`; portada y contratos
  públicos HTTP 200, con los tres estados privados comprobados en HTTP 404.
- **Evidencia:** evaluación y límites en
  `docs/source-assessments/madrid-expansion-2026-07-27.md`.
- **Incidencia live corregida:** la ejecución `30269094453` desplegó Pages,
  pero BOCM agotó el timeout antes de alcanzar la matriz. El radar confundía
  ese timeout interno con cancelación global y el resumen confundía el
  arranque SearXNG con las consultas. Ahora el timeout queda aislado por fuente,
  la cancelación externa conserva su semántica y el resumen exige un `lastRun`
  nuevo con la fuente web correcta antes de acreditar 716/716. La repetición
  `30270433121` verificó la corrección.
- **Criterio de aceptación:** 179 municipios/centroides validados, 716
  consultas configuradas sin muestreo, inventario sin datos personales,
  baseline offline verde y workflow live observado sin pérdida del último
  dataset válido.

## Matriz del encargo original

| # | Criterio | Estado | Evidencia o siguiente paso |
|---:|---|---|---|
| 1 | Compila en .NET 10 | Hecho | SDK fijado y build Release correcto |
| 2 | Todos los tests pasan | Hecho | 157/157 en la entrega |
| 3 | Crawler ejecutable localmente | Hecho | CLI y scripts |
| 4 | Crawler offline contra fixtures | Hecho | 4 promociones sintéticas |
| 5 | Fuente real permitida con Internet | Hecho | 31 revisadas; 29 automáticas y 2 manuales por bloqueo |
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
| 16 | Enlaces a webs originales | Hecho | las 21 fichas live publican su URL oficial y la UI expone el enlace |
| 17 | Action manual | Hecho | `workflow_dispatch` validado por actionlint |
| 18 | Action programada | Hecho | diaria 06:17 `Europe/Madrid` |
| 19 | Deploy Pages | Hecho | ejecución `30054208393` y URL pública verificadas |
| 20 | Subpath del repositorio | Hecho | `/SierraNueva/` en artefacto |
| 21 | `.nojekyll` | Hecho | generado y verificado |
| 22 | Fallback SPA | Hecho | `404.html` generado y verificado |
| 23 | Sin API keys obligatorias | Hecho | baseline offline |
| 24 | Sin secretos en el repo | Hecho | configuración no sensible |
| 25 | Portales excluidos bloqueados | Hecho | blocklist y pruebas |
| 26 | Fallo parcial no destruye dataset | Hecho | reglas y pruebas de estado |
| 27 | README permite ejecutar desde cero | Hecho | scripts y comandos manuales |
| 28 | Sin código esencial pendiente | Hecho | vertical local, cobertura P1/P2 y registro continuo P5 completos; ampliación de fuentes incremental |
| 29 | Repo limpio y estructurado | Hecho | monorepo y Git local |
| 30 | `dotnet test` ejecutado e informado | Hecho | 157/157 en la entrega |

## Fuera de esta hoja de ruta inmediata

Notificaciones, usuarios, administración web, API pública, base cloud,
aplicación móvil, OCR, imágenes, hipotecas, inversión y comparación con
portales siguen fuera del MVP. La IA solo entra como propuesta privada y
evidenciada de campos; no sustituye la extracción ni la revisión humana.
