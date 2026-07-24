# Evaluación del radar web privado SearXNG

Fecha de evaluación: **24 de julio de 2026**.

## Decisión

Se incorpora SearXNG como capa privada de descubrimiento web. SierraNueva no
raspa páginas HTML de resultados ni utiliza una instancia pública: cada
ejecución live levanta temporalmente la imagen oficial dentro del runner,
consulta su API JSON por `127.0.0.1` y destruye el contenedor al terminar.

La integración no es una fuente canónica y nunca publica resultados por sí
sola. Solo añade enlaces candidatos al estado privado del radar.

## Matriz diaria

La matriz cruza los 29 municipios habilitados con cuatro familias:

1. obra nueva, nueva promoción, chalets y viviendas unifamiliares;
2. promotora, promoción, residencial o cooperativa junto con vivienda;
3. licencia, estudio de detalle, plan parcial o urbanización residencial;
4. venta o adjudicación de parcelas, suelo residencial y derecho de
   superficie.

Son **116 consultas en cada ejecución diaria**, sin rotación ni muestreo. Cada
consulta admite como máximo diez resultados y la fuente conserva como máximo
2.000 URLs distintas.

## Encaje técnico

- Formato privado `searxngJson`, aislado en Infrastructure.
- Plantillas y exclusiones en los dos catálogos de radar.
- Fixture JSON reducida y pruebas sin Internet.
- Resultado asociado al municipio consultado, pero los términos de
  oportunidad y contexto deben aparecer también en el título o fragmento
  devuelto. La consulta no se usa como evidencia.
- Solo se admiten destinos HTTPS.
- Deduplicación por URL normalizada entre las 116 consultas y persistencia
  estable entre días mediante la cola existente.
- Confianza limitada al intervalo 0,45–0,70, inferior a una fuente oficial.
- La URL encontrada se conserva como dominio referido para localizar canales
  aún no monitorizados.

## Seguridad y operación

El único HTTP no cifrado permitido es `127.0.0.1` y exclusivamente para el
formato SearXNG. Ese cliente:

- no sigue redirecciones;
- no usa cookies;
- no admite otro host local o de red privada;
- no inicia Playwright, proxies ni mecanismos anti-CAPTCHA;
- no descarga páginas destino.

Los grandes portales inmobiliarios, redes sociales y propios buscadores quedan
excluidos antes de crear candidatos. La configuración conserva el bloqueo de
Idealista, Fotocasa, Pisos.com, Yaencontre, Habitaclia, Milanuncios, Trovit y
agregadores equivalentes.

La imagen oficial queda fijada para `linux/amd64` por el digest:

```text
ghcr.io/searxng/searxng@sha256:c3487408922e1d2673b76c244d5a400f74c489842b73c48269ae90392ff9c579
```

SearXNG está licenciado bajo AGPL-3.0. SierraNueva no modifica ni redistribuye
su código; ejecuta la imagen oficial efímera y mantiene su configuración local
en `config/searxng/settings.yml`.

## Fallos y límites

Los motores agregados pueden limitar, bloquear o devolver cero resultados al
runner. No se intenta eludir esas respuestas. Si el contenedor o la API fallan,
la fuente queda degradada dentro del radar, los candidatos anteriores se
conservan y el dataset comercial/Pages puede continuar.

El daemon Docker local no estaba iniciado durante la primera implementación,
pero GitHub Actions `30119685200` verificó la integración real completa:
`/healthz`, 116 consultas, 567 resultados, 340 candidatos filtrados y cierre
correcto del contenedor. El radar acumuló 365 candidatos antes de aplicar
estados de revisión y terminó con 334 pendientes. La fuente SearXNG quedó sana;
el único canal degradado fue el tablón de Los Molinos por HTTP 403.

La ejecución completa duró 10 min 5 s y desplegó Pages con 22 promociones,
22/22 fuentes comerciales y cero fallos. La cola privada sigue fuera del
artefacto público: su URL devuelve HTTP 404.

## Referencias primarias

- [API de búsqueda de SearXNG](https://docs.searxng.org/dev/search_api.html)
- [Instalación mediante contenedor](https://docs.searxng.org/admin/installation-docker.html)
- [Repositorio oficial](https://github.com/searxng/searxng)
