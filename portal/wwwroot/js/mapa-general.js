// Mapa general: el área afectada de cada desastre caracterizado, con el color de su fase.
(function () {
  const COLOR_FASE = { REGISTRO: '#6b7280', CARACTERIZACION: '#d97706', RECONSTRUCCION: '#2563eb', CIERRE: '#059669' };
  const texto = (s) => String(s == null ? '' : s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const mapa = L.map('mapa-general').setView([4.6, -74.1], 6);
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OpenStreetMap' }).addTo(mapa);
  const desastres = window.DESASTRES || [];
  const limites = [];
  let sinUbicacion = 0;
  desastres.forEach((d) => {
    if (d.latitud == null || d.longitud == null || d.radioKm == null) { sinUbicacion++; return; }
    const area = L.circle([d.latitud, d.longitud], { radius: d.radioKm * 1000, color: COLOR_FASE[d.fase] || '#6b7280', fillOpacity: 0.15 })
      .bindPopup('<strong>' + texto(d.tipo) + ' · ' + texto(d.ubicacion) + '</strong><br>Fase ' + texto(d.fase) + ' · nivel ' + texto(d.severidad) +
        '<br>Radio ' + texto(d.radioKm) + ' km<br><a href="/desastres/' + encodeURIComponent(d.id) + '">Abrir tablero</a>')
      .addTo(mapa);
    limites.push(area.getBounds());
  });
  if (limites.length) mapa.fitBounds(limites.reduce((a, b) => a.extend(b)), { maxZoom: 12, padding: [20, 20] });
  document.getElementById('sin-ubicacion').textContent = sinUbicacion
    ? sinUbicacion + ' desastre(s) aún sin caracterizar no aparecen en el mapa.' : '';
})();
