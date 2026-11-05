# Brosh: La Finca de Los Pinarejos — 5 noviembre de 2026

## Evidencia técnica

El índice oficial `https://www.brosh.es/promociones/madrid/` responde HTTP 200
y enlaza estas cinco fichas, también HTTP 200 mediante solicitudes estáticas:

- `/promociones/madrid/la-finca-de-los-pinarejos-adosados-f1/`
- `/promociones/madrid/la-finca-de-los-pinarejos-adosados-f2/`
- `/promociones/madrid/la-finca-de-los-pinarejos-independientes-f1/`
- `/promociones/madrid/la-finca-de-los-pinarejos-independientes-f2-1/`
- `/promociones/madrid/la-finca-de-los-pinarejos-pareados-f1/`

Una consulta anterior devolvió 404; no se ha reproducido en esta comprobación
directa. No corresponde tratarlas como enlaces retirados. El contenido HTML
es real y diferencia fases y tipologías; no es una página de error blanda.

`robots.txt` permite las fichas padre y declara demora de un segundo. Prohíbe
subrutas como `adosados-fase-1/`, `aisladas/` y `pareados-fase-1/`: no seguirlas.
La configuración existente de Brosh espera cinco segundos entre solicitudes.
Esta comprobación no añade autorización jurídica distinta de la evaluación
previa del mismo operador ni habilita seguimiento de enlaces nuevos.

## Datos observados y contraste de fases

Las descripciones diferencian 20 adosados en F1, 20 en F2, 18 independientes
en F1, 11 independientes en F2 y 20 pareados en F1. Las tablas no publican
precios en las tres primeras; F2 independientes anuncia desde 688.000 € y
pareados F1 desde 629.000 €. No trasladar precios de tarjetas relacionadas a
fichas sin precio. Adosados F2 incluye tipologías pareadas en su tabla; no
reducir automáticamente la ficha a una sola tipología por su título.

El micrositio ya integrado `https://www.lospinarejos.com/` describe una
manzana P-6 de ocho pareados en el mismo conjunto y menciona fases históricas.
La coincidencia territorial no prueba identidad, pero tampoco basta la
diferencia de URL para acreditar que no hay solapamiento. No se han consultado
planos ni descargado imágenes para resolver parcelas.

## Decisión

Se incorporan Adosados F1/F2 e Independientes F1/F2 (1). Títulos, fases,
inventarios y tipologías se diferencian de P-6, ocho pareados. Los rangos
observados son 180,68–218,95; 209,86–231,57; 255,77–266,34 y 305,07 m².
No se agregan viviendas como stock disponible ni se inventa parcela exacta.
Adosados F2 conserva las dos tipologías presentes en su tabla.

Los selectores de detalle excluyen contacto y promociones sugeridas. Cuatro
fixtures reducidas y regresiones cubren nombres, inventario, dormitorios,
superficies y precio ausente. El crawl HTTP real `20261105T182736073Z`
produjo cuatro fichas válidas, cuatro fuentes correctas y cero fallos,
con estado y salida aislados. El extractor reconoce ahora viviendas
unifamiliares «aisladas» como independientes, bajo ese contexto explícito.

Pareados F1 permanece en seguimiento hasta resolver la relación con P-6;
no se cuentan cinco altas ni se afirma una parcela exacta comprobada.
