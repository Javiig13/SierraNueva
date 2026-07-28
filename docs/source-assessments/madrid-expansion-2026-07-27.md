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

Las reglas se aplicaron al estado privado existente en la ejecución completa
`30279016501`. El triaje cifrado posterior `30281037199` confirmó 865
pendientes: 91 de prioridad alta, 260 media, 497 baja y 17 duplicados. Frente
a la instantánea anterior, alta bajó de 98 a 91 y media de 298 a 260 aunque la
matriz conservó más señales; el aumento total procede principalmente de
resultados nuevos clasificados como baja. El informe detallado siguió privado.

## Catálogo de actores

El importador se volvió a ejecutar con las fuentes nuevas. El catálogo
interno contiene 569 actores: 197 con web declarada y 22 enlazados ya a un
canal monitorizado. Insur, Grupo Egido, Bimarán e Impulsa se reconciliaron con
sus registros de ASPRIMA; GEMG y Soldit Homes se añadieron desde el catálogo live. El archivo
solo conserva nombre, web, procedencia, evidencia y relación con fuentes; no
guarda personas, teléfonos ni correos.

Este inventario amplía el universo a revisar, pero no demuestra que los otros
547 actores tengan oferta unifamiliar vigente en la Comunidad de Madrid.

## Verificación completa

El commit `f14a9d2` superó el CI `30278248263`. La ejecución manual
`30279016501` completó las 716 consultas, conservó 612 resultados de búsqueda,
dejó 61/61 canales de radar sanos y cobertura 179/179 con 28 canales
municipales directos. Las 32 fuentes comerciales automáticas terminaron
correctas y sin fallos; se publicaron 33 promociones válidas —26 activas— y 33
elementos GeoJSON, con tres altas y una modificación en ese crawl. La IA se
omitió.

Pages publicó el `runId` `20260727T153254903Z`. La portada y los tres contratos
públicos respondieron HTTP 200; las rutas privadas de candidatos,
enriquecimiento y auditoría devolvieron HTTP 404. Las siete fichas de ambos
lotes están presentes con sus URLs oficiales.

## Tercera revisión por dominio

El artefacto privado cifrado de la ejecución `30288195132` se descargó,
descifró y autenticó localmente. El SHA-256 del archivo cifrado fue
`e8feb8b66e8429027fc74dba671b50d15cc11a5399907f54e4ae67b08d04ad23`.
La clave RSA efímera se eliminó después de validar el contenido. La instantánea
contenía 865 pendientes: 91 altos, 260 medios, 497 bajos y 17 duplicados
probables. Se revisaron los 91 altos agrupados por dominio.

| Fuente | Decisión | Evidencia oficial publicada | Extracción acotada |
|---|---|---|---|
| [Antaro Móstoles Polar](https://antaro.es/cooperativas/mostoles-polar/) | Automática | Cooperativa de 11 chalets en Móstoles, desde 481.000 € | Contenido propio de la ficha, sin navegación ni contacto |
| [Trinosa Bonart](https://trinosa.com/promocion/bonart/) | Automática por REST pública | Ocho villas, precio desde 3,2 M€, 1.000 m² construidos y parcela de 1.500 m² | Un único contenido WordPress oficial; la URL canónica permanece en el mismo host |
| [Kronos Krest](https://kronoshomes.com/es/promociones/krest/) | Automática | 31 pareados de 4–5 dormitorios en Torrelodones, últimas unidades | JSON-LD y bloque principal de la ficha |
| [Brosh Villa 2](https://www.brosh.es/promociones/madrid/villa-2-mostoles/) | Automática | 17 adosados de 3 dormitorios en Móstoles, obras en curso | Tabla y descripción de la ficha |
| [Brosh Villa 6 fase 1](https://www.brosh.es/promociones/madrid/villa-6-navalcarnero-fase-1/) | Automática | 22 chalets de 4 dormitorios en Navalcarnero, obras en curso | Tabla y descripción de la ficha |
| [Brosh Villa 6 fase 2](https://www.brosh.es/promociones/madrid/villa-6-navalcarnero-fase-2/) | Automática | 26 adosados de 4 dormitorios, licencia y obras en curso | Tabla y descripción de la ficha |
| [Naiz Aceris](https://naizhomes.com/promociones/aceris/) | Automática | Próxima promoción unifamiliar en Pozuelo | Solo información publicada; inventario y precio permanecen nulos |
| [Naiz Pinea](https://naizhomes.com/promociones/pinea/) | Automática | 11 viviendas independientes de 5–6 dormitorios, parcelas de 500–700 m² | Bloque principal de la promoción |

Las ocho fichas produjeron una promoción y una fuente sana en smokes HTTP
reales sin Playwright ni geocodificación. Los casos reducidos se fijaron como
fixtures y cubren además un `h1` vacío, `villas`, superficies en tablas, el
estado `Disponible`, «próxima promoción» y la distinción entre superficie
construida y parcela.

El sitemap de Brosh solo enumera la portada, por lo que se añadió su índice
oficial acotado: procesó 26 enlaces del mismo host. El sitemap multilingüe de
Naiz no ofrecía fichas útiles de forma fiable; se sustituyó por el índice
español, que procesó 12 entradas y generó cinco candidatos privados tras un
seguimiento de detalle limitado. El radar queda en 64 canales —63 activos— y
el perfil comercial en 42 fuentes —40 automáticas—. El catálogo interno
mantiene 569 actores, 197 con web declarada y eleva a 24 los monitorizados.

Las fichas vendidas o entregadas, el producto plurifamiliar, agregadores,
directorios y ruido administrativo se codificaron como `stale` o `rejected`.
Los expedientes de planeamiento o contratación que podrían anticipar vivienda
permanecen en `monitoring`: son señales privadas de futuro, no promociones
publicables. Ninguna decisión negativa concede por sí sola exhaustividad y debe
reabrirse si aparece evidencia oficial nueva.

## Cuarta revisión por dominio

El 28 de julio se reabrió Mirage porque la ficha oficial ya publica estado
`En Comercialización`, precios y tipología. La promoción combina producto
unifamiliar y plurifamiliar: SierraNueva la incorpora porque existe producto
unifamiliar explícito, pero no inventa un subtipo más concreto ni reparte
inventario entre ambos. El smoke HTTP real obtuvo una promoción válida sin
Playwright ni geocodificación.

| Fuente | Decisión | Evidencia oficial publicada | Extracción acotada |
|---|---|---|---|
| [Kronos Mirage](https://www.kronoshomes.com/es/proyectos-obra-nueva/madrid/mirage/) | Automática | En comercialización en Villanueva de la Cañada; 3 dormitorios y precios publicados entre 790.800 € y 920.800 € | Bloque de resumen comercial y JSON-LD; no se siguen planos, imágenes, formularios ni área de clientes |
| [Cambrija Viridiana](https://cambrija.es/obra-nueva-viridiana/) | Automática | Adosados y pareados configurables de 4–5 dormitorios desde 1.232.000 € en Boadilla del Monte | Contenido de la página; no se descargan planos, imágenes ni formularios |
| [Larvin Hernán Cortés](https://larvin.es/venta-chalets-madrid/chalets-boadilla-del-monte/) | Automática | Unifamiliares configurables de 3–5 dormitorios y piscina comunitaria en Boadilla del Monte | Contenido propio; precio e inventario permanecen nulos |
| [Larvin Chalets en Rivas](https://larvin.es/venta-chalets-madrid/chalets-en-rivas-vaciamadrid/) | Automática | 14 unifamiliares configurables de 3–4 dormitorios en Rivas-Vaciamadrid | Contenido propio; se registra como alias la URL antigua enlazada por el índice |
| [Larvin Monte de la Villa](https://larvin.es/venta-chalets-madrid/chalets-villaviciosa-de-odon/) | Automática | Viviendas individuales de 4 dormitorios y parcelas de 525 m² en Villaviciosa de Odón | Contenido propio; precio e inventario permanecen nulos |
| [Larvin Residencial 48](https://larvin.es/venta-chalets-madrid/chalets-en-villaviciosa-de-odon/) | Automática | Viviendas individuales de 3 dormitorios con piscina comunitaria en Villaviciosa de Odón | Contenido propio; no se traslada el contador del índice a la ficha |

El índice regional de Kronos no es una promoción y se descarta mediante una
regla anclada al final de URL para no afectar a sus fichas hijas. Dos
resultados de `obrasnuevas.com` devolvieron la portada general en lugar de la
ficha anunciada y quedaron `rejected`; el agregador no se usa como fuente
canónica. Bonart dejó de reaparecer como nueva mediante una equivalencia
explícita y validada entre la URL REST usada para extraer y su URL pública.

El enlace del artículo antiguo de Viridis conduce ahora a Viridiana, cuya
ficha primaria sí contiene evidencia comercial vigente; el artículo se marca
`stale` y la ficha se integra. El índice oficial de Larvin se recorre con cinco
enlaces y seguimiento acotado de sus bloques de contenido. Cuatro URLs quedan
`verifiedSource`; la promoción de Pozuelo declara expresamente
`PROMOCIÓN VENDIDA` y queda `stale`. La detección geográfica prioriza título y
URL antes del bloque de recomendaciones para impedir que “otras promociones”
reasigne el municipio.

Las seis altas de esta cuarta revisión produjeron una promoción válida cada
una mediante HTTP real, sin Playwright ni geocodificación. El perfil live queda
en 48 fuentes —46 automáticas— y el radar en 66 canales —65 activos—: 30
canales comerciales, con 23 sitemaps y 10 índices HTML acotados.
