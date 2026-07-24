# Revisión de candidatos comerciales prioritarios

Fecha de revisión: **24 de julio de 2026**.

## Alcance y método

Se revisaron las nueve páginas comerciales que quedaron en prioridad alta tras
el triaje privado. Para cada URL se comprobó la respuesta HTTP, la ruta frente
a `robots.txt`, el contenido visible, el encaje unifamiliar, la vigencia y si
la página era fuente primaria o duplicaba una promoción ya registrada. No se
enviaron formularios, no se descargaron imágenes o documentos y no se usó
Playwright.

La sede electrónica de Collado Mediano no forma parte de estas nueve páginas:
su señal administrativa sigue en `monitoring` y su `robots.txt` bloquea la ruta
de detalle.

## Decisiones

| Municipio | Página | Decisión | Evidencia determinante |
|---|---|---|---|
| Alpedrete | Montevirey — Linos Residencial | `stale` | La propia ficha muestra «PROMOCION TERMINADA». |
| Galapagar | Grupo Inmobiliario MG — Living Natura | `rejected` | Es una ficha de agencia que reproduce Living Natura, ya cubierta por la URL canónica de EXXACON. |
| Guadalix de la Sierra | aProperties — búsqueda territorial | `stale` | La página declara que no dispone de promociones en esa zona y muestra resultados de otros municipios. |
| Navacerrada | Blossom — Qhomes 11 | `stale` | La ficha corporativa identifica el proyecto como «VENDIDO». |
| Guadarrama | Via Appia — Jardines de la Torre | `rejected` | La fuente oficial publica 11 viviendas, pero no demuestra que sean unifamiliares; sin esa evidencia no puede entrar en el alcance. |
| Galapagar | Almicasa — Procesiones 6 | `rejected` | La propia ficha describe pisos, bajos y dúplex de uno y dos dormitorios, no una promoción unifamiliar. |
| Collado Villalba | Avalon Studio — ONVLB1 | `rejected` | Reproduce Residencial Puerta de Villalba, ya rastreada desde la promotora Apremya Homes. |
| El Boalo | Habitania — Promoción Goya | `stale` | La ruta devuelve una página que dice que no se encuentra el resultado y ofrece inmuebles alternativos. |
| El Boalo | Inara Homes — Residencial El Boalo | **fuente admitida** | Dominio y sociedad de la promotora, cinco viviendas unifamiliares, precio desde 499.000 €, cuatro dormitorios, tres baños y contacto comercial vigentes. |

Las ocho decisiones negativas se codifican como `reviewRules` del canal
SearXNG para reconciliar también los candidatos ya existentes. `stale` se usa
para promociones o resultados que existieron pero ya no están disponibles;
`rejected` para duplicados o producto fuera de alcance.

## Inara Homes — controles de admisión

- Ficha:
  <https://www.inarahomes.com/inara-smart-homes-viviendas-eco-eficientes-e-innovadoras/>.
- Privacidad: <https://www.inarahomes.com/politica-privacidad/>.
- Robots: <https://www.inarahomes.com/robots.txt>.
- Sitemap: <https://www.inarahomes.com/sitemap.xml>.
- Identidad: la política de privacidad identifica a Inara Smart Homes S.L.,
  CIF B-67858779. La ficha y el pie muestran la misma sociedad y el dominio
  describe a Inara Homes como promotora.
- Acceso: HTML público por HTTPS, sin autenticación ni JavaScript necesario
  para los datos utilizados. `robots.txt` solo bloquea `/wp-admin/` y publica
  el sitemap.
- Vigencia observada: la ficha conserva precio, estado de proyecto,
  descripción unifamiliar y llamada de contacto. No se publica una fecha de
  entrega ni un estado comercial más preciso porque la página no los declara.

La fuente se limita a una URL, profundidad cero, una página y cinco segundos
de pausa. Tres bloques obligatorios aíslan cabecera, resumen e información;
excluyen navegación, pie y formulario. No se visitan galerías, planos, memoria
de calidades ni enlaces de mapas. El municipio queda fijado tras revisión. El
tipo concreto de unifamiliar permanece vacío porque la ficha no aclara si las
viviendas son independientes, pareadas o adosadas.

El sitemap oficial se añade solo como canal privado de descubrimiento, filtrado
al `page-sitemap`. Detecta futuras páginas candidatas pero no las incorpora
automáticamente al dataset público.
