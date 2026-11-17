// Tablero de un desastre: pide /tablero al portal, que lo reenvía al servicio de tablero (servlet
// asíncrono multihilo), y dibuja en el mapa el área del desastre, sus evaluaciones de daño y sus
// proyectos de reconstrucción, con el aviso de las fuentes que no respondieron.
(function () {
  const caja = document.getElementById('tablero');
  if (!caja) return;
  const ref = caja.dataset.ref;
  const lat = parseFloat(caja.dataset.lat), lon = parseFloat(caja.dataset.lon), radio = parseFloat(caja.dataset.radio);
  const COLOR_FASE = { REGISTRO: '#6b7280', CARACTERIZACION: '#d97706', RECONSTRUCCION: '#2563eb', CIERRE: '#059669' };
  const COLOR_NIVEL = { 1: '#16a34a', 2: '#84cc16', 3: '#eab308', 4: '#f97316', 5: '#dc2626' };

  const mapa = L.map('mapa').setView(isNaN(lat) ? [4.6, -74.1] : [lat, lon], isNaN(lat) ? 6 : 12);
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19, attribution: '&copy; OpenStreetMap'
  }).addTo(mapa);
  const capas = { 'Área del desastre': L.layerGroup().addTo(mapa), 'Evaluaciones de daño': L.layerGroup().addTo(mapa),
    'Proyectos de reconstrucción': L.layerGroup().addTo(mapa) };
  L.control.layers(null, capas, { collapsed: false }).addTo(mapa);
  const limites = [];
  if (!isNaN(lat) && !isNaN(radio)) {
    const area = L.circle([lat, lon], { radius: radio * 1000, color: COLOR_FASE[caja.dataset.fase] || '#6b7280', fillOpacity: 0.08 })
      .bindPopup('Área afectada: radio ' + radio + ' km').addTo(capas['Área del desastre']);
    limites.push(area.getBounds());
  }

  const texto = (s) => String(s == null ? '' : s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  fetch('/tablero?desastre=' + encodeURIComponent(ref), { headers: { Accept: 'application/json' } })
    .then((r) => r.json().then((cuerpo) => ({ estado: r.status, cuerpo })))
    .then(({ estado, cuerpo }) => {
      if (estado !== 200) throw new Error(cuerpo.error || ('HTTP ' + estado));
      pintarFuentes(cuerpo);
      const puntos = pintarEvaluaciones(cuerpo.evaluaciones);
      pintarProyectos(cuerpo.reconstruccion, puntos);
      if (limites.length) mapa.fitBounds(limites.reduce((a, b) => a.extend(b)), { maxZoom: 14, padding: [20, 20] });
    })
    .catch((e) => {
      document.getElementById('resumen-evaluaciones').textContent = 'No se pudo cargar el tablero: ' + e.message;
      document.getElementById('resumen-reconstruccion').textContent = '';
    });

  function pintarFuentes(t) {
    const cuerpo = document.querySelector('#fuentes tbody');
    const nombres = { desastre: 'Desastre (núcleo)', evaluaciones: 'Evaluaciones (servidor)', reconstruccion: 'Proyectos (Reconstrucción, gRPC)' };
    cuerpo.innerHTML = Object.entries(t.fuentes).map(([k, f]) =>
      '<tr><td>' + texto(nombres[k] || k) + '</td><td class="' + (f.estado === 'ok' ? 'bien' : 'mal') + '">' + texto(f.estado) +
      '</td><td>' + f.ms + ' ms</td><td class="tenue">' + texto(f.hilo || '—') + '</td></tr>').join('');
    document.getElementById('total-tablero').textContent = 'Modo ' + t.modo + ': ' + t.totalMs + ' ms en total' +
      (t.fuentesNoDisponibles.length ? ' · sin: ' + t.fuentesNoDisponibles.join(', ') : '');
  }

  function pintarEvaluaciones(ev) {
    const resumen = document.getElementById('resumen-evaluaciones');
    const porId = {};
    if (!ev) { resumen.textContent = 'Evaluaciones no disponibles en este momento.'; return porId; }
    resumen.innerHTML = '<strong>' + ev.total + '</strong> evaluaciones · por nivel: ' +
      (Object.entries(ev.porNivel).map(([n, c]) => 'nivel ' + texto(n) + ': ' + c).join(', ') || '—') +
      '<br>Por activo: ' + (Object.entries(ev.porActivo).map(([a, c]) => texto(a) + ' ' + c).join(', ') || '—');
    ev.puntos.forEach((p) => {
      porId[p.id] = p;
      const m = L.circleMarker([p.latitud, p.longitud], { radius: 7, color: '#111827', weight: 1,
        fillColor: COLOR_NIVEL[p.nivel] || '#9ca3af', fillOpacity: 0.9 })
        .bindPopup('<strong>' + texto(p.activo || 'Activo') + '</strong><br>Nivel de afectación ' + texto(p.nivel) + ' de 5<br>' +
          texto(p.descripcion) + '<br><a href="/evaluaciones/' + encodeURIComponent(p.id) + '">Ver evaluación</a>')
        .addTo(capas['Evaluaciones de daño']);
      limites.push(L.latLngBounds([m.getLatLng(), m.getLatLng()]));
    });
    return porId;
  }

  function pintarProyectos(rec, puntos) {
    const resumen = document.getElementById('resumen-reconstruccion');
    if (!rec) { resumen.textContent = 'Reconstrucción no respondió; el resto del tablero sigue disponible.'; return; }
    resumen.innerHTML = '<strong>' + rec.proyectos.length + '</strong> proyectos · avance ponderado <strong>' +
      rec.avancePonderado + ' %</strong> · con sobrecosto: ' + rec.conSobrecosto;
    rec.proyectos.forEach((p) => {
      const ubicados = (p.evaluaciones || []).map((id) => puntos[id]).filter(Boolean);
      if (!ubicados.length) return;
      const centro = [ubicados.reduce((s, x) => s + x.latitud, 0) / ubicados.length, ubicados.reduce((s, x) => s + x.longitud, 0) / ubicados.length];
      L.marker(centro, { title: p.nombre })
        .bindPopup('<strong>' + texto(p.nombre) + '</strong><br>' + texto(p.tipoActivo) + ' · ' + texto(p.estado) + ' · ' + p.avance + ' %' +
          (p.sobrecosto ? '<br><span class="mal">Sobrecosto</span>' : '') +
          '<br><a href="/reconstruccion/' + encodeURIComponent(ref) + '/proyectos/' + encodeURIComponent(p.id) + '">Ver proyecto</a>')
        .addTo(capas['Proyectos de reconstrucción']);
    });
  }
})();
