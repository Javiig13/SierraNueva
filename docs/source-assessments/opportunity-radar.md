# Evaluación de fuentes del radar de oportunidades

Fecha de revisión técnica: **23 de julio de 2026**.

El radar no convierte anuncios administrativos en promociones. Sus resultados
son candidatos privados que necesitan revisión y una fuente comercial oficial
antes de poder entrar en `data/public`.

## BOCM

- Fuente: `https://www.bocm.es/ultimo-boletin.xml`.
- Identidad: Sede Oficial del Boletín de la Comunidad de Madrid.
- Formato comprobado: RSS 2.0 con las órdenes del boletín del día.
- Acceso comprobado: HTTP 200, 68 entradas el día de la revisión.
- Resultado del smoke: cero candidatos tras el filtro endurecido.
- Límite: el endpoint solo representa el último boletín; no proporciona por sí
  solo backfill histórico.

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

## Mitigaciones comunes

- Perfil offline predeterminado con fixtures sintéticas reducidas.
- Perfil live separado y explícito; HTTPS y hosts permitidos por fuente.
- Resolución DNS segura, User-Agent identificable, timeout y límites de tamaño.
- Coincidencia obligatoria de municipio, señal administrativa y contexto
  inmobiliario; términos de ruido configurables.
- Fragmentos saneados, sin HTML completo, documentos ni datos personales.
- Persistencia atómica bajo `data/state`, con dos backups e inclusión prohibida
  en el artefacto web.
