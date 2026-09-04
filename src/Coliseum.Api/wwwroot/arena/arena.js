// Coliseum arena auto-play client. Talks REST to the API and listens to live events over SignalR.
// State is kept per browser tab (sessionStorage), so two tabs are two independent players.
(() => {
  const qs = new URLSearchParams(location.search);
  const $ = (id) => document.getElementById(id);
  const state = { service: null, player: null, token: null, opponents: [], connection: null, autoTimer: null, animating: Promise.resolve(), names: {} };

  // ---------- REST ----------
  async function api(path, { method = 'GET', body, token = state.token } = {}) {
    const res = await fetch(path, {
      method,
      headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) },
      body: body ? JSON.stringify(body) : undefined,
    });
    if (res.status === 204) return null;
    const data = await res.json().catch(() => null);
    if (!res.ok) {
      const errors = data?.errors?.map((e) => (e.field ? `${e.field}: ${e.message}` : e.message)).join('; ');
      throw new Error(errors || data?.title || `${res.status} ${res.statusText}`);
    }
    return data;
  }

  async function serviceToken(apiKey) {
    const res = await fetch('/auth/token', { method: 'POST', headers: { 'X-Api-Key': apiKey } });
    if (!res.ok) throw new Error('API key rejected');
    return (await res.json()).accessToken;
  }

  // ---------- setup ----------
  async function enter() {
    $('setupError').hidden = true;
    try {
      const service = await serviceToken($('apiKey').value.trim());
      const created = await api('/players', {
        method: 'POST',
        token: service,
        body: {
          name: $('name').value.trim(),
          description: 'arena auto-play',
          gold: +$('gold').value, silver: +$('silver').value,
          attack: +$('attack').value, defense: +$('defense').value, hitPoints: +$('hitPoints').value,
        },
      });
      sessionStorage.setItem('coliseum.player', JSON.stringify({ player: created.player, token: created.accessToken }));
      await boot(created.player, created.accessToken);
    } catch (err) {
      $('setupError').textContent = err.message;
      $('setupError').hidden = false;
    }
  }

  async function boot(player, token) {
    state.player = player;
    state.token = token;
    $('setup').hidden = true;
    $('arena').hidden = false;
    renderMe();
    await refreshOpponents();
    await connect();
    await refreshBoard();
    if (qs.get('auto') === '1') { $('auto').checked = true; toggleAuto(); }
  }

  // ---------- rendering ----------
  function renderFighter(prefix, p, hp) {
    $(prefix + 'Name').textContent = p ? p.name : 'choose an opponent';
    $(prefix + 'Stats').innerHTML = p ? `<span>ATK ${p.attack}</span><span>DEF ${p.defense}</span><span>HP ${p.hitPoints}</span>` : '';
    $(prefix + 'Wealth').innerHTML = p ? `<span>gold ${p.gold}</span><span>silver ${p.silver}</span>` : '';
    setHp(prefix, p ? (hp ?? p.hitPoints) : 0, p ? p.hitPoints : 1);
  }
  function setHp(prefix, hp, max) {
    const bar = $(prefix + 'Hp');
    const pct = Math.max(0, Math.min(100, (hp / max) * 100));
    bar.style.width = pct + '%';
    bar.classList.toggle('low', pct < 30);
  }
  function renderMe() { renderFighter('me', state.player); }
  function opponent() { return state.opponents.find((o) => o.id === $('opponents').value) || null; }
  function renderOpponent() { renderFighter('opp', opponent()); }

  function log(text, cls = '') {
    const li = document.createElement('li');
    li.textContent = text;
    if (cls) li.className = cls;
    const ol = $('log');
    ol.appendChild(li);
    while (ol.children.length > 200) ol.removeChild(ol.firstChild);
    ol.scrollTop = ol.scrollHeight;
  }
  function name(id) { return id === state.player.id ? state.player.name : (state.names[id] || id.slice(0, 8)); }

  async function refreshOpponents() {
    const players = await api('/players?limit=100');
    state.opponents = players.filter((p) => p.id !== state.player.id);
    players.forEach((p) => { state.names[p.id] = p.name; });
    const select = $('opponents');
    const current = select.value;
    select.innerHTML = state.opponents.map((p) => `<option value="${p.id}">${p.name} (ATK ${p.attack} / DEF ${p.defense} / HP ${p.hitPoints})</option>`).join('');
    if (state.opponents.some((p) => p.id === current)) select.value = current;
    renderOpponent();
  }

  async function refreshBoard(entries) {
    const board = entries || (await api('/leaderboard?limit=10')).entries;
    $('board').innerHTML = board.map((e) => `<tr class="${e.playerId === state.player.id ? 'me' : ''}"><td>${e.rank}</td><td>${name(e.playerId)}</td><td>${e.score}</td></tr>`).join('');
  }

  async function refreshMe() {
    state.player = await api('/players/' + state.player.id);
    renderMe();
  }

  // ---------- battles ----------
  async function fight(defenderId) {
    const id = defenderId || $('opponents').value;
    if (!id) { log('No opponent available yet: open another window to create one.', 'info'); return; }
    try {
      const accepted = await api('/battles', { method: 'POST', body: { defenderId: id } });
      log(`Challenge sent to ${name(id)} (battle ${accepted.battleId.slice(0, 8)}, ${accepted.status})`, 'info');
    } catch (err) {
      log('Could not submit: ' + err.message, 'loss');
    }
  }

  function toggleAuto() {
    clearInterval(state.autoTimer);
    state.autoTimer = null;
    if ($('auto').checked) {
      const every = Math.max(1000, +$('interval').value || 3000);
      state.autoTimer = setInterval(async () => {
        if (state.opponents.length === 0) await refreshOpponents();
        const pick = state.opponents[Math.floor(Math.random() * state.opponents.length)];
        if (pick) { $('opponents').value = pick.id; renderOpponent(); await fight(pick.id); }
      }, every);
      log(`Auto-play on: a random opponent every ${every} ms`, 'info');
    } else {
      log('Auto-play off', 'info');
    }
  }

  // Turn events arrive in a burst (the engine is microseconds); animate them one by one so the fight is watchable.
  function animate(step) { state.animating = state.animating.then(step).then(() => new Promise((r) => setTimeout(r, 350))); }

  function onEvent(json) {
    const e = JSON.parse(json);
    switch (e.type) {
      case 'battle.queued':
        if (e.defenderId === state.player.id) log(`${name(e.attackerId)} challenges you!`, 'info');
        break;
      case 'battle.turn':
        animate(() => {
          const meDefending = e.defenderId === state.player.id;
          const target = meDefending ? 'me' : 'opp';
          const max = meDefending ? state.player.hitPoints : (opponent()?.hitPoints || state.names[e.defenderId] ? 100 : 100);
          if (e.hit) {
            setHp(target, e.defenderHpAfter, meDefending ? state.player.hitPoints : (opponentMax(e.defenderId) || max));
            $(target).classList.remove('hit'); void $(target).offsetWidth; $(target).classList.add('hit');
            log(`Turn ${e.turn}: ${name(e.attackerId)} hits ${name(e.defenderId)} for ${e.damage}, ${e.defenderHpAfter} HP left`, meDefending ? 'loss' : 'win');
          } else {
            log(`Turn ${e.turn}: ${name(e.attackerId)} misses ${name(e.defenderId)}`, 'info');
          }
        });
        break;
      case 'battle.done':
        animate(async () => {
          const won = e.winnerId === state.player.id;
          log(`${name(e.winnerId)} wins in ${e.turns} turns and steals ${e.goldStolen} gold + ${e.silverStolen} silver (${e.lootPercent}%)`, won ? 'win' : 'loss');
          await refreshMe();
          setHp('me', state.player.hitPoints, state.player.hitPoints);
          await refreshOpponents();
        });
        break;
      case 'battle.failed':
        log(`Battle failed: ${e.error}`, 'loss');
        break;
      case 'leaderboard.changed':
        animate(() => refreshBoard(e.top));
        break;
      default:
        break;
    }
  }
  function opponentMax(id) { const o = state.opponents.find((p) => p.id === id); return o ? o.hitPoints : null; }

  // ---------- SignalR ----------
  async function connect() {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/arena', { accessTokenFactory: () => state.token })
      .withAutomaticReconnect()
      .build();
    connection.on('arenaEvent', onEvent);
    connection.onreconnecting(() => setStatus('reconnecting', false));
    connection.onreconnected(() => setStatus('online', true));
    connection.onclose(() => setStatus('offline', false));
    await connection.start();
    state.connection = connection;
    setStatus('online', true);
    log('Connected to the live arena feed', 'info');
  }
  function setStatus(text, ok) { const pill = $('connection'); pill.textContent = text; pill.classList.toggle('online', ok); }

  // ---------- wiring ----------
  $('enter').addEventListener('click', enter);
  $('fight').addEventListener('click', () => fight());
  $('refreshOpponents').addEventListener('click', refreshOpponents);
  $('auto').addEventListener('change', toggleAuto);
  $('opponents').addEventListener('change', renderOpponent);
  if (qs.get('name')) $('name').value = qs.get('name');
  if (qs.get('apiKey')) $('apiKey').value = qs.get('apiKey');
  if (qs.get('interval')) $('interval').value = qs.get('interval');

  const saved = sessionStorage.getItem('coliseum.player');
  if (saved) {
    const { player, token } = JSON.parse(saved);
    boot(player, token).catch(() => { sessionStorage.removeItem('coliseum.player'); $('setup').hidden = false; $('arena').hidden = true; });
  }
})();
