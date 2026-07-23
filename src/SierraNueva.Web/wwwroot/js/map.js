(function () {
  const maps = new WeakMap();

  function formatFullPrice(value) {
    return new Intl.NumberFormat("es-ES", {
      style: "currency",
      currency: "EUR",
      maximumFractionDigits: 0
    }).format(value);
  }

  function formatMarkerPrice(value) {
    if (!value) return "Ver";
    if (value >= 1000000) {
      return `${new Intl.NumberFormat("es-ES", {
        maximumFractionDigits: 1
      }).format(value / 1000000)} M €`;
    }
    return `${Math.round(value / 1000)} mil €`;
  }

  function createPopup(feature, dotNetReference) {
    const properties = feature.properties;
    const container = document.createElement("div");
    container.className = "map-popup";

    const place = document.createElement("span");
    place.className = "map-popup-place";
    place.textContent = properties.municipality || "";
    container.appendChild(place);

    const title = document.createElement("strong");
    title.textContent = properties.name || "Promoción";
    container.appendChild(title);

    if (properties.priceFrom) {
      const price = document.createElement("span");
      price.className = "map-popup-price";
      price.textContent = `Desde ${formatFullPrice(properties.priceFrom)}`;
      container.appendChild(price);
    }

    const detail = document.createElement("button");
    detail.type = "button";
    detail.className = "map-detail-button";
    detail.textContent = "Ver ficha completa →";
    detail.addEventListener("click", () => {
      dotNetReference.invokeMethodAsync("OpenPromotion", feature.id);
    });
    container.appendChild(detail);
    return container;
  }

  function precisionClass(precision) {
    if (precision === "ExactCoordinates" || precision === "exactCoordinates") {
      return "is-exact";
    }
    if (precision === "MunicipalityCentroid" || precision === "municipalityCentroid") {
      return "is-centroid";
    }
    return "is-approximate";
  }

  function markerIcon(feature) {
    const properties = feature.properties || {};
    const label = formatMarkerPrice(properties.priceFrom);
    return L.divIcon({
      className: `price-marker ${precisionClass(properties.locationPrecision)}`,
      html: `<span>${label}</span><i></i>`,
      iconSize: null,
      iconAnchor: [38, 38],
      popupAnchor: [0, -38]
    });
  }

  function setMarkerHighlight(marker, active) {
    if (!marker) return;
    const element = marker.getElement();
    if (element) {
      element.classList.toggle("is-active", active);
    }
    marker.setZIndexOffset(active ? 1000 : 0);
  }

  function applyHighlight(state, promotionId) {
    for (const [id, marker] of state.markers) {
      setMarkerHighlight(marker, id === promotionId);
    }
    state.highlightedId = promotionId || null;
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
          zoomControl: false,
          scrollWheelZoom: false,
          preferCanvas: false
        }).setView([40.68, -3.9], 10);
        L.control.zoom({ position: "bottomright" }).addTo(map);
        L.tileLayer(tileUrl, {
          attribution,
          maxZoom: 18,
          updateWhenIdle: true
        }).addTo(map);
        state = {
          map,
          layer: null,
          markers: new Map(),
          highlightedId: null,
          cardOver: event => {
            const card = event.target.closest?.("[data-promotion-id]");
            if (card) {
              applyHighlight(state, card.dataset.promotionId);
            }
          },
          cardOut: event => {
            const card = event.target.closest?.("[data-promotion-id]");
            if (card && !card.contains(event.relatedTarget)) {
              applyHighlight(state, null);
            }
          }
        };
        document.addEventListener("pointerover", state.cardOver);
        document.addEventListener("pointerout", state.cardOut);
        maps.set(element, state);
      }

      if (state.layer) {
        state.map.removeLayer(state.layer);
      }
      state.markers.clear();

      const allowed = new Set(visibleIds || []);
      const filtered = {
        type: "FeatureCollection",
        features: (geoJson.features || []).filter(feature => allowed.has(feature.id))
      };
      element.dataset.featureCount = String(filtered.features.length);
      state.layer = L.geoJSON(filtered, {
        pointToLayer(feature, latlng) {
          return L.marker(latlng, {
            icon: markerIcon(feature),
            title: feature.properties?.name || "Promoción",
            riseOnHover: true
          });
        },
        onEachFeature(feature, layer) {
          state.markers.set(feature.id, layer);
          layer.bindPopup(createPopup(feature, dotNetReference), {
            closeButton: false,
            offset: [0, -2],
            className: "sierra-popup"
          });
          layer.on("mouseover", () => {
            dotNetReference.invokeMethodAsync("HighlightPromotion", feature.id);
          });
          layer.on("mouseout", () => {
            dotNetReference.invokeMethodAsync("HighlightPromotion", null);
          });
        }
      }).addTo(state.map);

      const bounds = state.layer.getBounds();
      if (bounds.isValid()) {
        state.map.fitBounds(bounds.pad(0.2), {
          maxZoom: 13,
          paddingTopLeft: [42, 90],
          paddingBottomRight: [42, 42]
        });
      }
      applyHighlight(state, state.highlightedId);
      setTimeout(() => state.map.invalidateSize(), 0);
    },

    highlight(element, promotionId) {
      const state = maps.get(element);
      if (state) {
        applyHighlight(state, promotionId);
      }
    },

    dispose(element) {
      const state = maps.get(element);
      if (state) {
        document.removeEventListener("pointerover", state.cardOver);
        document.removeEventListener("pointerout", state.cardOut);
        state.map.remove();
        maps.delete(element);
      }
      delete element.dataset.featureCount;
    }
  };
})();
