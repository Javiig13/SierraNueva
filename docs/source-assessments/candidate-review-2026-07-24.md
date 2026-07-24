# Revisión de candidatos privados del radar

Fecha de evaluación: **24 de julio de 2026**.

## Decisión

Se revisaron los siete candidatos pendientes de la instantánea live aislada.
Cinco se descartan y dos permanecen en seguimiento. Ninguno justifica ampliar
el contrato público ni modificar el alcance unifamiliar de SierraNueva.

La caché privada de Actions mostraba seis pendientes en la ejecución
`30112292496`, mientras la instantánea local previa conservaba siete. La
diferencia procede de estados privados observados en momentos distintos. Las
reglas versionadas que se describen aquí hacen converger ambos estados en la
siguiente ejecución.

## Candidatos descartados

### Orbia — Collado Villalba

- La ficha sectorial de SIMA atribuía el producto de forma incompleta.
- La fuente canónica es la
  [ficha oficial de AEDAS Homes](https://www.aedashomes.com/pisos-en-venta-en-collado-villalba-obra-nueva-orbia).
- AEDAS publica 96 viviendas de uno, dos y tres dormitorios, precios y
  disponibilidad, y `robots.txt` no bloquea la ruta limpia de la promoción.
- Es un edificio plurifamiliar de pisos. SierraNueva cubre obra nueva
  **unifamiliar**, por lo que no se incorpora a `sources.live.json`.

La URL de SIMA queda `rejected` mediante una regla exacta. No se rechaza por
falta de calidad de la fuente oficial, sino por no pertenecer al producto.

### Nevia — El Escorial

- La
  [ficha oficial de Aurora Homes](https://aurora-homes.es/promociones/pisos-obra-nueva-el-escorial/)
  describe 88 viviendas VPPL de dos, tres y cuatro dormitorios en un edificio
  de tres plantas.
- La misma ficha indica de forma expresa que la promoción está adjudicada al
  100 % y que no quedan viviendas disponibles.
- Es producto plurifamiliar y, además, no representa una oportunidad comercial
  activa.
- El aviso legal de Aurora limita los enlaces externos a la página principal y
  prohíbe reutilizar una parte sustancial de sus contenidos. No se añade un
  crawler contra el dominio.

La URL sectorial queda `rejected`. Al salir del embudo pendiente, el dominio
referido por SIMA deja de contarse como hueco de cobertura.

### Páginas territoriales NUVARE

Se revisaron:

- `/obra-nueva-guadalix-de-la-sierra/`;
- `/obra-nueva-miraflores-de-la-sierra/`;
- `/obra-nueva-soto-del-real/`.

Las tres reutilizan la misma distribución de vivienda y enlazan el dossier
`Dossier_Navalafuente_Web.pdf`. La página de Miraflores enlaza además la
personalización de Cumbres de Navalafuente y describe una promoción **cerca**
de Miraflores, no una promoción independiente en ese municipio. No publican
una identidad comercial diferenciada que permita deduplicar con seguridad.

Las tres quedan `rejected` como páginas territoriales SEO derivadas de la
promoción Cumbres de Navalafuente, que ya está integrada mediante su ficha
canónica.

## Candidatos en seguimiento

### Estudio de detalle — Collado Mediano

El anuncio municipal de la calle de la Fuente, 2 demuestra actividad
urbanística, pero no identifica promotora, inventario ni comercialización.
Las búsquedas independientes solo localizaron planeamiento municipal. Se
mantiene `monitoring`; no es todavía una promoción.

### Ocho parcelas de Los Pinarejos — Miraflores de la Sierra

El Portal del Suelo y la Plataforma de Contratación describen la segunda
licitación por lotes de ocho parcelas residenciales municipales, expediente
`1049/2026`. La licitación estaba en evaluación y no identifica una futura
promoción comercial ni un adjudicatario que publique viviendas.

Toda referencia de detalle procedente de este canal se clasifica
`monitoring`: es una señal temprana válida, pero nunca una promoción
publicable por sí sola.

## Verificación

La repetición live `op-20260724T173446775Z` terminó con:

- 50/50 fuentes sanas y cero fallos;
- 29/29 municipios con vigilancia sana y 28 con canal directo;
- cero candidatos `new`;
- dos candidatos `monitoring`;
- 17 `rejected`, cinco `verifiedSource` y dos `stale`;
- cero dominios externos pendientes de monitorización.

El estado permaneció en `.runtime/radar-live-final`; no se modificó ni publicó
`data/state` o `data/public`.
