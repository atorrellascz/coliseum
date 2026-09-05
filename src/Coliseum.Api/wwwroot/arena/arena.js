// Coliseum arena auto-play client. REST for actions, SignalR for the live feed, one identity per browser tab.
// Player mode: the tab creates a player and acts with that player's token.
// Spectator mode (?watch=<playerId>): a service token watches another player's battles, read-only.
(() => {
  const qs = new URLSearchParams(location.search);
  const $ = (id) => document.getElementById(id);
  const state = { player: null, token: null, watching: null, opponents: [], names: {}, hp: { me: null, opp: null }, connection: null, autoTimer: null, animating: Promise.resolve(), audio: null };

  // ---------- REST ----------
  async function api(path, { method = 'GET', body, token = state.token } = {}) {
    const res = await fetch(path, {
      method,
      headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) },
      body: body ? JSON.stringify(body) : undefined,
    });
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
      if (state.watching) {
        const player = await api('/players/' + state.watching, { token: service });
        sessionStorage.setItem('coliseum.spectator', JSON.stringify({ player, token: service }));
        await boot(player, service, true);
        return;
      }
      const created = await api('/players', {
        method: 'POST', token: service,
        body: { name: $('name').value.trim(), description: 'arena auto-play', gold: +$('gold').value, silver: +$('silver').value, attack: +$('attack').value, defense: +$('defense').value, hitPoints: +$('hitPoints').value },
      });
      sessionStorage.setItem('coliseum.player', JSON.stringify({ player: created.player, token: created.accessToken }));
      await boot(created.player, created.accessToken, false);
    } catch (err) {
      $('setupError').textContent = err.message;
      $('setupError').hidden = false;
    }
  }

  async function boot(player, token, spectator) {
    state.player = player; state.token = token;
    $('setup').hidden = true; $('arena').hidden = false;
    if (spectator) {
      $('spectator').textContent = `Spectator mode: watching ${player.name}. Battles are read-only.`;
      $('spectator').hidden = false;
      $('controls').hidden = true;
    }
    renderMe();
    await refreshOpponents();
    await connect(spectator);
    await refreshBoard();
    if (!spectator && qs.get('auto') === '1') { $('auto').checked = true; toggleAuto(); }
  }

  // ---------- rendering ----------
  function renderFighter(side, p) {
    $(side + 'Name').textContent = p ? p.name : 'choose an opponent';
    $(side + 'Stats').innerHTML = p ? `<span>ATK ${p.attack}</span><span>DEF ${p.defense}</span><span>HP ${p.hitPoints}</span>` : '';
    $(side + 'Wealth').innerHTML = p ? `<span>gold <b>${p.gold.toLocaleString()}</b></span><span>silver <b>${p.silver.toLocaleString()}</b></span>` : '';
    state.hp[side] = p ? p.hitPoints : null;
    setHp(side, p ? p.hitPoints : 0, p ? p.hitPoints : 1);
    $(side).classList.remove('winner', 'loser');
  }
  function setHp(side, hp, max) {
    const pct = Math.max(0, Math.min(100, (hp / max) * 100));
    const bar = $(side + 'Hp');
    bar.style.width = pct + '%';
    bar.classList.toggle('low', pct < 30);
    $(side + 'HpText').textContent = `${hp} / ${max} HP`;
  }
  function renderMe() { renderFighter('me', state.player); }
  function opponent() { return state.opponents.find((o) => o.id === $('opponents').value) || null; }
  function renderOpponent() { renderFighter('opp', opponent()); }
  function showOpponent(id) {
    const o = state.opponents.find((p) => p.id === id);
    if (o && $('opponents').value !== id) { $('opponents').value = id; renderOpponent(); }
  }
  function floatText(side, text, cls) {
    const el = document.createElement('span');
    el.className = 'float ' + cls; el.textContent = text;
    el.style.left = (35 + Math.random() * 30) + '%';
    $(side + 'Floats').appendChild(el);
    setTimeout(() => el.remove(), 950);
  }
  function toast(text, cls = '') {
    const el = document.createElement('div'); el.className = 'toast ' + cls; el.textContent = text;
    $('toasts').appendChild(el); setTimeout(() => el.remove(), 5000);
  }
  function log(text, cls = '') {
    const li = document.createElement('li'); li.textContent = text; if (cls) li.className = cls;
    const ol = $('log'); ol.appendChild(li); while (ol.children.length > 200) ol.removeChild(ol.firstChild); ol.scrollTop = ol.scrollHeight;
  }
  function name(id) { return id === state.player.id ? state.player.name : (state.names[id] || id.slice(0, 8)); }
  function beep(freq, ms) {
    if (!$('sound').checked) return;
    try {
      state.audio ??= new (window.AudioContext || window.webkitAudioContext)();
      const o = state.audio.createOscillator(), g = state.audio.createGain();
      o.frequency.value = freq; o.type = 'square'; g.gain.value = .04;
      o.connect(g); g.connect(state.audio.destination); o.start(); o.stop(state.audio.currentTime + ms / 1000);
    } catch { /* audio not available */ }
  }

  async function refreshOpponents() {
    const players = await api('/players?limit=100');
    state.opponents = players.filter((p) => p.id !== state.player.id);
    players.forEach((p) => { state.names[p.id] = p.name; });
    const select = $('opponents'), current = select.value;
    select.innerHTML = state.opponents.map((p) => `<option value="${p.id}">${p.name} (ATK ${p.attack} / DEF ${p.defense} / HP ${p.hitPoints})</option>`).join('');
    if (state.opponents.some((p) => p.id === current)) select.value = current;
    renderOpponent();
  }
  async function refreshBoard(entries) {
    const board = entries || (await api('/leaderboard?limit=10')).entries;
    $('board').innerHTML = board.map((e) => `<tr class="${e.playerId === state.player.id ? 'me' : ''}"><td>${e.rank}</td><td>${name(e.playerId)}</td><td>${e.score.toLocaleString()}</td></tr>`).join('') || '<tr><td colspan="3" class="muted">no scores yet</td></tr>';
  }
  async function refreshMe() { state.player = await api('/players/' + state.player.id); renderMe(); }

  // ---------- battles ----------
  async function fight(defenderId) {
    const id = defenderId || $('opponents').value;
    if (!id) { log('No opponent available yet: open another window to create one.', 'info'); return; }
    try {
      const accepted = await api('/battles', { method: 'POST', body: { defenderId: id } });
      log(`Challenge sent to ${name(id)} (battle ${accepted.battleId.slice(0, 8)}, ${accepted.status})`, 'info');
    } catch (err) { log('Could not submit: ' + err.message, 'loss'); }
  }
  function toggleAuto() {
    clearInterval(state.autoTimer); state.autoTimer = null;
    if ($('auto').checked) {
      const every = Math.max(1000, +$('interval').value || 3000);
      state.autoTimer = setInterval(async () => {
        if (state.opponents.length === 0) await refreshOpponents();
        const pick = state.opponents[Math.floor(Math.random() * state.opponents.length)];
        if (pick) { showOpponent(pick.id); await fight(pick.id); }
      }, every);
      log(`Auto-play on: a random opponent every ${every} ms`, 'info');
    } else { log('Auto-play off', 'info'); }
  }

  // Turn events arrive in a burst (the engine is microseconds); animate them one by one so the fight is watchable.
  function animate(step) { state.animating = state.animating.then(step).then(() => new Promise((r) => setTimeout(r, 350))); }

  function onEvent(json) {
    const e = JSON.parse(json);
    switch (e.type) {
      case 'battle.queued':
        if (e.defenderId === state.player.id) log(`${name(e.attackerId)} challenges ${state.player.name}!`, 'info');
        animate(() => { const other = e.attackerId === state.player.id ? e.defenderId : e.attackerId; showOpponent(other); ['me', 'opp'].forEach((s) => $(s).classList.remove('winner', 'loser')); });
        break;
      case 'battle.turn':
        animate(() => {
          const side = e.defenderId === state.player.id ? 'me' : 'opp';
          const max = side === 'me' ? state.player.hitPoints : (opponent()?.hitPoints || Math.max(e.defenderHpAfter + e.damage, 1));
          $('turnCounter').textContent = `turn ${e.turn}`;
          if (e.hit) {
            setHp(side, e.defenderHpAfter, max);
            $(side).classList.remove('hit'); void $(side).offsetWidth; $(side).classList.add('hit');
            floatText(side, '-' + e.damage, e.damage >= max * 0.5 ? 'crit' : 'dmg');
            beep(side === 'me' ? 180 : 320, 60);
            log(`Turn ${e.turn}: ${name(e.attackerId)} hits ${name(e.defenderId)} for ${e.damage}, ${e.defenderHpAfter} HP left`, side === 'me' ? 'loss' : 'win');
          } else {
            floatText(side, 'miss', 'miss');
            log(`Turn ${e.turn}: ${name(e.attackerId)} misses ${name(e.defenderId)}`, 'info');
          }
        });
        break;
      case 'battle.done':
        animate(async () => {
          const won = e.winnerId === state.player.id;
          const iAmIn = e.attackerId === state.player.id || e.defenderId === state.player.id;
          $('me').classList.add(won ? 'winner' : 'loser'); $('opp').classList.add(won ? 'loser' : 'winner');
          log(`${name(e.winnerId)} wins in ${e.turns} turns and steals ${e.goldStolen} gold + ${e.silverStolen} silver (${e.lootPercent}%)`, won ? 'win' : 'loss');
          if (iAmIn) { toast(won ? `Victory! +${e.goldStolen} gold, +${e.silverStolen} silver` : `Defeat: -${e.goldStolen} gold, -${e.silverStolen} silver`, won ? '' : 'loss'); beep(won ? 660 : 140, 220); }
          try {
            const report = await api('/battles/' + e.battleId);
            log(`Seed ${report.seed}: replay it with the MCP tool simulate_battle (same stats, same seed, same result).`, 'seed');
            $('lastBattle').textContent = `${name(e.attackerId)} vs ${name(e.defenderId)} · ${e.turns} turns · seed ${report.seed}`;
          } catch { /* report not readable (spectator without rights) */ }
          if (!state.watching) await refreshMe(); else state.player = await api('/players/' + state.player.id).catch(() => state.player);
          renderMe();
          await refreshOpponents();
          $('turnCounter').textContent = '';
        });
        break;
      case 'battle.failed': log(`Battle failed: ${e.error}`, 'loss'); break;
      case 'leaderboard.changed': animate(() => refreshBoard(e.top)); break;
      default: break;
    }
  }

  // ---------- SignalR ----------
  async function connect(spectator) {
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/arena', { accessTokenFactory: () => state.token }).withAutomaticReconnect().build();
    connection.on('arenaEvent', onEvent);
    connection.onreconnecting(() => setStatus('reconnecting', false));
    connection.onreconnected(async () => { setStatus('online', true); if (spectator) await connection.invoke('WatchPlayer', state.player.id); });
    connection.onclose(() => setStatus('offline', false));
    await connection.start();
    if (spectator) await connection.invoke('WatchPlayer', state.player.id);
    state.connection = connection;
    setStatus('online', true);
    log(spectator ? `Watching ${state.player.name} live` : 'Connected to the live arena feed', 'info');
  }
  function setStatus(text, ok) { const pill = $('connection'); pill.textContent = text; pill.classList.toggle('online', ok); }

  // ---------- wiring ----------
  $('enter').addEventListener('click', enter);
  $('fight').addEventListener('click', () => fight());
  $('refreshOpponents').addEventListener('click', refreshOpponents);
  $('auto').addEventListener('change', toggleAuto);
  $('opponents').addEventListener('change', renderOpponent);
  document.addEventListener('keydown', (ev) => {
    if (ev.target.tagName === 'INPUT' || ev.target.tagName === 'SELECT' || $('arena').hidden) return;
    const k = ev.key.toLowerCase();
    if (k === 'f' && !state.watching) fight();
    else if (k === 'a' && !state.watching) { $('auto').checked = !$('auto').checked; toggleAuto(); }
    else if (k === 'r') refreshOpponents();
    else if (k === 's') $('sound').checked = !$('sound').checked;
  });
  if (qs.get('name')) $('name').value = qs.get('name');
  if (qs.get('apiKey')) $('apiKey').value = qs.get('apiKey');
  if (qs.get('interval')) $('interval').value = qs.get('interval');
  if (qs.get('watch')) {
    state.watching = qs.get('watch');
    $('setupTitle').textContent = 'Watch a player';
    $('setupHint').textContent = 'Spectator mode needs a service token (API key). You will see that player\'s battles as they happen.';
    $('playerFields').hidden = true;
    $('enter').textContent = 'Start watching';
  }

  const saved = sessionStorage.getItem(state.watching ? 'coliseum.spectator' : 'coliseum.player');
  if (saved) {
    const { player, token } = JSON.parse(saved);
    if (!state.watching || player.id === state.watching) {
      boot(player, token, !!state.watching).catch(() => { sessionStorage.removeItem('coliseum.player'); sessionStorage.removeItem('coliseum.spectator'); $('setup').hidden = false; $('arena').hidden = true; });
    }
  }
})();
