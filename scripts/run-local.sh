#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

skip_playwright=false
no_frontend=false
verbose_crawler=false

for argument in "$@"; do
  case "$argument" in
    --skip-playwright-install) skip_playwright=true ;;
    --no-frontend) no_frontend=true ;;
    --verbose) verbose_crawler=true ;;
    *)
      echo "Opción desconocida: $argument" >&2
      exit 2
      ;;
  esac
done

dotnet restore SierraNueva.sln
dotnet build SierraNueva.sln -c Release --no-restore

if [[ "$skip_playwright" == false ]]; then
  browser_found=false
  for cache_root in "$HOME/.cache/ms-playwright" "$HOME/Library/Caches/ms-playwright"; do
    if compgen -G "$cache_root/chromium-*" >/dev/null; then
      browser_found=true
      break
    fi
  done

  if [[ "$browser_found" == false ]]; then
    playwright_script="$(find "$repo_root" -path '*/bin/Release/net10.0/playwright.sh' -print -quit)"
    if [[ -z "$playwright_script" ]]; then
      echo "No se encontró playwright.sh después de compilar." >&2
      exit 2
    fi
    bash "$playwright_script" install chromium
  fi
fi

dotnet test SierraNueva.sln -c Release --no-build
dotnet format SierraNueva.sln --verify-no-changes --no-restore
dotnet run --project src/SierraNueva.Crawler -c Release --no-build -- validate-config
dotnet run --project src/SierraNueva.Crawler -c Release --no-build -- \
  discover-opportunities --dry-run

crawler_args=(
  run --project src/SierraNueva.Crawler -c Release --no-build --
  crawl --no-playwright
)
if [[ "$verbose_crawler" == true ]]; then
  crawler_args+=(--verbose)
fi

set +e
dotnet "${crawler_args[@]}"
crawler_exit=$?
set -e
if (( crawler_exit > 1 )); then
  exit "$crawler_exit"
fi

dotnet run --project src/SierraNueva.Crawler -c Release --no-build -- validate-data
dotnet publish src/SierraNueva.Web/SierraNueva.Web.csproj -c Release --no-restore

if [[ "$no_frontend" == false ]]; then
  echo "SierraNueva estará disponible en la URL que indique el servidor."
  dotnet run --project src/SierraNueva.Web -c Release --no-build
fi
