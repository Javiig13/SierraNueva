# Ampliación de fuentes de Madrid — 27 de julio de 2026

## Objetivo y evidencia privada

Se recuperó localmente el artefacto cifrado de la ejecución ampliada
`30270433121` mediante `Export private opportunity triage` `30274617555`.
El SHA-256 descargado coincidió con el publicado por GitHub:
`016827dc2bc0745671807e9b5e3eb5a80e7630759bf9dd6f2c1d0c769a4e78db`.
El sobre autenticado se descifró con una clave RSA efímera que se eliminó tras
el éxito. No se versionó ni publicó el informe en claro.

El triaje contenía 833 pendientes de 367 dominios: 98 de prioridad alta, 298
media, 421 baja y 16 duplicados probables. La revisión mostró dos fuentes de
ruido principales:

- 39 resultados altos de contratación pública, muchos sin contenido
  residencial; «promoción económica» activaba el término genérico
  «promoción».
- páginas territoriales, directorios y fichas históricas seguían compitiendo
  con promociones oficiales vigentes.

El triaje deja de considerar «promoción» aislada como evidencia comercial. Un
host administrativo conserva la bonificación solo si hay señal urbanística o
residencial; en caso contrario se penaliza sin descartarlo. La prueba reproduce
un contrato hostelero de Alcorcón y confirma que baja a prioridad baja.

## Fuentes incorporadas

| Fuente | Decisión | Evidencia oficial publicada | Extracción acotada |
|---|---|---|---|
| [Insur Calíope](https://grupoinsur.com/promocion-de-viviendas/chalet-obra-nueva-alcala-henares-insur-caliope) | Automática | En comercialización, obras iniciadas, 44 chalets adosados, 3–4 dormitorios | Cabecera y descripción; se excluyen formulario, navegación y promociones relacionadas |
| [Nuevo Brunete](https://soldithomes.es/promociones/nuevo-brunete/) | Automática | Próximo lanzamiento de unifamiliares pareadas con jardín y lista de espera | Se omite el párrafo del sector que menciona 2.700 viviendas, porque no es el total de la promoción |
| [Residencial Cielo Arroyomolinos](https://gemg.es/chalets-arroyomolinos-cielo/) | Automática | Ficha 2026, 20 unifamiliares de 4 dormitorios y licencia concedida | Se omite la referencia a los 34 m² del salón para no inferir superficie construida |
| [NovaCiempozuelos](https://grupoegido.es/obra-nueva-madrid/ciempozuelos/novaciempozuelos/) | Automática | Próximo desarrollo de chalets con zonas comunes y piscina, previo a comercialización | Solo título y aviso comercial; el formulario queda fuera |

Cada formato tiene fixture reducida, aserciones específicas y un smoke live
HTTP 200 respetando `robots.txt`. Las cuatro fichas funcionaron sin Playwright,
geocodificación ni escritura de datos. El estado desconocido permanece nulo.

El radar diario añade los sitemaps oficiales de Grupo Insur, Soldit Homes y
GEMG, más el índice HTML de promociones de Grupo Egido. Los smokes aislados
procesaron respectivamente 61/4, 10/1, 12/2 y 13/13
entradas/candidatos, sin fallos y en `dry-run`.

Una segunda revisión del mismo triaje amplía la cobertura al sur metropolitano:

| Fuente | Decisión | Evidencia oficial publicada | Extracción acotada |
|---|---|---|---|
| [Los Molinos](https://www.impulsaproyectos.com/proyectos/los-molinos/) | Automática | 14 unifamiliares adosadas de 3–4 dormitorios, 205 m² y comercialización vigente en Getafe | Único contenedor de proyecto; 2 plazas se extraen solo de «espacio … para dos vehículos» |
| [Los Molinos II](https://www.impulsaproyectos.com/proyectos/los-molinos-2/) | Automática | 12 unifamiliares de 3 dormitorios, 222 m² construidos, parcela de 178 m² y comercialización vigente en Getafe | Único contenedor de proyecto, sin seguir enlaces |
| [Arroyo Homes](https://www.bimaran.es/promociones-en-curso/arroyo-homes/arroyomolinos/madrid/) | Automática | 18 unifamiliares pareadas directamente del promotor y obra comenzada en Arroyomolinos | Descripción, estado y título; se excluyen planos, galería, contacto y los 30 m² del salón |

Las tres fichas respondieron HTTP 200, respetaron `robots.txt` y produjeron una
promoción en smokes aislados sin Playwright ni geocodificación. El sitemap de
Grupo Impulsa procesó 11 fichas y no creó duplicados de sus URLs ya
monitorizadas. El índice HTML de Bimarán procesó cinco enlaces oficiales,
todos dentro del host y del máximo configurado.

## Decisiones negativas y seguimiento

Las siguientes páginas no se publican como oportunidades activas:

| Fuente | Estado | Motivo comprobado |
|---|---|---|
| Inmoglaciar La Campiña I y II | `stale` | entregas anunciadas para 2021 y primer trimestre de 2024 |
| AEDAS La Maquia, Dendra, Gumiel, Maranta y Zimara | `stale` | la ficha oficial muestra «Promoción vendida» |
| Aelca Los Robles II | `stale` | 100 % vendido |
| Grupo Insur Boadilla Essences II | `stale` | 100 % vendida y entregada |
| Grupo Egido 7Fuster y NovaVega | `stale` | promociones entregadas o 100 % vendidas |
| Aurora Homes Tebaris | `rejected` | promoción plurifamiliar en bloque, fuera del alcance unifamiliar |
| Cambrija Viridis 3.0 | `monitoring` | evidencia de lanzamiento, pero sin disponibilidad comercial actual inequívoca |
| Grupo Larvin Hernán Cortés | `monitoring` | ficha técnica accesible y actualizada, pero sin estado comercial inequívoco |
| Bimarán Las Villas y Terrazas Arroyomolinos | `stale` | las fichas oficiales están vendidas |
| Avintia Nova Alcorcón y Los Molinos I | `stale` | las fichas oficiales indican promoción entregada |
| PromoRed Real de Arganda y El Mayoral | `rejected` | promociones de vivienda colectiva, fuera del alcance unifamiliar |
| Impact Homes «Los Molinos» | `rejected` | resultados de Toledo sin relación con Getafe |
| PisosAccesibles Getafe | `stale` | anuncio histórico de 2019 |
| JPGE Studio, ArQuality y VK arquitectura | `rejected` | servicios de arquitectura, no oferta comercial de vivienda |

Las reglas se aplicarán al estado privado existente en la siguiente ejecución
completa. No se atribuye una reducción concreta de la cola hasta observarla.

## Catálogo de actores

El importador se volvió a ejecutar con las fuentes nuevas. El catálogo
interno contiene 569 actores: 197 con web declarada y 22 enlazados ya a un
canal monitorizado. Insur, Grupo Egido, Bimarán e Impulsa se reconciliaron con
sus registros de ASPRIMA; GEMG y Soldit Homes se añadieron desde el catálogo live. El archivo
solo conserva nombre, web, procedencia, evidencia y relación con fuentes; no
guarda personas, teléfonos ni correos.

Este inventario amplía el universo a revisar, pero no demuestra que los otros
547 actores tengan oferta unifamiliar vigente en la Comunidad de Madrid.
