# Evaluación de fuente: EXXACON — Living Natura

Fecha de revisión: 2026-07-23.

## Decisión

Fuente aceptada para un primer perfil live manual y limitado. No forma parte de
la baseline offline ni se ejecuta automáticamente.

La página corporativa identifica la promoción como obra nueva unifamiliar en
Galapagar y enlaza su micrositio y documentación comercial. El titular de la web
se identifica como EXXACON, Sociedad Española para la Ingeniería, Construcción y
Administración de Proyectos, S.L. (CIF B-92192632).

## URLs revisadas

- Promoción: <https://www.exxacon.es/promocion/living-natura/>
- Web corporativa: <https://www.exxacon.es/>
- Condiciones y privacidad:
  <https://www.exxacon.es/politica-de-privacidad/>
- Robots: <https://www.exxacon.es/robots.txt>
- Micrositio relacionado: <https://livingnaturaexxacon.es/>

## Controles

- Encaje: promoción oficial de chalets pareados y villas independientes en
  Galapagar, municipio incluido en SierraNueva.
- Identidad: dominio corporativo de la promotora, datos societarios publicados,
  email del mismo dominio y enlace al micrositio.
- Acceso: HTML público sin autenticación, CAPTCHA ni área privada.
- Condiciones: acceso gratuito; contenido meramente informativo y protegido por
  propiedad intelectual. SierraNueva solo conserva hechos y evidencias breves;
  no descarga imágenes ni republica HTML.
- `robots.txt`: permite la página de promoción y excluye `/wp-admin/`.
- Técnica: HTML estático suficiente; Playwright no es necesario.
- Frecuencia: una URL, una página, espera mínima de 5 segundos, un reintento,
  sin sitemap ni seguimiento de enlaces.
- Identificación: User-Agent propio, descriptivo y sin suplantación. No se
  inventa un contacto; antes de cualquier automatización debe añadirse un canal
  duradero y volver a revisar las condiciones.
- Datos personales: no se envía el formulario ni se conserva teléfono, email o
  información de contacto.

## Verificación operacional

El 2026-07-23 se ejecutaron un dry run y una ejecución live con salida y estado
aislados bajo `tmp/`. En ambos casos `robots.txt` y la página respondieron con
HTTP 200, la única fuente terminó sin fallos y `validate-data` aceptó una
promoción.

La revisión manual del contrato aislado confirmó: municipio Galapagar, precio
desde 925.000 €, cuatro dormitorios para la vivienda disponible, superficie
construida de 256 a 365 m², 28 viviendas totales, una disponible, tipología
pareada y estado de últimas unidades. La superficie de 20.742 m² del conjunto
no se publicó como parcela privada. Los datos live no se copiaron a
`data/public` ni a `data/state`.

## Riesgos y mitigaciones

La página incluye referencias territoriales generales a Guadarrama y datos de
otras zonas en elementos comunes. Para evitar una asignación falsa al municipio
Guadarrama, el municipio se obtiene del bloque de dirección comercial y se
normaliza contra el catálogo oficial. Una fixture sintética reducida reproduce
este caso sin copiar el HTML completo de la fuente.

El precio, la disponibilidad y el estado son información comercial mutable y no
contractual. Cada ejecución debe registrar la fecha de captura y mantener el
último dataset válido si la fuente falla. Antes de convertir este perfil manual
en una ejecución periódica se debe volver a revisar la página y sus condiciones.
