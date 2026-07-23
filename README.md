# SierraNueva

SierraNueva localiza, normaliza y presenta promociones de obra nueva unifamiliar
en la Sierra de Madrid. El repositorio contiene un crawler/ETL en .NET y una
aplicación Blazor WebAssembly completamente estática. El MVP prioriza fuentes
oficiales y excluye los grandes portales inmobiliarios.

El repositorio público oficial es
`https://github.com/Javiig13/SierraNueva`. Incluye CI offline y un workflow
manual/diario que genera el dataset live y publica la SPA en
`https://javiig13.github.io/SierraNueva/`.

## Continuar el proyecto

El repositorio contiene todo el contexto necesario para trasladarlo a otro
equipo o agente. La lectura recomendada es:

1. [AGENTS.md](AGENTS.md), reglas de trabajo y comandos de control.
2. [docs/HANDOFF.md](docs/HANDOFF.md), estado comprobado y limitaciones reales.
3. [docs/PROJECT_BRIEF.md](docs/PROJECT_BRIEF.md), especificación consolidada.
4. [docs/ROADMAP.md](docs/ROADMAP.md), pendientes y criterios de aceptación.
5. [docs/architecture.md](docs/architecture.md), diseño y fronteras técnicas.

La documentación distingue entre funcionalidad local, automatización
comprobada e integraciones live que aún requieren trabajo.

## Qué hace

- Ejecuta un pipeline `configuración → descubrimiento → extracción →
  normalización → deduplicación → cambios → archivos públicos`.
- Procesa HTML estático con JSON-LD, metadatos y patrones españoles.
- Incluye descubrimiento por URLs configuradas, archivo manual, sitemap e
  hipervínculos internos.
- Ejecuta un radar privado y separado para señales de BOCM, BOE, contratación
  pública y suelo público, sin confundirlas con promociones verificadas.
- Respeta `robots.txt`, limita solicitudes y bloquea portales, esquemas
  peligrosos y direcciones privadas.
- Mantiene estado y evita desactivar una promoción hasta tres ausencias
  consecutivas en ejecuciones completas.
- Genera JSON canónico, CSV, GeoJSON, historial de cambios y resumen de
  ejecución.
- Muestra listado, filtros compartibles, detalle y mapa Leaflet/OpenStreetMap.
- Funciona sin Internet usando fixtures sintéticos.

No descarga imágenes, no resuelve CAPTCHA, no entra en áreas privadas, no usa
APIs de pago, no ofrece usuarios ni administración web y no presenta datos
ausentes como si fueran conocidos.

## Requisitos

- SDK .NET `10.0.301` o un parche compatible de esa feature band.
- Windows PowerShell 5.1 o posterior en Windows; Bash en Linux/macOS.
- Edge, Chrome o Chromium para la suite E2E; Chromium de Playwright si se
  habilita además una fuente dinámica.

## Puesta en marcha local

El camino directo en Windows es:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/run-local.ps1
```

En Linux o macOS:

```bash
bash ./scripts/run-local.sh
```

Los scripts restauran, compilan, instalan Chromium si falta, ejecutan pruebas y
formato, validan configuración, recorren las fixtures, validan datos, publican
la SPA y finalmente inician el frontend. Para omitir la instalación:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/run-local.ps1 -SkipPlaywrightInstall
```

Comandos manuales equivalentes:

```powershell
dotnet restore SierraNueva.sln
dotnet build SierraNueva.sln -c Release --no-restore
dotnet test SierraNueva.sln -c Release --no-build
dotnet format SierraNueva.sln --verify-no-changes --no-restore
dotnet run --project src/SierraNueva.Crawler -c Release --no-build -- validate-config
dotnet run --project src/SierraNueva.Crawler -c Release --no-build -- discover-opportunities --dry-run
dotnet run --project src/SierraNueva.Crawler -c Release --no-build -- crawl --no-playwright
dotnet run --project src/SierraNueva.Crawler -c Release --no-build -- validate-data
dotnet publish src/SierraNueva.Web/SierraNueva.Web.csproj -c Release --no-restore
dotnet run --project src/SierraNueva.Web
```

El proyecto web enlaza `data/public` como `wwwroot/data` durante la compilación.
El script recompila el frontend después del crawler para que el servidor de
desarrollo use siempre la última salida.

## CLI

```text
SierraNueva.Crawler crawl
  --config <ruta>
  --sources <ruta>
  --municipalities <ruta>
  --centroid-sources <ruta>
  --exclusions <ruta>
  --output <ruta>
  --state <ruta>
  --municipality <nombre>
  --source <id>
  --max-pages <n>
  --no-playwright
  --no-geocoding
  --dry-run
  --verbose

SierraNueva.Crawler validate-config
SierraNueva.Crawler validate-data
SierraNueva.Crawler discover-opportunities
  --discovery-sources <ruta>
  --municipalities <ruta>
  --state <ruta>
  --source <id>
  --from <aaaa-mm-dd>
  --to <aaaa-mm-dd>
  --dry-run

SierraNueva.Crawler review-opportunity
  --state <ruta>
  --candidate <id>
  --status <new|monitoring|rejected|verifiedSource|stale>
```

Códigos de salida: `0` éxito, `1` éxito parcial, `2` configuración inválida,
`3` fallo total o cancelación y `4` datos públicos inválidos.

La configuración predeterminada habilita únicamente `fixtures-locales`. Por
tanto, el comando básico no realiza solicitudes externas. El perfil explícito
`config/sources.live.json` contiene ocho fuentes revisadas y limitadas, pero
nunca se usa en la baseline ni en pruebas automáticas.

El radar sigue el mismo principio. `config/discovery-sources.json` usa 29
fuentes con fixtures y es completamente offline.
`config/discovery-sources.live.json` habilita de forma explícita los cuatro
canales centrales y 25 fuentes municipales: cinco tablones `eAdmin`, 18
portadas públicas de sedes `sedelectronica.es`, el tablón de transparencia de
Bustarviejo y el RSS oficial de Cercedilla:

```powershell
dotnet run --project src/SierraNueva.Crawler -c Release --no-build -- `
  discover-opportunities `
  --discovery-sources config/discovery-sources.live.json `
  --from 2026-07-23 --to 2026-07-23 `
  --state tmp/opportunity-live
```

Los resultados se guardan únicamente en `data/state` o en la ruta aislada
indicada. Un candidato no es una promoción ni se incorpora automáticamente al
contrato público.

BOCM admite backfill por intervalos de hasta 367 días inclusivos. Para recorrer
varios años se ejecuta un lote por año, siempre con una ruta de estado privada:

```powershell
dotnet run --project src/SierraNueva.Crawler -c Release --no-build -- `
  discover-opportunities `
  --discovery-sources config/discovery-sources.live.json `
  --source bocm-calendar `
  --from 2025-01-01 --to 2025-12-31 `
  --state tmp/bocm-2025
```

## Configurar fuentes

Las fuentes viven en [config/sources.json](config/sources.json). Cada una
controla hosts permitidos, URLs iniciales, profundidad, límites, espera entre
peticiones, sitemaps, Playwright, patrones y selectores específicos. El
[ejemplo documentado](config/sources.example.json) usa dominios `.example` y
está deshabilitado de forma intencionada.

Las fichas de una sola promoción pueden declarar `fixedMunicipality` después
de una revisión humana. `contentSelector` y, si hace falta,
`additionalContentSelectors` limitan la extracción a los bloques de la
promoción; si alguno desaparece, la fuente falla de forma cerrada en vez de
leer navegación, pies o promociones relacionadas.

Para añadir URLs manuales, usa `manualUrlsFile` y una URL HTTP(S) oficial por
línea. No apuntes el crawler a resultados de buscadores ni a portales
inmobiliarios. La blocklist está en
[config/domain-exclusions.json](config/domain-exclusions.json).

Para depurar una fuente:

```powershell
dotnet run --project src/SierraNueva.Crawler -- crawl `
  --source identificador-de-fuente --max-pages 3 --verbose --dry-run
```

La comprobación manual de una fuente live usa rutas de salida y estado aisladas
para no tocar el último dataset local válido:

```powershell
dotnet run --project src/SierraNueva.Crawler -c Release -- crawl `
  --config config/appsettings.live.json `
  --sources config/sources.live.json `
  --source exxacon-living-natura `
  --max-pages 1 --no-playwright --no-geocoding --dry-run --verbose `
  --output tmp/live-source/public --state tmp/live-source/state
```

La evaluación de identidad, condiciones, `robots.txt`, alcance y mitigaciones
está en [docs/source-assessments](docs/source-assessments). La matriz completa
de los 29 municipios está en
[docs/source-coverage.md](docs/source-coverage.md). Ambas revisiones deben
repetirse antes de automatizar o ampliar el rastreo.

Playwright es un fallback explícito. Se ejecuta solo si la configuración global
y la fuente tienen habilitado Playwright y el HTML inicial es insuficiente. No
incluye evasión, fingerprints falsos ni resolución de CAPTCHA.

## Configurar municipios

[config/municipalities.json](config/municipalities.json) contiene los 29
municipios iniciales. Se pueden habilitar, deshabilitar o ampliar sin
recompilar. Los alias permiten resolver, por ejemplo, Cerceda y Mataelpino como
localidades de El Boalo.

Los 29 municipios incluyen coordenadas procedentes del Nomenclátor Geográfico
de Municipios y Entidades de Población 2026 del IGN. La fuente publica ETRS89,
compatible con WGS84 en la península. La trazabilidad, el registro municipal,
el hash del ZIP original y la atribución están en
[config/municipality-centroids.json](config/municipality-centroids.json) y
`validate-config` comprueba automáticamente que ambos archivos coinciden.

Nominatim está deshabilitado por defecto. Si se habilita, usa caché persistente,
una sola solicitud simultánea y un máximo inicial de cuatro solicitudes por
minuto. Debe configurarse un User-Agent de contacto real antes de usarlo.

## Datos generados

Los archivos públicos son:

```text
data/public/promotions.json
data/public/promotions.csv
data/public/promotions.geojson
data/public/changes.json
data/public/run.json
```

El estado interno, que nunca debe publicarse como parte del sitio, es:

```text
data/state/promotions-state.json
data/state/promotions-state.backup-1.json
data/state/promotions-state.backup-2.json
data/state/geocoding-cache.json
data/state/http-cache.json
data/state/opportunity-candidates.json
data/state/opportunity-candidates.backup-1.json
data/state/opportunity-candidates.backup-2.json
```

Los archivos `opportunity-candidates*` están ignorados por Git para reducir el
riesgo de publicar accidentalmente una cola live.

`promotions.json` es el contrato canónico, versión `1.0`. Importes y
superficies son números, las marcas temporales son UTC y los enums se
serializan como texto. Cada promoción conserva evidencias breves con fuente,
extractor, calidad y confianza. `sourceConfidenceExplanation` enumera la base y
cada señal aplicada al score. La UI presenta moneda y fechas en cultura
española y zona de Madrid.

En local, la URL de datos esperada es:

```text
http://localhost:<puerto>/data/promotions.json
```

## Confianza y calidad

`sourceConfidence` mide la confianza en la identidad y cercanía de la fuente;
no es una garantía comercial ni jurídica. Una fuente oficial configurada parte
de una puntuación superior. Dossier, identidad de promotora, evidencias y
coincidencia de dominio pueden aumentarla. La ficha muestra esas señales y su
impacto de forma estructurada.

`run.json` diferencia promociones válidas, inválidas y advertencias. Una
ubicación por centroide se etiqueta como aproximada. El pipeline valida rangos,
coordenadas, precios y URLs antes de publicar.

## Extender el crawler

- Un extractor general implementa `IPromotionExtractor`; los selectores por
  dominio permanecen en configuración.
- Un nuevo mecanismo de descubrimiento implementa `IUrlDiscoveryProvider` y se
  registra junto a los proveedores existentes.
- Un nuevo canal administrativo implementa `IOpportunityFeedReader`; su salida
  permanece en el contrato privado del radar y nunca se entrega al frontend.
- La extracción de PDF está aislada por `IPdfTextExtractor`.
- El render dinámico está aislado por `IDynamicPageRenderer`.
- La geocodificación está aislada por `IGeocoder`.

Las fronteras y el flujo completo se describen en
[docs/architecture.md](docs/architecture.md).

## Recuperar el último dataset válido

La escritura pública se realiza en archivos temporales y solo después se
reemplazan los destinos. Si todas las fuentes fallan, el pipeline no escribe
un dataset vacío. Conserva `data/public` y `data/state` de la última ejecución
válida, corrige la fuente y usa `validate-data` antes de volver a publicar.

Antes de reemplazar `promotions-state.json`, el repositorio rota dos copias
atómicas. Si el estado principal está corrupto, intenta `backup-1` y después
`backup-2`, registrando una advertencia. Si las tres copias fallan, aborta sin
sobrescribirlas. Para recuperar manualmente, conserva primero los tres archivos,
valida su JSON y copia la versión válida más reciente al nombre principal.

## Dependencias y licencias

- AngleSharp 1.5.2 — MIT.
- Microsoft.Playwright 1.61.0 — Apache-2.0.
- PdfPig 0.1.15 — Apache-2.0.
- Leaflet 1.9.4 — BSD-2-Clause.
- OpenStreetMap — datos © colaboradores de OpenStreetMap; atribución visible.
- NGMEP 2026 — CC-BY 4.0; obra derivada con atribución visible al Instituto
  Geográfico Nacional.
- xUnit, bUnit y coverlet — dependencias de pruebas con licencias permisivas.

Las versiones .NET están centralizadas en `Directory.Packages.props`. Leaflet
1.9.4 se sirve desde `wwwroot/vendor/leaflet` con su licencia; las pruebas no
dependen de una CDN. Si Leaflet o las teselas no están disponibles, el listado
sigue funcionando.

## Límites actuales y siguiente fase

- El perfil predeterminado no tiene fuentes live y mantiene la baseline
  determinista. El único perfil live es manual, explícito y está limitado a una
  página.
- La interpretación de lenguaje comercial es heurística y debe ampliarse con
  fixtures cuando aparezcan formatos nuevos.
- El crawler procesa las fuentes de forma conservadora; el volumen inicial no
  requiere paralelismo: el recorrido es secuencial y no expone ajustes de
  concurrencia que no aplique.
- El radar cubre cuatro canales centrales y 25 municipios. Quedan cuatro
  ayuntamientos sin adaptador municipal: Collado Villalba, Guadalix de la
  Sierra, Navalafuente y Robledo de Chavela. Sus portales actuales requieren
  JavaScript o sesión, o están inactivos. Las portadas muestran únicamente los
  anuncios más recientes y no sustituyen al tablón histórico bloqueado.
- BOCM dispone de backfill oficial por calendario y sumario XML. PCSP debe
  revalidarse antes de operación porque su WAF devolvió HTML con HTTP 200 en el
  último smoke; el fallo queda aislado y no se intenta evadir.
- `.github/workflows/ci.yml` reproduce la baseline offline en cada push y pull
  request. `.github/workflows/crawl-and-deploy.yml` se puede lanzar
  manualmente y está programado cada día a las 06:17, zona
  `Europe/Madrid`; usa las ocho fuentes live revisadas y no versiona el estado.
- `scripts/prepare-pages.ps1` prepara el subpath `/SierraNueva/`, `.nojekyll` y
  `404.html`, y rechaza cualquier `data/state` en el artefacto.
- GitHub Pages está activo en `https://javiig13.github.io/SierraNueva/`. El
  despliegue manual real terminó correctamente con ocho promociones de ocho
  fuentes, cero fallos y sin publicar `data/state`; también se comprobó una URL
  directa con filtro (`?q=Galapagar`).

## Crawling responsable

SierraNueva se identifica, respeta `robots.txt`, limita frecuencia y tamaño,
no persiste cookies, no republica HTML completo ni imágenes y no sortea
protecciones. El radar puede conservar en memoria una cookie pública de sesión
durante las redirecciones de una sede, pero nunca la escribe en disco. Que una
página sea técnicamente accesible no implica permiso para reutilizarla: revisa
siempre las condiciones de la fuente.
