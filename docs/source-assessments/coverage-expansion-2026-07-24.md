# Ampliación de cobertura del 24 de julio de 2026

Fecha de revisión: **24 de julio de 2026**.

## Decisión y alcance

Se incorporan cinco fichas públicas de promoción unifamiliar: Cumbres de
Navalafuente, Residencial Claveles, Essentia Galapagar, Residencial Osnola y
Residencial Montemilano. El perfil live pasa de 16 a 21 fuentes/promociones y
de 13 a 16 municipios con al menos una fuente comercial.

Cada fuente queda limitada a una URL, profundidad cero, `robots.txt`, una
pausa mínima de cinco segundos y HTML estático. No se descargan imágenes,
planos, dosieres ni formularios y no se usa Playwright. El perfil
predeterminado continúa siendo completamente offline.

## Nuvare — Cumbres de Navalafuente y Residencial Claveles

- Fichas:
  <https://nuvare.es/promocion-cumbres-de-navalafuente/> y
  <https://nuvare.es/promocion-residencial-claveles/>.
- Aviso legal: <https://nuvare.es/aviso-legal/>.
- Robots: <https://nuvare.es/robots.txt>.
- Identidad: el aviso legal identifica a Investa 23 S.L., CIF B56770035, con
  domicilio y contacto publicados. La marca comercial visible es Nuvare.
- Cumbres: dos viviendas disponibles, unifamiliares pareadas sobre parcelas de
  500 m², cuatro dormitorios principales y precios publicados de 512.500 a
  543.000 euros.
- Claveles: unifamiliares en Zarzalejo, parcelas publicadas entre 150 y 250 m²,
  tres dormitorios principales más una estancia, piscina comunitaria y tabla
  de precios de 399.000 a 460.000 euros.

Las páginas Elementor repiten galerías y tablas. La configuración selecciona
solo cuatro bloques obligatorios por ficha: identidad, descripción,
disponibilidad cuando existe y precios. De este modo no se trunca la tabla ni
se confunden contadores de carrusel con el total de viviendas. Si cambia un
identificador de bloque, la fuente falla de forma cerrada.

Cumbres mezcla en una misma tabla una vivienda acabada y otra con entrega
futura. El contrato no modela estado por unidad, por lo que
`constructionStatus` permanece desconocido; sí conserva disponibilidad,
precios y la fecha textual como evidencia. En Claveles no se cuenta el número
de filas de la tabla para inventar totales o disponibilidad agregada.

## STANCE Homes — Essentia Galapagar y Residencial Osnola

- Fichas:
  <https://stancehomes.es/promocion-essentia-galapagar> y
  <https://stancehomes.es/promocion-osnola-zarzalejo>.
- Aviso legal: <https://stancehomes.es/aviso-legal>.
- Robots: <https://stancehomes.es/robots.txt>.
- Identidad: el aviso legal identifica a Grupo Bizup S.L., CIF B86797263, con
  datos registrales, domicilio y contacto.
- Essentia: cuatro viviendas unifamiliares independientes y cuatro disponibles,
  3–4 dormitorios, superficies construidas de 340 a 500 m², parcelas de 512 a
  1.131 m², piscina privada y precios de 875.000 a 985.000 euros. La ficha
  declara comercialización en curso.
- Osnola: viviendas unifamiliares adosadas desde 221 m² sobre parcelas desde
  250 m², cuatro dormitorios, tres baños, licencia obtenida y obra en curso.
  Precio y total de unidades permanecen nulos.

Solo se procesa el elemento `main`; navegación, pie, legales, formularios,
imágenes y enlaces de descarga no forman parte de la extracción.

## Residencial Montemilano — Bustarviejo

- Ficha pública: <https://residencialmontemilano.es/registro>.
- Empresa ejecutora identificada:
  <https://geyn.es/aviso-legal/>.
- Robots: <https://residencialmontemilano.es/robots.txt>.
- Encaje: cinco viviendas de obra nueva, tres dormitorios, modelos desde
  106,45 m², parcelas de 425 a 450 m² y entrega textual prevista para el primer
  semestre de 2026.
- Identidad: el micrositio atribuye la ejecución a GEYN Construcciones
  Técnicas. Su web corporativa identifica a GEYN Construcciones Técnicas S.L.,
  CIF B86301421. El micrositio no atribuye una promotora distinta, de modo que
  SierraNueva no inventa `developerName`.

La ruta se llama `/registro`, pero devuelve directamente una página pública y
no exige cuenta, formulario ni autenticación para consultar la promoción. Se
clasifica como `officialMicrosite`, no como promotora.

La página incluye en formularios bandas de inversión de 260.000 a 360.000
euros. No son precios de las viviendas. La fuente exige el bloque principal,
los dos modelos, la identidad de la constructora, la disponibilidad y las
preguntas frecuentes, y excluye por completo ambos formularios. La fixture
contiene una banda de presupuesto fuera de los bloques seleccionados y
comprueba que `priceFrom` permanece nulo.

## Radar municipal

Se incorporan tres canales públicos que faltaban:

- actualidad HTML del Ayuntamiento de Collado Villalba;
- RSS oficial del Ayuntamiento de Guadalix de la Sierra;
- RSS oficial del Ayuntamiento de Navalafuente.

Cada formato tiene fixture reducida con una señal urbanística y una noticia de
ruido. Los smokes live procesaron respectivamente 4, 6 y 10 entradas con cero
fallos y cero candidatos el día de revisión. El radar municipal directo pasa
de 25/29 a 28/29 municipios y el catálogo completo de 29 a 32 fuentes.

Robledo de Chavela no se incorpora como canal municipal directo. El RSS
WordPress devuelve HTTP 403 al `HttpClient` y User-Agent reales del producto;
la sede `robledodechavela.sedelectronica.es` responde “Sede Electrónica
Inactiva”, y la nueva sede `eadministracion.es` entrega una aplicación
JavaScript sin avisos en el HTML estático. No se imitan cabeceras de navegador,
no se evade el filtro y no se declara una fuente que falla. BOCM, BOE y PCSP
siguen aportando vigilancia central para el municipio.

## Correcciones generales derivadas de la prueba live

Las tablas de Nuvare revelaron que el patrón de precios podía unir una columna
decimal anterior con un precio separado por espacios. El patrón conserva
`399 000 euros`, pero ya no concatena columnas como `126 399.000 €`.

También se amplió de forma acotada la distancia entre la palabra “parcela” y
su primer número para expresiones como “Parcelas privadas: terrenos exclusivos
desde 512 m²”, se admiten millares como `1.131 m²`, se reconoce “licencia
obtenida”, se separa el número total del contador “viviendas disponibles” y se
admite el orden “Viviendas: 4 disponibles”. Todas las variantes quedan
cubiertas por fixtures.

## Candidatas no incorporadas

- **Qhomes — Situación 11, Navacerrada:** la web corporativa permanece
  accesible, pero no aporta una disponibilidad actual verificable y conserva
  contenido antiguo; señales de terceros indican agotamiento. No se convierte
  una página histórica en oportunidad vigente.
- **Puerta de Abantos, El Escorial:** sigue mezclando 26 chalets con 16 bajos
  y dúplex en un único inventario. El contrato no puede aislar con rigor el
  subconjunto unifamiliar.
- **Los Herrenes, Soto del Real:** la ficha encontrada describe una vivienda
  individual, no una promoción con inventario defendible.
- **Navalagamella S. Coop. Mad.:** continúan existiendo anuncios de terceros,
  pero no una ficha oficial pública de la cooperativa o gestora.
