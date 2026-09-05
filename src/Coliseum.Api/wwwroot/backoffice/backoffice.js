// Coliseum back-office. Sources: /metrics (Prometheus text, parsed here for RED), /admin/stats (economy + queue),
// /leaderboard and /battles/{id} (REST), and the SignalR hub for the live feed (JoinBackOffice, service token).
(() => {
  const $ = (id) => document.getElementById(id);
  const POLL_MS = 5000;
  const state = { token: null, connection: null, prevMetrics: null, series: { t: [], rate: [], p95: [], submitted: [], processed: [] }, battles: new Map(), names: {} };
  let redChart, queueChart, bucketChart;

  // ---------- auth ----------
  async function signIn() {
    $('loginError').hidden = true;
    try {
      const res = await fetch('/auth/token', { method: 'POST', headers: { 'X-Api-Key': $('apiKey').value.trim() } });
      if (!res.ok) throw new Error('API key rejected');
      const { accessToken } = await res.json();
      sessionStorage.setItem('coliseum.backoffice', accessToken);
      await boot(accessToken);
    } catch (err) {
      $('loginError').textContent = err.message;
      $('loginError').hidden = false;
    }
  }
  function signOut() { sessionStorage.removeItem('coliseum.backoffice'); location.reload(); }

  async function api(path) {
    const res = await fetch(path, { headers: { Authorization: 'Bearer ' + state.token } });
    if (res.status === 401) { signOut(); return null; }
    if (!res.ok) throw new Error(`${res.status} on ${path}`);
    return res.json();
  }

  // ---------- Prometheus text parsing (RED from the API's own /metrics) ----------
  async function scrapeMetrics() {
    const text = await (await fetch('/metrics')).text();
    const m = { count: 0, err: 0, sum: 0, buckets: new Map(), submitted: 0, ts: Date.now() };
    for (const line of text.split('\n')) {
      if (line.startsWith('#') || !line) continue;
      const sp = line.lastIndexOf(' ');
      const name = line.slice(0, sp), value = parseFloat(line.slice(sp + 1));
      if (name.startsWith('http_server_request_duration_seconds_count')) {
        m.count += value;
        if (/http_response_status_code="5\d\d"/.test(name)) m.err += value;
      } else if (name.startsWith('http_server_request_duration_seconds_sum')) {
        m.sum += value;
      } else if (name.startsWith('http_server_request_duration_seconds_bucket')) {
        const le = /le="([^"]+)"/.exec(name)?.[1];
        if (le) m.buckets.set(le, (m.buckets.get(le) || 0) + value);
      } else if (name.startsWith('coliseum_battles_submitted_total')) {
        m.submitted += value;
      }
    }
    return m;
  }

  function quantile(prev, cur, q) {
    const les = [...cur.buckets.keys()].map(Number).sort((a, b) => a - b);
    const total = cur.count - prev.count;
    if (total <= 0) return null;
    const target = q * total;
    let lower = 0, lowerLe = 0;
    for (const le of les) {
      const c = (cur.buckets.get(String(le)) ?? cur.buckets.get(le === Infinity ? '+Inf' : String(le)) ?? 0) - (prev.buckets.get(String(le)) ?? 0);
      if (c >= target) {
        const frac = c === lower ? 0 : (target - lower) / (c - lower);
        return le === Infinity ? lowerLe : lowerLe + (le - lowerLe) * frac;
      }
      lower = c; lowerLe = le;
    }
    return null;
  }

  // ---------- refresh loop ----------
  async function refresh() {
    try {
      const [metrics, stats] = await Promise.all([scrapeMetrics(), api('/admin/stats')]);
      if (!stats) return;
      const prev = state.prevMetrics;
      if (prev) {
        const dt = (metrics.ts - prev.ts) / 1000;
        const rate = (metrics.count - prev.count) / dt;
        const errRatio = metrics.count - prev.count > 0 ? (metrics.err - prev.err) / (metrics.count - prev.count) : 0;
        const p95 = quantile(prev, metrics, 0.95), p50 = quantile(prev, metrics, 0.5);
        $('tRate').textContent = rate.toFixed(1);
        $('tRateSub').textContent = `${(metrics.count - prev.count)} requests in ${dt.toFixed(0)} s`;
        setTile('tErr', (errRatio * 100).toFixed(2) + ' %', errRatio > 0.01 ? 'bad' : errRatio > 0 ? 'warn' : 'ok');
        setTile('tP95', p95 == null ? '–' : fmtMs(p95), p95 > 0.25 ? 'warn' : 'ok');
        $('tP50').textContent = p50 == null ? '' : 'p50 ' + fmtMs(p50);
        push('t', new Date().toLocaleTimeString()); push('rate', +rate.toFixed(2)); push('p95', p95 == null ? null : +(p95 * 1000).toFixed(1));
        push('submitted', +((metrics.submitted - prev.submitted) / dt * 60).toFixed(1));
        push('processed', state.lastProcessed == null ? 0 : +((stats.economy.battlesProcessed - state.lastProcessed) / dt * 60).toFixed(1));
        redChart.update(); queueChart.update();
      }
      state.prevMetrics = metrics;
      state.lastProcessed = stats.economy.battlesProcessed;

      const q = stats.queue;
      setTile('tQueue', `${q.length} / ${q.pending} / ${q.deadLettered}`, q.deadLettered > 0 ? 'bad' : q.pending > 50 ? 'warn' : 'ok');
      $('tProcessed').textContent = stats.economy.battlesProcessed;
      $('tTurns').textContent = `avg ${stats.economy.averageTurns} turns`;
      const win = stats.economy.attackerWinRate;
      setTile('tWin', (win * 100).toFixed(1) + ' %', Math.abs(win - 0.72) > 0.15 && stats.economy.battlesProcessed > 20 ? 'warn' : 'ok');
      $('economy').innerHTML = `<span>gold <b>${stats.economy.goldStolen.toLocaleString()}</b></span><span>silver <b>${stats.economy.silverStolen.toLocaleString()}</b></span><span>attacker wins <b>${stats.economy.attackerWins}</b></span>`;
      bucketChart.data.datasets[0].data = Object.values(stats.economy.turnBuckets);
      bucketChart.update();
      renderBoard(stats.top);
      $('clock').textContent = 'updated ' + new Date(stats.generatedAt).toLocaleTimeString();
    } catch (err) {
      $('clock').textContent = 'refresh failed: ' + err.message;
    }
  }
  function push(key, v) { const s = state.series[key]; s.push(v); if (s.length > 60) s.shift(); }
  function setTile(id, text, cls) { const el = $(id); el.textContent = text; el.parentElement.className = 'tile ' + (cls || ''); }
  function fmtMs(s) { return (s * 1000).toFixed(0) + ' ms'; }
  function name(id) { return state.names[id] || (id ? id.slice(0, 8) : '–'); }

  function renderBoard(entries) {
    $('board').innerHTML = entries.map((e) => `<tr><td>${e.rank}</td><td>${name(e.playerId)}</td><td>${e.score.toLocaleString()}</td></tr>`).join('') || '<tr><td colspan="3" class="muted">no battles settled yet</td></tr>';
  }

  // ---------- charts ----------
  function charts() {
    const base = { responsive: true, animation: false, plugins: { legend: { labels: { color: '#8b93a1' } } }, scales: { x: { ticks: { color: '#8b93a1', maxTicksLimit: 8 }, grid: { color: '#262c36' } }, y: { ticks: { color: '#8b93a1' }, grid: { color: '#262c36' }, beginAtZero: true } } };
    redChart = new Chart($('chartRed'), { type: 'line', data: { labels: state.series.t, datasets: [
      { label: 'req/s', data: state.series.rate, borderColor: '#f2b134', tension: .3, yAxisID: 'y' },
      { label: 'p95 ms', data: state.series.p95, borderColor: '#58a6ff', tension: .3, yAxisID: 'y1' }] },
      options: { ...base, scales: { ...base.scales, y1: { position: 'right', ticks: { color: '#58a6ff' }, grid: { drawOnChartArea: false }, beginAtZero: true } } } });
    queueChart = new Chart($('chartQueue'), { type: 'line', data: { labels: state.series.t, datasets: [
      { label: 'submitted / min', data: state.series.submitted, borderColor: '#f2b134', tension: .3 },
      { label: 'settled / min', data: state.series.processed, borderColor: '#3fb950', tension: .3 }] }, options: base });
    bucketChart = new Chart($('chartBuckets'), { type: 'bar', data: { labels: ['1-5', '6-10', '11-20', '21-50', '51+'], datasets: [{ label: 'battles by turns', data: [0, 0, 0, 0, 0], backgroundColor: '#58a6ff' }] }, options: base });
  }

  // ---------- live feed ----------
  function upsertBattle(id, patch) {
    const b = { id, attackerId: '', defenderId: '', status: 'queued', turns: 0, winnerId: '', score: '', ...(state.battles.get(id) || {}), ...patch };
    state.battles.set(id, b);
    if (state.battles.size > 40) state.battles.delete(state.battles.keys().next().value);
    renderFeed();
  }
  function renderFeed() {
    const rows = [...state.battles.values()].reverse();
    $('battles').innerHTML = rows.map((b) => `<tr data-id="${b.id}" class="${b.status}"><td>${b.id.slice(0, 10)}</td><td>${name(b.attackerId)}</td><td>${name(b.defenderId)}</td><td>${b.status}</td><td>${b.turns || ''}</td><td>${b.winnerId ? name(b.winnerId) : ''}</td><td>${b.score}</td></tr>`).join('');
  }
  function onEvent(json) {
    const e = JSON.parse(json);
    switch (e.type) {
      case 'battle.queued': upsertBattle(e.battleId, { attackerId: e.attackerId, defenderId: e.defenderId, status: 'queued' }); break;
      case 'battle.turn': upsertBattle(e.battleId, { status: 'processing', turns: e.turn }); break;
      case 'battle.done': upsertBattle(e.battleId, { attackerId: e.attackerId, defenderId: e.defenderId, status: 'done', turns: e.turns, winnerId: e.winnerId, score: e.score }); break;
      case 'battle.failed': upsertBattle(e.battleId, { status: 'failed', score: e.error }); break;
      case 'leaderboard.changed': renderBoard(e.top); break;
      default: break;
    }
  }
  async function openReport(id) {
    const r = await api('/battles/' + id);
    if (!r) return;
    $('reportTitle').textContent = `${name(r.attackerId)} vs ${name(r.defenderId)} · ${r.status}`;
    $('reportMeta').textContent = r.status === 'done'
      ? `battle ${r.battleId}\nseed ${r.seed}  (replay: MCP simulate_battle with this seed and the players' stats)\nwinner ${name(r.winnerId)} · ${r.turns} turns · loot ${r.loot.gold} gold + ${r.loot.silver} silver (${r.loot.percent}%) · score ${r.loot.score}`
      : `battle ${r.battleId}\nstatus ${r.status}${r.error ? ' · ' + r.error : ''}`;
    $('reportNarrative').innerHTML = (r.narrative || []).map((l) => `<li>${l}</li>`).join('');
    $('report').showModal();
  }

  async function connect() {
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/arena', { accessTokenFactory: () => state.token }).withAutomaticReconnect().build();
    connection.on('arenaEvent', onEvent);
    connection.onreconnecting(() => setFeed('feed reconnecting', false));
    connection.onreconnected(() => { setFeed('feed online', true); connection.invoke('JoinBackOffice'); });
    connection.onclose(() => setFeed('feed offline', false));
    await connection.start();
    await connection.invoke('JoinBackOffice');
    state.connection = connection;
    setFeed('feed online', true);
  }
  function setFeed(text, ok) { const p = $('feed'); p.textContent = text; p.classList.toggle('online', ok); }

  async function loadNames() {
    const players = await api('/players?limit=100');
    (players || []).forEach((p) => { state.names[p.id] = p.name; });
  }

  // ---------- boot ----------
  async function boot(token) {
    state.token = token;
    $('login').hidden = true; $('dash').hidden = false; $('signout').hidden = false;
    charts();
    await loadNames();
    await refresh();
    setInterval(refresh, POLL_MS);
    setInterval(loadNames, POLL_MS * 6);
    await connect();
  }

  $('signin').addEventListener('click', signIn);
  $('signout').addEventListener('click', signOut);
  $('closeReport').addEventListener('click', () => $('report').close());
  $('battles').addEventListener('click', (ev) => { const id = ev.target.closest('tr')?.dataset.id; if (id) openReport(id); });
  const saved = sessionStorage.getItem('coliseum.backoffice');
  if (saved) boot(saved).catch(signOut);
})();
