# Conversión comercial y omisiones — 5 noviembre de 2026

## Altas y descartes

Se admiten las cuatro fases de Brosh documentadas en la evaluación específica
y `https://www.impulsaproyectos.com/proyectos/impulsa-montecillos/`.
Montecillos está enlazada desde el catálogo oficial y declara nueve
unifamiliares en Rivas: ocho pareados y uno independiente, con licencia
concedida. El crawl aislado `20261105T183349083Z` produjo una ficha y cero
fallos; cada alta tiene fixture reducida y aserciones específicas.

Impulsa ya estaba evaluada para Los Molinos I/II. Se releyeron condiciones de
uso y robots: permite proyectos y prohíbe `/wp-admin/`. Se mantienen HTML
estático, User-Agent identificable y cinco segundos de espera. No se descargan
imágenes ni planos ni se republica texto comercial completo.

La reserva de 10.000 € de Montecillos no es el precio: se excluye su bloque.
También se omiten piscina opcional y superficies contradictorias (atributo
matemático 220 frente a texto visible 210 m²). No se suma un dormitorio por
planta para inferir un total. Esos campos permanecen nulos y tienen regresión.

Cielo de Alcalá (GEMG) responde 200 y describe 27 viviendas con locales en
planta baja: no es unifamiliar. Se reconcilia el descarte web con el del sitemap.
Cooperativa Brunete de Impulsa responde 200 y describe 89 VPPB con sótano
común y trasteros: tampoco es una promoción de chalets. Rechazo por URL.
La otra ficha sectorial de Brunete permanece pendiente de tipología.

## Control de omisión y medición

Montecillos no estaba en el catálogo anterior ni en los pendientes exportados.
No demuestra que jamás se hubiera descubierto, ni permite estimar recall.
Su slug no nombra Rivas. El sitemap Impulsa sigue ahora hasta doce artículos
de proyectos, sin recursión y con demora antes de cada detalle. El smoke real
procesó once fichas y cinco candidatos, verificando Montecillos. Los Molinos
I/II se asignan a Getafe mediante la localidad ya revisada de su URL,
no al municipio homónimo; se conserva identidad privada y primera observación.

`commercial-coverage` mide configuración, no salud ni cuota de mercado:
573 actores, 30 con canal, 171 con web pendientes y 372 sin web resuelta;
29/179 municipios tienen extracción configurada. El informe queda bajo
`--state`. Se añaden cuatro dominios ya integrados que faltaban en el
inventario y se enlazan Gilmar/Cambrija, sin fingir una nueva revisión del
censo ASPRIMA/SIMA. El script offline conserva su fecha de evidencia.
El triaje separa comerciales, administrativos, otros y duplicados sin
modificar decisiones ni considerar «comercial» equivalente a «apto».

## Diagnóstico remoto pendiente

Los pendientes web del exportado `42049846477` solo abarcan 21 municipios,
de Ajalvir a Brunete. Ese informe omite estados terminales: es una alerta,
no prueba de su causa ni de que las demás consultas estuvieran vacías.

El lector registra consultas intentadas, con resultados brutos, con
advertencias de motores y fallidas, agregado por municipio. Un 200 vacío
con `unresponsive_engines` deja la fuente degradada; los fallos HTTP/JSON
se aíslan por consulta. La matriz continúa y conserva los resultados válidos.
Cuatro regresiones offline cubren vacío legítimo, motores fallidos, HTTP
fallido y retención parcial. El campo se contrastó con el código oficial:
`https://github.com/searxng/searxng/blob/master/searx/webapp.py`.

No se cambia el volumen ni orden de las 716 consultas, ni se añaden motores,
proxies o evasión. Falta observar este diagnóstico en el próximo run remoto
antes de atribuir el sesgo a bloqueos, timeouts o falta de oferta. Quedan
pendientes la revisión de las 171 webs y una muestra independiente más amplia
en municipios sin extracción; no se declara inventario completo.
