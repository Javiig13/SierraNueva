# Evaluación de fuentes del radar de oportunidades

Fecha de revisión técnica: **23 de julio de 2026**.

El radar no convierte anuncios administrativos en promociones. Sus resultados
son candidatos privados que necesitan revisión y una fuente comercial oficial
antes de poder entrar en `data/public`.

## BOCM

- Fuente de calendario:
  `https://www.bocm.es/search-day-month?field_date[date]=DD/MM/AAAA`.
- Fuente de datos: el enlace `CM_Boletin_BOCM/.../BOCM-*.xml` que devuelve
  la página oficial de cada fecha.
- Identidad: Sede Oficial del Boletín de la Comunidad de Madrid.
- Formato comprobado: HTML de calendario seguido del sumario XML estructurado.
- Acceso comprobado: HTTP 200, 68 disposiciones el 23 de julio de 2026; un
  recorrido del 17 al 23 de julio resolvió las seis ediciones existentes y
  omitió correctamente el día sin boletín.
- Resultado del smoke: cero candidatos en esos intervalos tras el filtro.
- Backfill: la búsqueda oficial declara cobertura desde el 12 de febrero de
  2010. La CLI admite intervalos inclusivos de hasta 367 días para que un
  histórico largo se ejecute por lotes auditables.
- Mitigación: solo se conservan título, organismo, sección, identificador,
  fecha y enlace HTML oficial; no se descargan ni republican los PDF.

## BOE OpenData

- Fuente:
  `https://www.boe.es/datosabiertos/api/boe/sumario/{fecha}`.
- Identidad: Agencia Estatal Boletín Oficial del Estado.
- Formato comprobado: JSON oficial del sumario diario.
- Acceso comprobado: HTTP 200, 184 entradas el 23 de julio de 2026.
- Resultado del smoke: cero candidatos después de eliminar una coincidencia
  nominal ajena al producto.
- Condición: la reutilización queda sujeta a las condiciones publicadas por la
  AEBOE; se conservan únicamente metadatos breves y enlaces oficiales.

## Plataforma de Contratación del Sector Público

- Fuente:
  `https://contrataciondelsectorpublico.gob.es/sindicacion/sindicacion_643/licitacionesPerfilesContratanteCompleto3_AAAAMM.zip`.
- Identidad: conjunto de datos abiertos de licitaciones publicadas en perfiles
  alojados en la Plataforma.
- Formato comprobado: ZIP mensual con documentos Atom/XML.
- Acceso comprobado: HTTP 200 y 16.815 entradas procesadas para julio de 2026.
- El ZIP supera 64 MiB: se descarga a un temporal con límite de 512 MiB, se lee
  entrada a entrada y se elimina siempre al terminar.
- Resultado del smoke: cinco coincidencias léxicas iniciales, todas ruido
  administrativo; cero candidatos tras exigir contexto inmobiliario y aplicar
  exclusiones.
- Límite: una licitación puede aparecer varias veces por sus actualizaciones;
  la identidad externa evita candidatos duplicados dentro del estado.
- Incidencia posterior: el 23 de julio de 2026 una repetición del smoke recibió
  HTTP 200 con una página HTML de denegación del WAF en lugar del ZIP. La fuente
  quedó como fallo parcial y no escribió estado. El lector rechaza ahora de
  forma explícita tipos de contenido que no sean ZIP; no se intenta evadir el
  WAF y la fuente debe revalidarse antes de una ejecución operativa.

## Portal del Suelo 4.0

- Fuente:
  `https://www.comunidad.madrid/inversion-empresa/portal-suelo-40`.
- Identidad: portal informativo oficial de la Comunidad de Madrid.
- Formato comprobado: HTML estático acotado a tarjetas y listas de
  licitaciones.
- Acceso comprobado: HTTP 200, 26 bloques analizados.
- Resultado del smoke: un candidato de suelo residencial en Miraflores de la
  Sierra.
- Límite jurídico: el propio portal declara que su contenido es informativo,
  no sustituye al perfil del contratante y no produce efectos frente a
  terceros. El radar conserva el enlace al expediente oficial para revisión.

## Tablones municipales eAdmin — primera cohorte

Formato compartido comprobado en cinco sedes oficiales:

| Municipio | Endpoint | Entradas el 2026-07-23 | `robots.txt` |
|---|---|---:|---|
| Galapagar | `sede.galapagar.es/eAdmin/Tablon.do?action=verAnuncios` | 255 | permite el tablón; excluye documentos |
| Alpedrete | `carpeta.alpedrete.es/eAdmin/Tablon.do?action=verAnuncios&tipoTablon=1` | 18 | no publicado (404) |
| Los Molinos | `sede.ayuntamiento-losmolinos.es/eAdmin/Tablon.do?action=verAnuncios&tipoTablon=1` | 19 | no publicado (404) |
| Moralzarzal | `carpeta.moralzarzal.es/eAdmin/Tablon.do?action=inicioTablon` | 17 | no publicado (404) |
| San Lorenzo de El Escorial | `sede.aytosanlorenzo.es/eAdmin/Tablon.do?action=verAnuncios` | 26 | permite el tablón; excluye documentos |

- Identidad: sedes electrónicas rotuladas con el ayuntamiento correspondiente;
  las URLs están además citadas por publicaciones oficiales BOCM/BOE.
- Formato: HTML estático común `eAdmin`. El adaptador selecciona únicamente
  filas con enlace de detalle `verAnuncio`, ignora enlaces `javascript:` y
  extrae título y fecha de exposición.
- Cada fuente fija un municipio ya validado contra el catálogo. Esto evita
  depender de pies de página repetidos y no inventa la ubicación.
- Resultado live aislado: 335 entradas en total, cero fallos y cero candidatos
  el 23 de julio de 2026. Cero candidatos es un resultado válido, no evidencia
  de ausencia histórica de oportunidades.
- No se descargan adjuntos ni certificados y solo se conserva metadata breve.
- Los tablones `sedelectronica.es/board` evaluados para Miraflores de la Sierra,
  Manzanares el Real, Becerril de la Sierra y El Boalo se descartaron porque
  su `robots.txt` prohíbe expresamente `/board`.

## Mitigaciones comunes

- Perfil offline predeterminado con fixtures sintéticas reducidas.
- Perfil live separado y explícito; HTTPS y hosts permitidos por fuente.
- Resolución DNS segura, User-Agent identificable, timeout y límites de tamaño.
- Métricas por fuente en la salida CLI: entradas leídas, candidatos y fallo.
- Coincidencia obligatoria de municipio, señal administrativa y contexto
  inmobiliario; términos de ruido configurables.
- Fragmentos saneados, sin HTML completo, documentos ni datos personales.
- Persistencia atómica bajo `data/state`, con dos backups e inclusión prohibida
  en el artefacto web.
