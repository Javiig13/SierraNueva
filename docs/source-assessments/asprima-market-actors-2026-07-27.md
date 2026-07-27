# ASPRIMA y SIMA como universo de actores de mercado

Fecha de revisión: **27 de julio de 2026**.

## Identidad y alcance

- Fuente: <https://asprima.es/listado-de-socios/>.
- Organización responsable: Asociación de Promotores Inmobiliarios de Madrid.
- Endpoint usado por la propia página pública:
  `https://asprima.es/api/v1/web/search/organization/`.
- Resultado observado: 194 organizaciones y 158 registros con una web
  declarada aprovechable.
- Segunda fuente: <https://simaexpo.com/empresas/>.
- Resultado observado en SIMA: 378 fichas de empresa enlazadas.

Ambos directorios son fuentes sectoriales para descubrir organizaciones, no
fuentes canónicas de promociones. Incluyen perfiles distintos de promotoras
—por ejemplo, servicios profesionales, expositores internacionales y
proveedores— y no acreditan por sí solos actividad residencial unifamiliar
vigente en la Comunidad de Madrid.

## Integración

`scripts/import-market-actors.ps1` consulta el endpoint público de ASPRIMA y el
directorio público de SIMA, conserva solo nombre, web oficial normalizada y
enlaces de evidencia, y cruza dominios con los canales comerciales habilitados
en `config/discovery-sources.live.json`. La salida versionada está en
`config/market-actors.json`.

La instantánea resultante contiene 567 actores:

- 194 entradas procedentes de ASPRIMA;
- 378 fichas procedentes de SIMA, con 19 coincidencias normalizadas respecto a
  ASPRIMA;
- 14 actores adicionales derivados de canales comerciales live sin
  coincidencia en esos directorios;
- 195 con web declarada;
- 16 monitorizados mediante al menos una fuente de SierraNueva;
- 551 pendientes de clasificación y revisión.

No se persisten correos, teléfonos, contactos, direcciones, descripciones,
documentos ni ningún otro campo devuelto por la API. El catálogo no forma parte
de `data/public` ni de la salida web.

## Revisión necesaria

Antes de convertir un candidato en fuente live hay que verificar identidad,
oferta unifamiliar vigente en Madrid, condiciones/robots, acceso con
`HttpClient`, selectores, límites, host permitido y una fixture con prueba. Un
estado `candidate` nunca equivale a promotora confirmada ni permite publicar
una promoción.
