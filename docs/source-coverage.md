# Cobertura de fuentes

Fecha de revisión: **24 de julio de 2026**.

Esta matriz documenta una búsqueda sistemática para los 29 municipios del
alcance. Es una fotografía verificable, no una afirmación de que no existan
otras promociones ni una garantía de vigencia futura. Solo se integra una
fuente cuando la promoción es unifamiliar, la página es pública, el responsable
es identificable, el acceso respeta `robots.txt` y no exige registro, CAPTCHA,
área privada ni portal inmobiliario generalista.

## Resultado

- 22 fuentes live revisadas y limitadas a una URL cada una.
- 22 promociones válidas en 16 municipios distintos.
- 14 promociones añadidas desde la fotografía inicial de ocho fuentes: un
  aumento del 175 %.
- 13 municipios sin fuente integrada: no se encontró una candidata apta o la
  candidata encontrada quedó descartada por un motivo documentado.
- El perfil offline predeterminado no cambia y sigue usando solo fixtures.

## Matriz municipal

| Municipio | Estado al 2026-07-24 | Fuente o evidencia de descarte |
|---|---|---|
| Alpedrete | **Integrada** | Residencial Alpedrete — La Bellota. Micrositio oficial de 9 unifamiliares; precio y estado comercial permanecen desconocidos. |
| Becerril de la Sierra | **Integrada** | Antaro — Los Trigales. Sustituye como evidencia apta a Becerril Homes, cuyo aviso legal sigue incompleto. |
| Bustarviejo | **Integrada** | Residencial Montemilano. La ruta pública no exige registro; se aíslan los bloques informativos y se excluyen los formularios para no confundir bandas de presupuesto con precios. |
| Cabanillas de la Sierra | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente. |
| Cercedilla | Fuera de alcance | Campo 6 es una promoción multifamiliar y figura vendida. |
| Collado Mediano | Sin candidata apta | Solo se localizaron anuncios de terceros o autopromoción individual sin fuente oficial revisable. |
| Collado Villalba | **Integrada** | Apremya Homes — Residencial Puerta de Villalba. |
| El Boalo | **Integrada** | Grupo Index — El Mirador de Sierra Bonita. |
| El Escorial | Candidata mixta descartada | Puerta de Abantos combina 26 chalets con 16 bajos y dúplex en una promoción única; el contrato actual no puede aislar con rigor el inventario unifamiliar. |
| Fresnedillas de la Oliva | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente. |
| Galapagar | **Integradas (3)** | EXXACON — Living Natura, Altter — Residencial Navata Nature y STANCE — Essentia Galapagar. |
| Guadalix de la Sierra | **Integradas (3)** | Antaro — Prado de Noria, Vesari — El Tomillar y Névola Homes. Esta última publica 16 chalets, pero no un precio de vivienda; su reserva se excluye. |
| Guadarrama | **Integrada** | Gilmar — Quercus Dorf Guadarrama, comercialización exclusiva y última unidad publicada. |
| Hoyo de Manzanares | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente. |
| La Cabrera | Fuera de alcance | La actuación pública encontrada corresponde a vivienda multifamiliar en alquiler. |
| Los Molinos | Falso positivo descartado | “Residencial Los Molinos” de Impact Homes está en Seseña, no en el municipio madrileño. |
| Manzanares el Real | **Integrada** | La Quinta de Manzanares — Residencial Manzanares el Real. |
| Miraflores de la Sierra | **Integrada** | Urbanizadora Colmenar — Los Pinarejos, Manzana P-6. |
| Moralzarzal | **Integrada** | Hirimasa — C/ Pradillos, 13 unifamiliares. La página mixta se acota a tres bloques obligatorios; el rótulo visual de última vivienda no se publica como estado porque no existe en el HTML rastreable. |
| Navacerrada | Candidata histórica descartada | Qhomes — Situación 11 conserva una página corporativa antigua, pero no publica disponibilidad actual verificable; no se transforma una ficha posiblemente agotada en oportunidad vigente. |
| Navalafuente | **Integrada** | Nuvare — Cumbres de Navalafuente, con dos viviendas disponibles y tabla pública de precios. |
| Navalagamella | Sin fuente oficial apta | Se localizaron anuncios de 9 pareadas de Navalagamella S. Coop. Mad., pero no una ficha pública oficial de la cooperativa o gestora. |
| Robledo de Chavela | **Integrada** | Vesari — Luar. La evaluación registra una frase residual contradictoria y la evidencia que fija Robledo. |
| San Lorenzo de El Escorial | **Integradas (2)** | Trinosa — Etria y Vesari — Cuarteto. |
| Santa María de la Alameda | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente. |
| Soto del Real | Candidata individual descartada | Los Herrenes describe una sola vivienda, no una promoción con inventario defendible; Los Pinarejos declara ubicación en Miraflores. |
| Torrelodones | **Integrada** | Kronos Homes — Onix. |
| Valdemaqueda | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente. |
| Zarzalejo | **Integradas (2)** | Nuvare — Residencial Claveles y STANCE — Residencial Osnola. |

## Revisión y mantenimiento

Cada alta requiere una evaluación en `docs/source-assessments`, fixture
reducida, prueba offline y recorrido live aislado. Antes de ejecutar el perfil
de forma periódica hay que volver a revisar identidad, condiciones,
`robots.txt`, vigencia y selectores. Una candidata descartada puede reevaluarse
si publica la información que falta o corrige la discrepancia.
