# Evaluación consolidada de fuentes live ampliadas

Fecha de revisión: **23 de julio de 2026**.

## Decisión y alcance

Se aceptan siete fuentes adicionales para el perfil live manual y se mantiene
EXXACON — Living Natura. Cada fuente se limita a una única ficha pública, una
página, profundidad cero, espera mínima de cinco segundos, un reintento y sin
Playwright, sitemap, seguimiento de enlaces ni geocodificación live.

No se descargan imágenes, no se envían formularios y no se republica HTML. El
dataset conserva hechos normalizados, fragmentos breves de evidencia y el
enlace canónico. El perfil no forma parte de la baseline offline.

## Fuentes aceptadas

### Apremya Homes — Residencial Puerta de Villalba

- Promoción: <https://apremyahomes.es/promociones/residencial-puerta-de-villalba/>
- Aviso legal: <https://apremyahomes.es/aviso-legal/>
- Robots: <https://apremyahomes.es/robots.txt>
- Identidad: APREMYA HOMES S.L., CIF B73956427, domicilio y contacto
  publicados.
- Encaje: diez chalets pareados en Collado Villalba; la página indica obra en
  ejecución y precio desde 695.500 € más IVA.
- Acceso: HTML público; `robots.txt` solo excluye administración de WordPress.

### Altter — Residencial Navata Nature

- Promoción: <https://www.altter.es/residencial-navata-nature/>
- Aviso legal: <https://www.altter.es/aviso-legal/>
- Robots: <https://www.altter.es/robots.txt>
- Identidad: ALTTER REAL ESTATE, S.L., CIF B86470291, domicilio y contacto
  publicados.
- Encaje: comercializador de cuatro chalets pareados en La Navata, Galapagar;
  licencia solicitada, entrega anunciada para 2027 y precios publicados.
- Acceso: HTML público; `robots.txt` permite el contenido y declara
  `Crawl-delay: 3`, superado por la espera configurada de cinco segundos.

### Urbanizadora Colmenar — Los Pinarejos

- Promoción: <https://www.lospinarejos.com/>
- Aviso legal: <https://www.lospinarejos.com/aviso-legal-privacidad.html>
- Robots: <https://www.lospinarejos.com/robots.txt>
- Identidad: Urbanizadora Colmenar S.A., NIF A-28245009, inscripción y
  domicilio publicados.
- Encaje: fase de ocho chalets pareados en Miraflores de la Sierra; la ficha
  publica superficies, parcelas y precios de la Manzana P-6.
- Acceso: HTML público; el dominio responde 404 para `robots.txt`, por lo que
  no existen reglas publicadas que interpretar. El rastreo sigue siendo de una
  sola página y no descarga los PDF enlazados.

### Trinosa — Etria

- Promoción: <https://trinosa.com/etria/>
- Aviso legal: <https://trinosa.com/aviso-legal/>
- Robots: <https://trinosa.com/robots.txt>
- Identidad: TRINOSA INTEGRACIÓN URBANA, S.L., NIF B88108550, domicilio y
  contacto publicados.
- Encaje: trece viviendas unifamiliares en San Lorenzo de El Escorial, en
  precomercialización y con precio desde 590.000 €.
- Acceso: HTML público; solo se consulta la ficha, nunca el área de cliente.

### Kronos Homes — Onix

- Promoción:
  <https://www.kronoshomes.com/es/proyectos-obra-nueva/madrid/onix/>
- Términos:
  <https://www.kronoshomes.com/es/terminos-y-condiciones/>
- Robots: <https://www.kronoshomes.com/robots.txt>
- Identidad: KRONOS INVESTMENT MANAGEMENT SPAIN, S.L., CIF B86990173,
  inscripción, domicilio y contacto publicados.
- Encaje: 29 chalets pareados de cuatro y cinco dormitorios en Torrelodones,
  con últimas viviendas publicadas.
- Acceso: ficha HTML pública y JSON-LD; el área de clientes y las rutas
  excluidas por `robots.txt` quedan fuera.

### La Quinta de Manzanares — Manzanares el Real

- Promoción:
  <https://www.laquintademanzanares.com/manzanares.html>
- Aviso legal:
  <https://www.laquintademanzanares.com/aviso-legal.html>
- Robots: <https://www.laquintademanzanares.com/robots.txt>
- Identidad: LA QUINTA DE MANZANARES S.L., CIF B05376090, inscripción,
  domicilio y contacto publicados.
- Encaje: cuatro viviendas independientes, parcelas de 500 m², 160 m²
  construidos, cuatro dormitorios y tres baños en Manzanares el Real.
- Acceso: HTML público; no se usa el formulario ni las rutas de aplicaciones
  excluidas por `robots.txt`.

### Gilmar — Quercus Dorf Guadarrama

- Promoción:
  <https://www.gilmar.es/inmueble/quercus-dorf-guadarrama/>
- Aviso legal: <https://www.gilmar.es/aviso-legal/>
- Robots: <https://www.gilmar.es/robots.txt>
- Identidad: CONSULTING INMOBILIARIO GILMAR S.A., CIF A28894194, inscripción,
  domicilio y contacto publicados.
- Encaje: comercialización de Quercus Dorf en Guadarrama; diez chalets y una
  última unidad publicada. La ficha aporta rangos de superficie y parcela.
- Acceso: ficha pública permitida por `robots.txt`. Se excluyen login, agentes,
  búsquedas y rutas administrativas. No se reutilizan imágenes ni textos.

## Condiciones comunes y mitigaciones

Los avisos revisados protegen los contenidos y, en varios casos, limitan su
reproducción. SierraNueva no crea una copia del sitio: extrae hechos
comerciales puntuales, conserva evidencia breve para trazabilidad y enlaza a la
fuente. El perfil es local, no comercial y manual; una automatización futura
exige revisar de nuevo estas condiciones y añadir una identidad de contacto
duradera al User-Agent.

Las páginas contienen navegación, promociones relacionadas y calculadoras. Para
reducir falsos positivos, cada URL fija el municipio revisado y limita la
extracción a un selector de contenido. Si el selector desaparece, la fuente
falla de forma cerrada y el último dataset válido no se sustituye.

## Verificación operacional

El 23 de julio de 2026 se ejecutaron dry runs individuales y una ejecución
conjunta con salida y estado aislados bajo `tmp/live-expansion`. Las ocho
fuentes respondieron, las ocho promociones finales fueron válidas y
`validate-data` terminó correctamente. Los datos no se copiaron a
`data/public` ni a `data/state`.

La cobertura y todos los descartes de la búsqueda municipal se documentan en
`docs/source-coverage.md`.
