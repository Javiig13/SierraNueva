# Ampliación live de julio de 2026

Fecha de revisión: **23 de julio de 2026**.

## Decisión y alcance

Se incorporan seis promociones públicas adicionales mediante seis
configuraciones acotadas a una ficha: dos de Antaro, una de Grupo Index y tres
de Vesari. Todas usan HTML estático, `robots.txt`, profundidad cero, una sola
página, pausa mínima de cinco segundos y ningún formulario, imagen, PDF,
Playwright o geocodificador live.

El perfil pasa de 8 a 14 fuentes/promociones y de 7 a 11 municipios con alguna
promoción verificada. La baseline predeterminada continúa siendo offline.

## Antaro — Prado de Noria y Los Trigales

- Fichas:
  <https://antaro.es/promociones/prado-de-noria/> y
  <https://antaro.es/promociones/los-trigales/>.
- Aviso legal: <https://antaro.es/aviso-legal/>.
- Robots: <https://antaro.es/robots.txt>.
- Identidad: Antaro 2020 S.L., NIF B02863751, domicilio, teléfono y correo
  publicados.
- Encaje: Prado de Noria anuncia 35 pareadas en Guadalix de la Sierra desde
  479.500 €; Los Trigales anuncia 25 pareadas y últimas viviendas en Becerril
  de la Sierra desde 395.013 €.
- Acceso: ambas fichas son HTML público permitido. Se excluyen administración,
  legales, cookies y cualquier descarga enlazada.

## Grupo Index — El Mirador de Sierra Bonita

- Ficha:
  <https://grupoindexmadrid.com/el-mirador-de-sierra-bonita-el-boalo>.
- Aviso legal: <https://grupoindexmadrid.com/aviso-legal>.
- Robots: <https://grupoindexmadrid.com/robots.txt>.
- Identidad: Proyectos y Soluciones Index S.L., NIF B85773356, domicilio,
  teléfono y correo publicados.
- Encaje: 80 viviendas unifamiliares adosadas, pareadas e independientes en El
  Boalo, de 134 a 180 m² construidos y parcelas de 250 a 974 m²; la ficha
  figura en venta.
- Acceso: solo se procesa el `article` de la ficha HTML. El área privada,
  documentos, imágenes y formulario quedan fuera.

## Vesari — El Tomillar, Cuarteto y Luar

- Fichas:
  <https://www.vesari.info/guadalix>,
  <https://www.vesari.info/cuarteto> y
  <https://www.vesari.info/robledo>.
- Listado de cooperativas abiertas: <https://www.vesari.info/abiertas>.
- Robots: <https://www.vesari.info/robots.txt>.
- Identidad publicada: Vesari S.L., domicilio, teléfonos y correo. El sitio no
  muestra en sus páginas públicas revisadas un NIF ni un aviso legal separado;
  esta limitación queda registrada y exige reevaluación si cambia el uso.
- Encaje: El Tomillar publica 18 pareadas en Guadalix de la Sierra; Cuarteto,
  4 adosadas y última vivienda en San Lorenzo de El Escorial; Luar, 7
  unifamiliares en Robledo de Chavela.
- Acceso: `robots.txt` permite las fichas. El proceso secuencial conserva diez
  segundos entre peticiones al mismo host; una prueba con cinco segundos
  recibió HTTP 429 en la tercera ficha, por lo que no debe reducirse esa pausa.

La ficha de Luar contiene al final una frase residual que menciona
Navalagamella. Se conserva Robledo de Chavela como municipio revisado porque
coinciden el título, la URL, la descripción principal y el listado oficial de
cooperativas abiertas. El municipio queda fijado en configuración y la
discrepancia no se oculta.

## Correcciones de extracción derivadas

El HTML de Wix separa visualmente el contador de galería `1/17` y el texto
`18 viviendas`, pero `TextContent` podía concatenarlos como `1718`. El
extractor ahora introduce límites entre nodos de texto y omite
`script/style/template/noscript`; una prueba reproduce el caso.

También se añadieron formatos para rangos como “desde 134 m² hasta los 180 m²
construidos”, el indicador singular “última vivienda” y la negación “no
adosadas”. Los hechos desconocidos continúan nulos.

## Verificación operacional

Cada formato tiene una fixture reducida y una prueba offline. Los dry runs
individuales de Antaro y Grupo Index terminaron con una promoción válida por
ficha. Las tres fichas de Vesari terminaron correctamente respetando la pausa y
produjeron 18, 4 y 7 viviendas, sin el falso total concatenado.

La comprobación conjunta terminó con 14/14 fuentes, 14 promociones, cero
fallos, cero inválidas y 14/14 elementos en GeoJSON. `validate-data` y la
baseline offline completa (91/91 pruebas) también terminaron correctamente. La
publicación y su verificación quedan registradas en `docs/HANDOFF.md`.
