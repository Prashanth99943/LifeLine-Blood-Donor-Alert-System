// Two jobs: (1) keep the dashboards live through SignalR, (2) draw maps.
(function () {
  const $ = (sel) => document.querySelector(sel);

  // Small pop-up message in the corner
  window.toast = function (title, body, kind) {
    const box = $('#toasts');
    if (!box) return;
    const t = document.createElement('div');
    t.className = 'toast-lite ' + (kind || '');
    const h = document.createElement('strong'); h.textContent = title;
    const p = document.createElement('div'); p.className = 'muted small'; p.textContent = body;
    t.append(h, p);
    box.appendChild(t);
    setTimeout(() => t.remove(), 7000);
  };

  function beep() {
    try {
      const ctx = new (window.AudioContext || window.webkitAudioContext)();
      const o = ctx.createOscillator(), g = ctx.createGain();
      o.frequency.value = 880; g.gain.value = 0.05;
      o.connect(g); g.connect(ctx.destination); o.start();
      setTimeout(() => { o.stop(); ctx.close(); }, 180);
    } catch (e) { /* sound is optional */ }
  }

  // Each dashboard has <div id="live" data-src="...">. When the server says "something changed",
  // we fetch that URL (it returns only an HTML fragment) and swap it in. No full page reload.
  function refreshLive() {
    const el = $('#live');
    if (!el || !el.dataset.src) return;
    fetch(el.dataset.src, { credentials: 'same-origin' })
      .then(r => (r.ok ? r.text() : null))
      .then(html => { if (html) el.innerHTML = html; });
  }

  function setLive(on) {
    const d = $('#live-dot');
    if (!d) return;
    d.classList.toggle('on', on);
    d.querySelector('em').textContent = on ? 'Live' : 'Offline';
  }

  if (document.body.dataset.auth === '1' && window.signalR) {
    const conn = new signalR.HubConnectionBuilder().withUrl('/hubs/alerts').withAutomaticReconnect().build();
    conn.on('NewAlert', m => { toast(m.title, m.body, 'alert'); beep(); refreshLive(); });   // sent to matched donors
    conn.on('RequestUpdated', m => { toast(m.title, m.body); refreshLive(); });               // sent to hospital / admin / donors
    conn.onreconnecting(() => setLive(false));
    conn.onreconnected(() => { setLive(true); refreshLive(); });
    conn.onclose(() => setLive(false));
    conn.start().then(() => setLive(true)).catch(() => setLive(false));
  }

  // markers: [{lat, lon, label, kind}], kind = hospital | donor | accepted
  window.drawMap = function (id, markers, radiusKm) {
    const map = L.map(id, { scrollWheelZoom: false });
    L.tileLayer('https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png', {
      attribution: '&copy; OpenStreetMap contributors &copy; CARTO', maxZoom: 18
    }).addTo(map);

    const colors = { hospital: '#e5484d', donor: '#5aa9e6', accepted: '#38b27b' };
    const pts = [];
    markers.forEach(m => {
      pts.push([m.lat, m.lon]);
      L.circleMarker([m.lat, m.lon], {
        radius: m.kind === 'hospital' ? 10 : 6, color: '#0f1720', weight: 2,
        fillColor: colors[m.kind] || '#8fa0b2', fillOpacity: 1
      }).addTo(map).bindTooltip(m.label);
    });

    const h = markers.find(m => m.kind === 'hospital');
    if (h && radiusKm) {
      L.circle([h.lat, h.lon], { radius: radiusKm * 1000, color: '#e5484d', weight: 1, dashArray: '4 6', fillOpacity: 0.04 }).addTo(map);
    }
    if (pts.length) map.fitBounds(pts, { padding: [40, 40], maxZoom: 11 }); else map.setView([17.0, 79.0], 6);
  };
})();
