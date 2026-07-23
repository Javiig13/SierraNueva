# Continuación de la ampliación live de julio de 2026

Fecha de revisión: **23 de julio de 2026**.

## Decisión y alcance

Se incorporan dos promociones unifamiliares públicas adicionales: Residencial
Alpedrete La Bellota y Hirimasa C/ Pradillos. El perfil pasa de 14 a 16
fuentes/promociones y de 11 a 13 municipios con cobertura. El perfil
predeterminado continúa usando exclusivamente fixtures.

Ambas integraciones usan una sola página HTML, profundidad cero,
`robots.txt`, pausa mínima de cinco segundos y ningún formulario, imagen, PDF,
Playwright o geocodificador live. Los enlaces a documentos pueden aparecer en
el contrato, pero el crawler no descarga ni republica esos documentos.

## Residencial Alpedrete — La Bellota

- Ficha: <https://residencialalpedrete.com/>.
- Equipo: <https://residencialalpedrete.com/conocenos/>.
- Aviso legal: <https://residencialalpedrete.com/aviso-legal/>.
- Robots: <https://residencialalpedrete.com/robots.txt>.
- Encaje: el micrositio publica 9 viviendas unifamiliares en Alpedrete, una
  individual y ocho pareadas, con cuatro habitaciones y piscina comunitaria.
- Identidad: la página de equipo atribuye el proyecto a Residencial Alpedrete
  S.L. y la comercialización a COPUN. El aviso legal identifica como titular
  web a Grupo COPUN, CIF B87835559, con domicilio, teléfono y correo.
- Limitación: no hay precio ni estado comercial explícito en el bloque
  rastreado. Ambos permanecen nulos/desconocidos; la existencia del formulario
  de contacto y de imágenes recientes no se convierte en disponibilidad.
- Acceso: `robots.txt` no bloquea la portada. Solo se procesa `main article`;
  se excluyen navegación, pie, legales, cookies, imágenes, formularios y
  descargas.

La fuente se clasifica como `officialMicrosite`, no como promotora, porque el
propio sitio distingue el proyecto, la sociedad mencionada en “Conócenos” y la
entidad que figura en el aviso legal. SierraNueva no resuelve esa estructura
societaria ni inventa `developerName`.

## Hirimasa — C/ Pradillos, Moralzarzal

- Página corporativa:
  <https://hirimasa.com/promociones-en-venta>.
- Aviso legal: <https://hirimasa.com/aviso-legal>.
- Robots: <https://hirimasa.com/robots.txt>.
- Identidad: Hilario Rico Matellano, S.A., NIF A28602696, con registro,
  domicilio, representante, teléfono y correo publicados.
- Encaje: 13 viviendas unifamiliares adosadas y pareadas en C/ Pradillos,
  Moralzarzal; aproximadamente 150 m², cuatro dormitorios, dos baños, aseo,
  jardín y zona común con piscina.
- Vigencia: la tarjeta visible muestra “ÚLTIMA VIVIENDA”, pero el rótulo se
  genera mediante CSS y no forma parte del texto HTML recuperado por el
  crawler. Se documenta la observación, pero `commercialStatus` queda
  `unknown` para no afirmar algo que la ejecución periódica no puede volver a
  verificar.
- Acceso: `robots.txt` permite la página pública. No se accede a administración,
  formularios, planos ni imágenes y no se republican descripciones completas.

La página mezcla cinco proyectos, incluidos uno fuera del ámbito y otros
finalizados. La configuración exige tres bloques exclusivos de Moralzarzal:
título, descripción y ubicación. Si cualquiera desaparece, la extracción falla
de forma cerrada. La fixture incluye promociones ajenas antes y después del
bloque y comprueba que sus totales, superficies y estados no contaminan el
resultado.

Los identificadores de Elementor usados como selectores pueden cambiar cuando
se edite la página. Esa fragilidad es deliberadamente visible: un cambio
estructural produce un fallo de fuente y conserva el último dataset válido, en
vez de leer toda la página y publicar una mezcla.

## Candidatas no incorporadas

- **Puerta de Abantos, El Escorial:** la fuente oficial presenta un único
  conjunto de 42 viviendas que mezcla 26 chalets con 16 bajos y dúplex. El
  contrato actual representa promociones unifamiliares y no puede separar con
  rigor disponibilidad, precios y totales del subconjunto sin modelar
  inventario mixto.
- **Navalagamella S. Coop. Mad.:** se localizaron anuncios consistentes de
  nueve chalets pareados comercializados por Allegro, pero no una ficha oficial
  pública de la cooperativa o gestora. Los portales generalistas sirven como
  pista, no como fuente integrada.

## Verificación

Cada formato tiene una fixture reducida y una prueba offline. Los dry runs
individuales terminaron con una promoción y cero fallos. La primera ejecución
conjunta reveló HTTP 429 en la tercera ficha de Vesari aun con diez segundos
exactos entre solicitudes; se elevó la pausa compartida a veinte segundos y se
añadió una prueba para impedir su reducción accidental.

La repetición conjunta terminó con 16/16 fuentes, 16 promociones, cero fallos,
cero inválidas y 16/16 elementos en GeoJSON. Quince ubicaciones son centroides
municipales trazables y una conserva coordenadas exactas. El `runId` aislado
fue `20260723T214132164Z`.
