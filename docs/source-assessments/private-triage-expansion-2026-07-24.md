# Ampliación desde el triaje privado

Fecha de revisión: **24 de julio de 2026**.

## Alcance y resultado

Se exportó y descifró localmente el triaje privado de la ejecución
`30129015499`. El sobre y el ZIP coincidieron en SHA-256, la clave privada
efímera se eliminó después del descifrado y el texto claro permanece solo bajo
`tmp/`, ignorado por Git. La fotografía contiene 339 candidatos pendientes:
2 de prioridad alta, 146 media, 185 baja y 6 duplicados probables, repartidos
en 189 dominios.

Los dos candidatos altos no aportaron una promoción nueva:

- el estudio de detalle de Collado Mediano continúa como señal administrativa
  en `monitoring`;
- la noticia de Madridiario describe Jardines de la Torre, cuya fuente
  primaria de Via Appia publica pisos de 2 a 5 dormitorios. Queda `rejected`
  por producto fuera del alcance unifamiliar.

La revisión de candidatos medios y de sus enlaces oficiales permitió admitir
cuatro fichas públicas vigentes. Cada fuente usa una URL, profundidad cero,
cinco segundos de pausa, `robots.txt`, HTML estático y selectores cerrados. No
se enviaron formularios ni se descargaron imágenes, planos o memorias.

## Fuentes admitidas

| Fuente | Alcance publicado | Decisión técnica |
|---|---|---|
| Doce Fresnos — Moralzarzal | 12 pareados, desde 465.000 € + IVA, 4 dormitorios, más de 180 m² construidos y parcelas desde 375 m². | Micrositio público; `robots.txt` permite la ruta y declara sitemap. Se aíslan cuatro bloques y se excluyen formulario y galería. |
| Residencial Villa Bárbara — Manzanares el Real | 15 pareados, 4 dormitorios, 139 m² construidos, parcelas de 189–404 m², dos plazas de garaje, piscina comunitaria, licencia concedida y entrega 2028. | Ficha de Galivivienda S. Coop. Se omite el bloque de adjudicación porque la misma página muestra contadores incompatibles; disponibilidad y precio quedan nulos. |
| Montelago Residencial — Manzanares el Real | Adosados de 4 dormitorios y 4 baños, dos plazas, piscina comunitaria y precios de 543.500–588.100 €. | Ficha vigente marcada como disponible. La tabla pública y otro material oficial discrepan en el total de viviendas; tampoco permite separar con seguridad las superficies de una tipología no disponible. Esos campos permanecen nulos. |
| Miraflores Villas — Miraflores de la Sierra | Viviendas en venta de 3–4 dormitorios sobre parcelas de 1.000 m². | Micrositio público. Se excluye el bloque de personalización para no publicar la piscina opcional como prestación confirmada. Precio, total, superficie construida y subtipo unifamiliar permanecen nulos. |

Identidad y acceso:

- `docefresnos.es` vincula su información legal y comercial con Altter Real
  Estate S.L.; el dominio se trata como micrositio, sin atribuir una promotora
  no declarada en los bloques publicados;
- el aviso legal de `galivivienda.com` identifica a Galivivienda S. Coop.,
  CIF F87417226 y registro 2727-SMT;
- la información legal de `mirafloresvillas.es` identifica al titular del
  dominio, pero no se transforma automáticamente en nombre de promotora;
- los tres `robots.txt` respondieron HTTP 200 y declararon sitemap. En
  Miraflores Villas se respeta además la señal `ai-train=no`: el crawler solo
  realiza extracción determinista de referencia.

Los cuatro smokes live aislados terminaron con una promoción y cero fallos por
fuente, sin Playwright ni Nominatim. Las fixtures reducidas reproducen el
formato observado y prueban que formularios, personalización y bloques
contradictorios quedan fuera del texto extraído.

La ejecución de GitHub `30131105415` reveló una diferencia de red no visible
en local: la portada de Miraflores Villas devolvió HTTP 403, aunque
`robots.txt`, el sitemap directo de páginas y
`/wp-json/wp/v2/pages/32` siguieron públicos. La fuente usa desde entonces ese
endpoint REST oficial con el adaptador cerrado `wordpressPageJson`; únicamente
se transforma `content.rendered`, y `link` solo se acepta como canónica si es
HTTPS y conserva el mismo host. El smoke live posterior recuperó una promoción
sin Playwright y el sitemap directo procesó dos páginas sin fallos.

## Candidatos clasificados

Las siguientes decisiones se codifican en `reviewRules` para que se apliquen
también a la cola ya existente:

- `stale`: Los Pinarejos de Nismar figura como proyecto realizado de 2019;
  Residencial Amatista es una ficha histórica; ZOE 19 y ZOE 48 constan
  vendidos; Prado Real declara «promoción vendida»;
- `rejected`: la noticia de Madridiario remite a producto plurifamiliar;
  Parque Las Colinas es una página genérica de barrio y reventa; Impulsa Los
  Molinos está en Getafe; Jardines de San Lorenzo es un edificio de
  bajos, plantas y dúplex; las páginas territoriales `obra-nueva-en-*` de
  STANCE no representan promociones;
- `monitoring`: Idarq Bustarviejo describe una casa piloto, sin inventario ni
  comercialización demostrada.

Habitania y Engel & Völkers se excluyen como agregador y portal generalista;
Uría se excluye porque sus boletines jurídicos producen ruido documental y no
son fuente comercial. La exclusión afecta al buscador privado, no sustituye la
revisión de una futura fuente primaria.

## Vigilancia añadida

Se añaden los sitemaps oficiales de Doce Fresnos, Galivivienda y Miraflores
Villas. Galivivienda incorpora además un canal `htmlLinks` estrictamente
limitado a las dos fichas revisadas y a dos páginas de detalle como máximo. El
smoke live obtuvo 1, 61 y 2 entradas en los sitemaps, y el seguimiento interno
recuperó Montelago y produjo un candidato privado; ninguna señal se convierte
automáticamente en promoción pública.
