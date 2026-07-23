# Cobertura de fuentes

Fecha de revisión: **23 de julio de 2026**.

Esta matriz documenta una búsqueda sistemática para los 29 municipios del
alcance. Es una fotografía verificable, no una afirmación de que no existan
otras promociones ni una garantía de vigencia futura. Solo se integra una
fuente cuando la promoción es unifamiliar, la página es pública, el responsable
es identificable, el acceso respeta `robots.txt` y no exige registro, CAPTCHA,
área privada ni portal inmobiliario generalista.

## Resultado

- 8 fuentes live revisadas y limitadas a una URL cada una.
- 8 promociones válidas en 7 municipios distintos.
- 7 fuentes nuevas además de EXXACON — Living Natura.
- 22 municipios sin fuente integrada: no se encontró una candidata apta o la
  candidata encontrada quedó descartada por un motivo documentado.
- El perfil offline predeterminado no cambia y sigue usando solo fixtures.

## Matriz municipal

| Municipio | Estado al 2026-07-23 | Fuente o evidencia de descarte |
|---|---|---|
| Alpedrete | Sin fuente integrada | DC Inmobiliaria y Nordec publican referencias locales, pero no se pudo cerrar identidad legal, disponibilidad y condiciones con el rigor exigido. |
| Becerril de la Sierra | Descartada | Becerril Homes publica Calvo Sotelo 63/89, pero su aviso legal conserva campos de plantilla sin razón social, domicilio ni identificador. |
| Bustarviejo | Descartada | Residencial Montemilano presenta una pantalla de registro y no ofrece una ficha pública suficiente. |
| Cabanillas de la Sierra | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente. |
| Cercedilla | Fuera de alcance | Campo 6 es una promoción multifamiliar y figura vendida. |
| Collado Mediano | Sin candidata apta | Solo se localizaron anuncios de terceros o autopromoción individual sin fuente oficial revisable. |
| Collado Villalba | **Integrada** | Apremya Homes — Residencial Puerta de Villalba. |
| El Boalo | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente en El Boalo, Cerceda o Mataelpino. |
| El Escorial | Sin candidata apta | Las referencias encontradas eran vagas, antiguas o multifamiliares. |
| Fresnedillas de la Oliva | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente. |
| Galapagar | **Integradas (2)** | EXXACON — Living Natura y Altter — Residencial Navata Nature. |
| Guadalix de la Sierra | Sin candidata apta | Se localizaron anuncios de terceros, pero no una ficha oficial con identidad y vigencia comprobables. |
| Guadarrama | **Integrada** | Gilmar — Quercus Dorf Guadarrama, comercialización exclusiva y última unidad publicada. |
| Hoyo de Manzanares | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente. |
| La Cabrera | Fuera de alcance | La actuación pública encontrada corresponde a vivienda multifamiliar en alquiler. |
| Los Molinos | Falso positivo descartado | “Residencial Los Molinos” de Impact Homes está en Seseña, no en el municipio madrileño. |
| Manzanares el Real | **Integrada** | La Quinta de Manzanares — Residencial Manzanares el Real. |
| Miraflores de la Sierra | **Integrada** | Urbanizadora Colmenar — Los Pinarejos, Manzana P-6. |
| Moralzarzal | Fuera de alcance/sin vigencia | Plan Vive es multifamiliar en alquiler; otras referencias localizadas eran antiguas. |
| Navacerrada | Sin candidata apta | Solo se localizaron agregadores o anuncios de terceros. |
| Navalafuente | Fuera de alcance | La oferta municipal encontrada era de inmuebles existentes, no promoción de obra nueva. |
| Navalagamella | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente. |
| Robledo de Chavela | Descartada | Grupo Almenara publica un chalet en La Suiza II, pero no identifica claramente al titular y ofrece 263 m² en la ficha frente a 363 m² en el índice. |
| San Lorenzo de El Escorial | **Integrada** | Trinosa — Etria. |
| Santa María de la Alameda | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente. |
| Soto del Real | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente; Los Pinarejos declara ubicación en Miraflores. |
| Torrelodones | **Integrada** | Kronos Homes — Onix. |
| Valdemaqueda | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente. |
| Zarzalejo | Sin candidata apta | No se localizó una promoción unifamiliar oficial, pública y vigente. |

## Revisión y mantenimiento

Cada alta requiere una evaluación en `docs/source-assessments`, fixture
reducida, prueba offline y recorrido live aislado. Antes de ejecutar el perfil
de forma periódica hay que volver a revisar identidad, condiciones,
`robots.txt`, vigencia y selectores. Una candidata descartada puede reevaluarse
si publica la información que falta o corrige la discrepancia.
