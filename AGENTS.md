# Guía de trabajo para agentes

Este archivo es la puerta de entrada para cualquier agente de IA que continúe
SierraNueva. El repositorio debe poder entenderse sin acceso a la conversación
en la que se creó.

## Lectura obligatoria

Antes de modificar código, lee en este orden:

1. `docs/HANDOFF.md`: estado real, decisiones y limitaciones conocidas.
2. `docs/PROJECT_BRIEF.md`: alcance funcional y restricciones del producto.
3. `docs/ROADMAP.md`: criterios de aceptación y trabajo pendiente priorizado.
4. `README.md`: comandos y uso local.
5. `docs/architecture.md`: fronteras técnicas y flujo de datos.

Si un documento contradice una petición nueva del propietario, prevalece la
petición nueva. Si hay una discrepancia entre documentación e implementación,
comprueba el código y corrige después la documentación.

## Estado de la fase

La fase actual es **producto local y repositorio**. El propietario ha aplazado
expresamente GitHub, Actions, Pages, hosting y demás infraestructura hasta el
final. No añadas todavía workflows, remotos, despliegues, secretos, `base href`
de un repositorio provisional ni servicios cloud, salvo petición expresa.

El punto de partida funcional usa únicamente `fixtures-locales`. No habilites
una fuente real por comodidad: cada fuente debe verificarse jurídica y
técnicamente antes de incorporarla.

## Invariantes del producto

- .NET 10, C# con nullable y advertencias como errores.
- Un crawler/ETL de consola y una única SPA Blazor WebAssembly standalone.
- Frontend estático, sin backend, autenticación ni base de datos remota.
- Contrato público versionado en `data/public`; estado privado en `data/state`.
- Leaflet y OpenStreetMap; no Google Maps, Mapbox ni Bing Maps.
- `HttpClient` y HTML estático primero; Playwright solo como fallback explícito.
- Sin APIs de pago ni claves obligatorias.
- Sin grandes portales inmobiliarios, CAPTCHA, áreas privadas, evasión
  anti-bot, imágenes republicadas ni HTML completo de terceros.
- No inventar datos. Lo desconocido permanece nulo y toda aproximación debe
  etiquetarse.
- Un fallo total no puede sustituir el último dataset válido por uno vacío.

## Comandos de control

Ejecuta desde la raíz:

```powershell
dotnet restore SierraNueva.sln
dotnet build SierraNueva.sln -c Release --no-restore
dotnet test SierraNueva.sln -c Release --no-build
dotnet format SierraNueva.sln --verify-no-changes --no-restore
dotnet run --project src/SierraNueva.Crawler -c Release --no-build -- validate-config
dotnet run --project src/SierraNueva.Crawler -c Release --no-build -- crawl --no-playwright
dotnet run --project src/SierraNueva.Crawler -c Release --no-build -- validate-data
```

Para una comprobación del artefacto estático:

```powershell
dotnet publish src/SierraNueva.Web/SierraNueva.Web.csproj -c Release --no-restore
```

Los scripts `scripts/run-local.ps1` y `scripts/run-local.sh` ofrecen el recorrido
completo. Las pruebas automáticas no deben depender de Internet ni de webs
live.

## Reglas para cambios

- Mantén las dependencias en `Directory.Packages.props`.
- Conserva las fronteras Contracts → Core → Infrastructure/Crawler y
  Contracts → Web. Core no conoce librerías de infraestructura.
- Añade una fixture y una prueba por cada formato nuevo de extracción.
- Los selectores y fuentes específicos viven en configuración o en adaptadores
  aislados, nunca mezclados con las reglas generales.
- No publiques `data/state` dentro de la salida web.
- Evita cambios ruidosos en los JSON: orden estable, UTC y escritura atómica.
- No borres el dataset o el estado para “arreglar” una prueba.
- No confirmes cobertura live, Playwright, Nominatim o navegador real si solo se
  han probado dobles o fixtures.
- Actualiza `docs/HANDOFF.md` y `docs/ROADMAP.md` cuando cambie el estado real.

## Definición de terminado

Un cambio queda terminado cuando compila sin advertencias, pasan las pruebas
relevantes, el formato es válido, los contratos públicos siguen siendo
compatibles o su versión se ha actualizado deliberadamente, y la documentación
describe tanto lo hecho como lo que aún no se ha verificado.
