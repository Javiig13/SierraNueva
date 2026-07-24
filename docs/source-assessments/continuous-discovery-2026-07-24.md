# Descubrimiento continuo por sitemaps oficiales

Fecha de revisión: **24 de julio de 2026**.

## Objetivo

Complementar el radar administrativo con una frontera comercial que pueda
detectar nuevas fichas sin ampliar automáticamente el dataset público. Solo se
revisaron dominios que ya tenían una fuente comercial aprobada en
`config/sources.live.json`.

## Reglas aplicadas

- Se consultó `robots.txt` con el User-Agent identificable de SierraNueva.
- Se prefirió el sitemap declarado por el propio dominio. Cuando no existía una
  directiva, se comprobó un endpoint estándar y se seleccionó únicamente un
  `urlset` específico y acotado.
- El adaptador admite exclusivamente HTTPS y descarta cualquier URL cuyo host
  no esté en `allowedHosts`.
- Solo se leen URL, `lastmod` y títulos incluidos dentro del XML. No se
  descargan imágenes ni HTML de las fichas.
- Municipio y señales como `promoción`, `promociones` u `obra nueva` se
  resuelven antes de crear el candidato.
- Una URL que ya aparece en el registro comercial se marca
  `verifiedSource`. Cualquier otra permanece `new` y requiere revisión técnica
  y jurídica.
- Todo el resultado vive en `data/state`; no se incorpora a Pages.

## Fuentes incorporadas

| Fuente | Sitemap acotado | URLs | Coincidencias |
|---|---|---:|---:|
| EXXACON | `promocion-sitemap.xml` | 30 | 0 |
| Antaro | `promocion-sitemap.xml` | 13 | 1 |
| Apremya Homes | `wp-sitemap-posts-page-1.xml` | 12 | 0 |
| Grupo Index Madrid | `page-sitemap.xml` | 63 | 0 |
| Hirimasa | `wp-sitemap-posts-page-1.xml` | 5 | 0 |
| NUVARE | `page-sitemap.xml` | 24 | 4 |
| Residencial Alpedrete | `page-sitemap.xml` | 26 | 0 |
| STANCE Homes | `page-sitemap.xml` | 163 | 10 |
| Trinosa | `promocion-sitemap.xml` | 13 | 0 |
| Altter | `property-sitemap.xml` | 18 | 0 |
| Kronos Homes | `sitemap-negocio.xml` | 459 | 0 |
| La Quinta de Manzanares | `sitemap.xml` | 4 | 0 |
| Vesari | `pages-sitemap.xml` | 9 | 0 |

El smoke aislado procesó **839 URLs**, con **13/13 fuentes correctas** y 15
coincidencias. Tres coincidían exactamente con Cumbres de Navalafuente,
Essentia y Osnola y quedaron `verifiedSource`. Las otras 12 permanecen en la
cola privada: una página de Antaro en Guadarrama, tres de NUVARE y ocho de
STANCE. Entre ellas figura `Promoción Residencial Real Torrelodones`; las
páginas genéricas “obra nueva en…” no demuestran por sí solas que exista una
promoción vigente.

## Fuentes no incorporadas

- **Gilmar:** el índice publica varios sitemaps de inventario `property` que
  mezclan tipologías y segunda mano. No se incorporan hasta disponer de un
  filtro que aísle obra nueva oficial sin convertir el radar en un portal
  inmobiliario general.
- **Los Pinarejos:** `robots.txt` y `/sitemap.xml` devolvieron 404.
- **Residencial Montemilano:** `/sitemap.xml` respondió HTTP 200 sin contenido
  utilizable.

Estas exclusiones no eliminan las fichas comerciales ya aprobadas; únicamente
evitan prometer una frontera de descubrimiento que no se ha podido acotar.

## Conclusión

La incorporación es apta para el radar privado. El sitemap aporta detección
temprana y barata, pero no prueba disponibilidad, carácter unifamiliar ni
vigencia comercial. Ningún candidato debe pasar a `sources.live.json` sin
resolver la página oficial y añadir su fixture de extracción.

## Revisión de los 14 candidatos pendientes

La cola se reconstruyó el 24 de julio con las cinco fuentes que habían
producido hallazgos en Actions. Las páginas comerciales se consultaron de forma
secuencial con el User-Agent identificable; no se accedió a la ficha PCSP ni al
documento municipal porque sus `robots.txt` no permiten esas rutas.

Las decisiones se incorporaron como reglas por URL en el catálogo live para
que también se apliquen a candidatos `new` ya existentes en la caché privada:

| Decisión | Cantidad | Evidencia |
|---|---:|---|
| `monitoring` | 4 | Estudio de detalle de Collado Mediano y tres páginas NUVARE con producto residencial, pero sin evidencia suficiente de promoción independiente |
| `rejected` | 8 | Un contrato musical de PCSP que coincidía por “promoción del turismo” y siete landings municipales SEO de STANCE |
| `stale` | 2 | Antaro Guadarrama declara la promoción entregada; REAL 15 de STANCE figura finalizada y vendida |

Las tres coincidencias restantes eran Cumbres, Essentia y Osnola y conservan
`verifiedSource`. Un smoke aislado reprodujo exactamente la distribución
esperada: 4 en monitorización, 8 rechazadas, 2 obsoletas y 3 verificadas.

La primera ejecución integral posterior (`30077326296`) detectó además un
candidato nuevo en la portada de El Boalo. El anuncio es una provisión de plaza
de coordinador de deportes por **promoción interna**, no una oportunidad
residencial. Se revisó y rechazó mediante su URL exacta; la regla evita
reinterpretar el término laboral sin convertirlo en una exclusión global que
pudiera ocultar otros anuncios legítimos.

## Recuperación de los cinco canales municipales

Se repitieron individualmente y sobre estado temporal los cinco canales
degradados en la ejecución `30054208393`:

| Canal | Respuesta | Entradas |
|---|---:|---:|
| El Boalo — portada de sede | HTTP 200 | 3 |
| El Escorial — portada de sede | HTTP 200 | 3 |
| Guadarrama — portada de sede | HTTP 200 | 3 |
| Torrelodones — portada de sede | HTTP 200 | 3 |
| Los Molinos — tablón eAdmin | HTTP 200 | 19 |

Los cinco terminaron sanos sin cambiar URL, selector, cabeceras ni frecuencia.
La evidencia indica fallos transitorios del proveedor, no un bloqueo que
justifique evasión o un adaptador alternativo.

## Seguimiento interno acotado

Se compararon las URLs comerciales ya aprobadas con los trece sitemaps. Dos
omisiones disponían además de un índice HTML público, estático y permitido:

- Apremya enlaza la URL canónica de Puerta de Villalba desde su portada, pero
  el sitemap contiene una variante distinta.
- Trinosa enlaza Etria desde su portada, aunque `promocion-sitemap.xml` la
  omite.

El formato `htmlLinks` consulta únicamente esa portada, aplica selectores CSS
declarados, no sigue más de un salto ni descarga las fichas destino y conserva
solo enlaces HTTPS del mismo conjunto de hosts. El smoke live obtuvo cuatro
enlaces Apremya y ocho Trinosa; tras el filtro territorial produjo únicamente
Puerta de Villalba y Etria, ambas `verifiedSource`, sin nuevos pendientes.

No se habilitó seguimiento en Altter, Los Pinarejos ni Montemilano: el primero
no expone un índice HTML estático acotable y los otros dos no publican enlaces
internos a fichas independientes. Añadir solicitudes allí no incrementaría la
cobertura demostrable.
