# SIMA como canal privado de descubrimiento

Fecha de evaluación: **24 de julio de 2026**.

## Decisión

SIMA se admite únicamente como directorio sectorial para generar candidatos
privados. No es una fuente canónica, sus fichas no se publican y una
coincidencia nunca demuestra por sí sola vigencia, disponibilidad ni
condiciones comerciales.

La decisión se apoya en:

- `robots.txt` permite las páginas públicas y excluye `/wp-admin/`,
  `/wp-includes/` y `/myarea/`; el adaptador no accede a esas rutas;
- existe un sitemap específico de oferta residencial:
  `https://simaexpo.com/oferta-sitemap.xml`;
- el [aviso legal de SIMA](https://simaexpo.com/aviso-legal/) atribuye el
  contenido de terceros a sus anunciantes y describe a Planner Exhibitions
  como intermediario; la propia ficha advierte que la información puede no
  estar actualizada;
- no se descargan imágenes, scripts, formularios, áreas privadas ni HTML
  completo para republicarlo.

## Alcance técnico

El canal `sima-residential-directory` filtra el sitemap por los 29 municipios
del producto y sigue como máximo 50 fichas. Dos canales `htmlLinks` consultan
las vistas de obra nueva de Collado Villalba y El Escorial y siguen como
máximo cinco fichas cada uno. Son necesarios porque `Orbia` y `Nevia` no
incluyen la localidad en el slug y, por tanto, no pueden seleccionarse de forma
segura desde el sitemap general.

Cada ficha exige al menos uno de estos bloques:

- `.migas-header`;
- `.oferta-datas.descripcion`;
- `.single-offer-main-exhibitor`.

Los enlaces externos se aceptan solo desde selectores explícitos, se reducen a
HTTPS, se eliminan query y fragmento, se deduplican y se guardan únicamente en
`data/state`. El lector no los visita. Los hosts ajenos a SIMA tampoco se
incorporan automáticamente a `sources.live.json`.

## Verificación

El smoke live aislado terminó con los tres canales sanos:

- el sitemap leyó tres fichas relevantes ya clasificadas mediante reglas
  exactas: Quercus Dorf Guadarrama, Residencial Los Molinos II y Suite Los
  Molinos;
- el índice de Collado Villalba encontró `Orbia`; la revisión posterior
  resolvió que su fuente canónica es AEDAS Homes, compañía de Neinor Group;
- el índice de El Escorial encontró `Nevia`, de Aurora Homes;
- ambos índices siguieron una única ficha y produjeron un candidato privado
  `new`;
- Nevia referenció `https://aurora-homes.es/promociones/`, por lo que la
  cobertura registró un dominio descubierto y todavía no monitorizado;
- Orbia no expuso una URL externa oficial en los bloques admitidos, así que el
  dominio debe resolverse con evidencia independiente.

No se promovió ninguna ficha ni se modificó `data/public`. La fixture y las
pruebas automáticas cubren tanto sitemap como índice HTML, límites, allowlist,
selectores, enlaces externos y eliminación de parámetros de seguimiento.

La ejecución integrada posterior comprobó 50/50 fuentes live sin degradaciones,
incluidos los tres canales SIMA. El embudo privado total quedó en tres
candidatos nuevos y cuatro en seguimiento; dos de los nuevos son Orbia y
Nevia. Esta cifra no es una métrica de exhaustividad ni convierte el catálogo
sectorial en evidencia independiente.

## Revisión posterior

La evaluación
[`candidate-review-2026-07-24.md`](candidate-review-2026-07-24.md) resolvió
ambos candidatos:

- Orbia es una promoción oficial de AEDAS Homes con 96 pisos de uno a tres
  dormitorios;
- Nevia publica 88 pisos VPPL y declara que está adjudicada al 100 %.

Ambas son plurifamiliares y quedan fuera del alcance unifamiliar del producto.
Nevia tampoco conserva disponibilidad. Las dos URLs de SIMA se clasifican
`rejected`; no se añadió ningún dominio al registro comercial.
