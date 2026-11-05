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

## Datos observados, no incorporados todavía

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

Mantener los cinco candidatos en seguimiento privado, no descartarlos por
404 ni contarlos como cinco promociones nuevas verificadas. Antes de alta:
contrastar parcela/fase con P-6, aplicar selectores de detalle que excluyan
contacto y promociones sugeridas y añadir fixtures reducidas y regresiones
por fase. No se modifica el dataset público para simular cobertura.
