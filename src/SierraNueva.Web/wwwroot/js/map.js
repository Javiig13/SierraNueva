(function () {
  const maps = new WeakMap();

  function createPopup(feature, dotNetReference) {
    const properties = feature.properties;
    const container = document.createElement("div");
    container.className = "map-popup";

    const title = document.createElement("strong");
    title.textContent = properties.name || "Promoción";
    container.appendChild(title);

    const place = document.createElement("span");
    place.textContent = properties.municipality || "";
    container.appendChild(place);

    if (properties.priceFrom) {
      const price = document.createElement("span");
      price.textContent = `Desde ${new Intl.NumberFormat("es-ES", {
        style: "currency",
        currency: "EUR",
        maximumFractionDigits: 0
      }).format(properties.priceFrom)}`;
      container.appendChild(price);
    }

    const detail = document.createElement("button");
    detail.type = "button";
    detail.className = "map-detail-button";
    detail.textContent = "Ver ficha";
    detail.addEventListener("click", () => {
      dotNetReference.invokeMethodAsync("OpenPromotion", feature.id);
    });
    container.appendChild(detail);
    return container;
  }

  function markerStyle(precision) {
    if (precision === "ExactCoordinates" || precision === "exactCoordinates") {
      return { radius: 8, color: "#ffffff", weight: 2, fillColor: "#d66a3a", fillOpacity: 1 };
    }
    if (precision === "MunicipalityCentroid" || precision === "municipalityCentroid") {
      return { radius: 10, color: "#4e7568", weight: 2, dashArray: "3 3", fillColor: "#dce9e2", fillOpacity: 0.9 };
    }
    return { radius: 9, color: "#ffffff", weight: 2, fillColor: "#698f83", fillOpacity: 0.95 };
  }

  window.sierraMap = {
    render(element, geoJson, visibleIds, tileUrl, attribution, dotNetReference) {
      if (!element) return;
      if (!window.L) {
        element.classList.add("map-unavailable");
        element.textContent = "El mapa no está disponible. Puedes seguir usando el listado.";
        return;
      }

      let state = maps.get(element);
      if (!state) {
        const map = L.map(element, {
          zoomControl: true,
          scrollWheelZoom: false
        }).setView([40.68, -3.9], 10);
        L.tileLayer(tileUrl, {
          attribution,
          maxZoom: 18,
          updateWhenIdle: true
        }).addTo(map);
        state = { map, layer: null };
        maps.set(element, state);
      }

      if (state.layer) {
        state.map.removeLayer(state.layer);
      }

      const allowed = new Set(visibleIds || []);
      const filtered = {
        type: "FeatureCollection",
        features: (geoJson.features || []).filter(feature => allowed.has(feature.id))
      };
      state.layer = L.geoJSON(filtered, {
        pointToLayer(feature, latlng) {
          return L.circleMarker(latlng, markerStyle(feature.properties.locationPrecision));
        },
        onEachFeature(feature, layer) {
          layer.bindPopup(createPopup(feature, dotNetReference));
        }
      }).addTo(state.map);

      const bounds = state.layer.getBounds();
      if (bounds.isValid()) {
        state.map.fitBounds(bounds.pad(0.18), { maxZoom: 13 });
      }
      setTimeout(() => state.map.invalidateSize(), 0);
    },

    dispose(element) {
      const state = maps.get(element);
      if (state) {
        state.map.remove();
        maps.delete(element);
      }
    }
  };
})();
