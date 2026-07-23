# Especificación consolidada de SierraNueva

Este documento conserva el contexto funcional del encargo original dentro del
repositorio. Es la referencia para continuar el producto sin depender del chat
inicial. GitHub, Actions, Pages y la operación diaria fueron autorizados
expresamente el 23 de julio de 2026 después de cerrar la baseline local.

## 1. Producto

SierraNueva localiza, rastrea, normaliza y presenta promociones de obra nueva
unifamiliar en la Sierra de Madrid. Debe priorizar:

- promotoras y micrositios oficiales;
- gestoras de cooperativas;
- comercializadoras exclusivas;
- constructoras que comercialicen promociones propias;
- fuentes municipales o urbanísticas públicas.

Se excluyen Idealista, Fotocasa, Pisos.com, Yaencontre, Habitaclia,
Milanuncios, Trovit y agregadores equivalentes.

El producto tiene dos piezas: un crawler/ETL de consola y una SPA Blazor
WebAssembly standalone que consume archivos estáticos. El diseño final debe
poder vivir en GitHub Pages sin backend, base de datos remota ni APIs de pago.

## 2. Restricciones no negociables

- .NET 10 LTS, C#, nullable reference types y una solución mantenible.
- Un solo frontend Blazor WebAssembly; JavaScript únicamente para integración
  necesaria, principalmente Leaflet.
- Leaflet con cartografía basada en OpenStreetMap y atribución visible.
- `HttpClient` y análisis HTML estático como primera opción.
- Microsoft Playwright para .NET solo como fallback configurado por fuente.
- Nada de Selenium, evasión, fingerprints falsos, CAPTCHA, autenticación,
  paywalls, áreas privadas, proxies o scraping de buscadores comerciales.
- Respetar `robots.txt`, condiciones aplicables y límites de frecuencia.
- No descargar imágenes, guardar HTML completo ni publicar datos personales
  innecesarios.
- Ninguna API de pago, clave obligatoria, backend HTTP, Firebase, Azure o base
  de datos cloud en el MVP.
- El sistema continúa siendo útil ante fallos parciales y conserva el último
  dataset válido ante un fallo total.
- No presentar inferencias o centroides como datos exactos.
- No dejar implementaciones esenciales vacías.

## 3. Arquitectura esperada

El monorepo contiene:

```text
src/
  SierraNueva.Contracts
  SierraNueva.Core
  SierraNueva.Infrastructure
  SierraNueva.Crawler
  SierraNueva.Web
tests/
  SierraNueva.Core.Tests
  SierraNueva.Infrastructure.Tests
  SierraNueva.Web.Tests
config/
data/public/
data/state/
test-data/
scripts/
docs/
```

Responsabilidades:

- `Contracts`: DTOs, enums y contratos públicos versionados, sin
  infraestructura.
- `Core`: interfaces, normalización, identidad, deduplicación, reglas,
  confianza, cambios, calidad y orquestación.
- `Infrastructure`: HTTP, robots, descubrimiento, AngleSharp, PDF, Playwright,
  geocodificación y persistencia en archivos.
- `Crawler`: CLI, composición, logging y códigos de salida.
- `Web`: SPA estática, filtros, listado, detalle, estado y mapa.

## 4. Contrato público

`promotions.json` es el contrato canónico y versionado. `PromotionDataset`
incluye versión, fecha UTC, `runId`, recuentos de fuentes, promociones y
estadísticas.

Cada promoción admite:

- identidad: `id`, nombre original y normalizado;
- ubicación: municipio, localidad, dirección, código postal, coordenadas y
  precisión;
- producto: tipologías, dormitorios, baños, superficies útiles y construidas,
  parcela, garajes y piscinas;
- estado: comercial, construcción, licencia, entrega y disponibilidad;
- actores: promotora, comercializadora y cooperativa;
- economía: precios decimales en EUR y número de viviendas;
- procedencia: tipo y confianza de fuente, URL canónica, URLs relacionadas y
  dossiers;
- histórico: primera/última detección, último cambio, ausencias consecutivas y
  activo;
- calidad: etiquetas, advertencias y evidencias.

La precisión de ubicación distingue coordenadas exactas, calle, zona,
localidad, centroide municipal y desconocida. La fuente distingue promotora
oficial, micrositio oficial, gestora, comercializadora exclusiva, constructora,
autoridad pública y desconocida. El estado comercial contempla anunciada,
próxima, preventa, en venta, últimas unidades, agotada, finalizada, pausada y
desconocida.

Cada evidencia conserva campo, valor breve, URL, instante UTC, extractor,
confianza, calidad y fragmento de texto saneado. Los importes son decimales, las
fechas de almacenamiento son ISO 8601 UTC y la UI presenta en `es-ES` y horario
de Madrid.

## 5. Identidad, normalización y deduplicación

La identidad prioriza URL oficial canónica; después nombre + municipio +
promotora; y después dirección o zona + municipio + promotora. El identificador
final es un hash SHA-256 truncado y determinista.

Se normalizan mayúsculas, tildes para comparación, espacios, puntuación,
tracking y barra final de URL, nombres societarios y alias geográficos. Se
conserva la grafía original para presentación. Un posible duplicado ambiguo no
se fusiona: se conserva y genera una advertencia.

## 6. Cobertura geográfica inicial

Los 29 municipios editables son:

Alpedrete, Becerril de la Sierra, Bustarviejo, Cabanillas de la Sierra,
Cercedilla, Collado Mediano, Collado Villalba, El Boalo, El Escorial,
Fresnedillas de la Oliva, Galapagar, Guadalix de la Sierra, Guadarrama, Hoyo de
Manzanares, La Cabrera, Los Molinos, Manzanares el Real, Miraflores de la
Sierra, Moralzarzal, Navacerrada, Navalafuente, Navalagamella, Robledo de
Chavela, San Lorenzo de El Escorial, Santa María de la Alameda, Soto del Real,
Torrelodones, Valdemaqueda y Zarzalejo.

Cada entrada admite nombre oficial, alias, localidades, centro, bounding box,
estado habilitado y términos de búsqueda sin recompilar.

## 7. Fuentes y descubrimiento

Todas las fuentes viven en `config/sources.json` con identidad, URL base,
estado, tipo, hosts permitidos, URLs iniciales, robots, sitemaps, enlaces
internos, profundidad, límites, demora, Playwright, pistas de municipio,
patrones, selectores y notas. Una URL no verificada permanece deshabilitada
como ejemplo; nunca se inventa ni se habilita silenciosamente.

`IUrlDiscoveryProvider` cubre:

- URLs iniciales configuradas;
- sitemaps e índices de sitemap con límites;
- enlaces internos relevantes, normalizados y sin bucles;
- archivo manual JSON o TXT.

La extensión futura prevista incluye SearXNG propio, Common Crawl, BOCM y
transparencia municipal. No se raspan resultados de Google, Bing o DuckDuckGo.

La blocklist es configurable, se aplica antes de descargar y también a enlaces
descubiertos.

## 8. Rastreo y extracción

El rastreo usa `IHttpClientFactory`, User-Agent identificable, timeout,
reintentos y backoff limitados, ETag/Last-Modified, caché de metadatos, límites
globales y por host, demora por dominio, límites de tamaño y tipo, redirecciones
limitadas, cancelación y cierre ordenado. Se evitan esquemas peligrosos, redes
privadas, bucles, parámetros infinitos y binarios no admitidos.

El orden de extracción es:

1. JSON-LD.
2. Microdatos y metadatos.
3. OpenGraph.
4. HTML semántico.
5. patrones de texto españoles;
6. PDF comercial;
7. Playwright como fallback.

Debe reconocer importes españoles, metros cuadrados y entregas por trimestre
sin confundir cuotas con precio total, aportaciones de cooperativa con PVP,
parcela con superficie construida o textos históricos con disponibilidad.
Nunca se completan valores ausentes por intuición.

Playwright usa Chromium headless, bloquea recursos innecesarios, tiene timeout
y libera página, contexto y navegador. Solo se activa si la fuente lo permite,
el ajuste global está activo y el HTML inicial resulta insuficiente.

## 9. Geolocalización

Prioridad: coordenadas explícitas en JSON-LD; scripts/atributos de mapa; enlaces
de mapas; dirección completa; centroide de localidad; centroide municipal; o
sin coordenadas. Siempre se registra `LocationPrecision`.

El centroide municipal es obligatorio y trazable. Nominatim es opcional,
deshabilitado de fábrica, con User-Agent real, caché persistente, una petición
simultánea y como máximo cuatro peticiones por minuto. No se usa desde el
navegador, como autocomplete ni para barridos de cuadrícula.

## 10. Confianza y calidad

`SourceConfidence` es un score de 0 a 100 conceptualmente —serializado en el
contrato actual como decimal de 0 a 1— y debe apoyarse en señales verificables:
dominio corporativo, enlace desde la matriz, identidad legal, email de dominio,
dossier, memoria, datos societarios y configuración oficial. Penalizan
agregación, contenido replicado, identidad oculta, falta de aviso legal,
redirección a portales y formularios de captación.

La apariencia profesional no basta para clasificar una fuente como oficial.
La evolución del modelo debe conservar una explicación resumida de las señales.

Las validaciones incluyen rangos de precio, superficies, dormitorios y
coordenadas; URL obligatoria; advertencia para municipio desconocido; y etiqueta
aproximada para centroides. Un coste estimado de cooperativa no se muestra como
PVP cerrado. `run.json` incluye el informe de calidad.

## 11. Estado, cambios y salidas

El estado legible vive en JSON dentro de `data/state`. Se comparan ejecuciones y
se detectan altas, precio, disponibilidad, estados, entrega, licencia,
dirección, coordenadas, fuentes, dossiers, reaparición y posible desaparición.
Una promoción se desactiva inicialmente tras tres ausencias consecutivas en
ejecuciones completas; un fallo de fuente no cuenta como ausencia.

Las salidas atómicas y estables son:

```text
data/public/promotions.json
data/public/promotions.csv
data/public/promotions.geojson
data/public/changes.json
data/public/run.json
data/state/promotions-state.json
data/state/geocoding-cache.json
data/state/http-cache.json
```

GeoJSON contiene solo promociones geolocalizadas. El histórico público tiene
un límite configurable. Si toda la ejecución falla, no se reemplaza el dataset
válido. Un éxito parcial publica las fuentes correctas, informa las fallidas y
no provoca bajas falsas.

## 12. CLI

Comandos:

```text
SierraNueva.Crawler crawl
SierraNueva.Crawler validate-config
SierraNueva.Crawler validate-data
```

`crawl` admite rutas de configuración, fuentes, municipios, exclusiones,
salida y estado; filtros por municipio/fuente; máximo de páginas; desactivar
Playwright o geocodificación; `dry-run`; y logging detallado.

Códigos: `0` éxito, `1` éxito parcial, `2` configuración inválida, `3` fallo
total/cancelación y `4` datos públicos inválidos.

## 13. SPA

Blazor carga `run.json`, `promotions.json`, `changes.json` y
`promotions.geojson`. La portada muestra fecha, ejecución, totales, cambios,
errores, filtros, listado, mapa y descarga CSV.

Filtros: texto, municipios múltiples, localidad, tipología, precios,
dormitorios, superficie construida, parcela, estados, tipo de fuente,
confianza, activas, con precio, ubicación exacta, nuevas y modificadas.
Ordenaciones: recientes, precio, parcela, superficie, confianza y municipio.
Los filtros se reflejan en la query y una única colección filtrada alimenta
listado y mapa.

Cada tarjeta resume producto, precio, superficies, estado, entrega, actores,
fuente, confianza, comprobación, novedad/cambio, precisión y enlace directo.
El detalle presenta todos los datos disponibles, fuentes, evidencias, historial
y advertencias. Nunca renderiza HTML de terceros.

Leaflet consume GeoJSON, usa teselas configurables y atribución visible, ajusta
el encuadre a los resultados y diferencia exacto/aproximado/centroide. El
listado sigue funcionando si falla el mapa. En móvil se puede alternar entre
mapa y resultados.

La UX es española, responsive, operable con teclado, con etiquetas, contraste,
carga, vacío y errores accionables, limpieza de filtros y avisos de datos
antiguos o ejecución parcial.

## 14. Pruebas, seguridad y rendimiento

Las pruebas xUnit son deterministas y no dependen de Internet. Deben cubrir
normalización, deduplicación, confianza, extracción, cambios, pipeline,
persistencia, CSV, GeoJSON, contratos, geocodificación, PDF, robots, sitemaps,
URLs bloqueadas, saneamiento, componentes Blazor y salida publicada.

Solo se permiten HTTP/HTTPS y hosts autorizados. Se bloquean localhost, redes
privadas, metadata y esquemas alternativos; se limitan HTML/PDF; no se
deserializan tipos arbitrarios, guardan cookies ni desactiva TLS. No se
registran tokens, cookies, formularios, datos personales ni HTML completo.

El frontend filtra en cliente y nunca recibe HTML/PDF. Hay cachés HTTP y de
geocodificación, escritura determinista y ausencia de descargas repetidas
innecesarias. Playwright no se inicia para fuentes estáticas.

## 15. Fuera del MVP

Telegram, email, usuarios, login, administración online, edición web de
fuentes, base de datos cloud, API pública, aplicación móvil, comparación con
portales, modelos de IA, LLM local, OCR, imágenes, valoración de inversión,
hipotecas, redes sociales, proxies, CAPTCHA y multiusuario.

La arquitectura puede admitirlos en el futuro, pero no deben introducirse por
anticipado.

## 16. Infraestructura final

La fase incorpora dos workflows:

- CI para restore, build, test, formato y configuración, siempre con fixtures.
- Crawl y despliegue manual/programado, actualización segura de datos y
  publicación con GitHub Pages.

La implementación usa permisos mínimos, concurrencia, horario diario 06:17
`Europe/Madrid`, resumen, acciones fijadas, `base href` para project site,
`.nojekyll`, `404.html`, artefacto con `data/public` y exclusión de
`data/state`. El estado live se mantiene en caché privada de Actions, no en
commits del bot.

La activación real de Pages depende de hacer público el repositorio o usar un
plan que admita Pages privadas. Cambiar la visibilidad requiere confirmación
expresa del propietario.
