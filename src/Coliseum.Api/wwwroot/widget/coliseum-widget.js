// Coliseum embeddable widget: a live leaderboard and latest-battles panel for any page.
//
//   <script src="https://<api-host>/widget/coliseum-widget.js"
//           data-api="https://<api-host>" data-token="<player token>" data-limit="10" data-title="Coliseum"></script>
//
// Security model (read docs/widget.md): the token is visible to the embedding page, so use the least-privileged
// one that works: a PLAYER token shows that player's own battles and the public leaderboard and expires in 1 h.
// Never embed an API key or a long-lived service token in a page. Rendering happens in a shadow root, so the host
// page's CSS cannot leak in or out. Requires @microsoft/signalr; it is loaded from cdnjs if the page has not got it.
(() => {
  const script = document.currentScript;
  const api = (script.dataset.api || location.origin).replace(/\/$/, '');
  const token = script.dataset.token;
  const limit = Math.min(50, Math.max(1, parseInt(script.dataset.limit || '10', 10)));
  const title = script.dataset.title || 'Coliseum';
  if (!token) { console.warn('coliseum-widget: data-token is required'); return; }

  const host = document.createElement('div');
  host.className = 'coliseum-widget';
  script.parentNode.insertBefore(host, script.nextSibling);
  const root = host.attachShadow({ mode: 'open' });
  root.innerHTML = `
    <style>
      :host { all: initial; display: block; font-family: system-ui, sans-serif; color: #e6e8ec; }
      .w { background: #171b22; border: 1px solid #262c36; border-radius: .6rem; padding: .8rem; max-width: 22rem; font-size: .85rem; }
      h4 { margin: 0 0 .5rem; font-size: .9rem; display: flex; justify-content: space-between; }
      .dot { width: .5rem; height: .5rem; border-radius: 50%; background: #8b93a1; display: inline-block; } .dot.on { background: #3fb950; }
      table { width: 100%; border-collapse: collapse; } td, th { text-align: left; padding: .2rem .3rem; border-bottom: 1px solid #262c36; }
      .me { color: #f2b134; } .feed { margin: .6rem 0 0; padding: 0; list-style: none; max-height: 8rem; overflow-y: auto; }
      .feed li { padding: .15rem 0; color: #8b93a1; } .feed li.win { color: #3fb950; } .feed li.loss { color: #f85149; }
      .foot { margin-top: .4rem; font-size: .7rem; color: #8b93a1; }
    </style>
    <div class="w">
      <h4><span>${title}</span><span class="dot" id="dot" title="live feed"></span></h4>
      <table><thead><tr><th>#</th><th>Player</th><th>Score</th></tr></thead><tbody id="board"></tbody></table>
      <ul class="feed" id="feed"></ul>
      <div class="foot">live via SignalR · read-only</div>
    </div>`;
  const $ = (id) => root.getElementById(id);
  const names = {};
  let me = null;

  const get = (path) => fetch(api + path, { headers: { Authorization: 'Bearer ' + token } }).then((r) => (r.ok ? r.json() : Promise.reject(new Error(r.status))));
  const name = (id) => names[id] || id.slice(0, 8);

  function renderBoard(entries) {
    $('board').innerHTML = entries.slice(0, limit).map((e) => `<tr class="${e.playerId === me ? 'me' : ''}"><td>${e.rank}</td><td>${name(e.playerId)}</td><td>${e.score}</td></tr>`).join('') || '<tr><td colspan="3">no scores yet</td></tr>';
  }
  function log(text, cls = '') {
    const li = document.createElement('li'); li.textContent = text; li.className = cls;
    const feed = $('feed'); feed.prepend(li); while (feed.children.length > 20) feed.removeChild(feed.lastChild);
  }
  async function refresh() {
    try {
      const [board, players] = await Promise.all([get('/leaderboard?limit=' + limit), get('/players?limit=100')]);
      players.forEach((p) => { names[p.id] = p.name; });
      renderBoard(board.entries);
    } catch (err) { log('refresh failed: ' + err.message); }
  }
  function onEvent(json) {
    const e = JSON.parse(json);
    if (e.type === 'battle.done') log(`${name(e.winnerId)} beat ${name(e.loserId)} in ${e.turns} turns (+${e.score})`, e.winnerId === me ? 'win' : e.loserId === me ? 'loss' : '');
    else if (e.type === 'battle.queued') log(`${name(e.attackerId)} challenges ${name(e.defenderId)}`);
    else if (e.type === 'leaderboard.changed') renderBoard(e.top);
  }
  async function connect() {
    if (!window.signalR) await new Promise((res, rej) => { const s = document.createElement('script'); s.src = 'https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.7/signalr.min.js'; s.onload = res; s.onerror = rej; document.head.appendChild(s); });
    const c = new signalR.HubConnectionBuilder().withUrl(api + '/hubs/arena', { accessTokenFactory: () => token }).withAutomaticReconnect().build();
    c.on('arenaEvent', onEvent);
    c.onclose(() => $('dot').classList.remove('on'));
    c.onreconnected(() => $('dot').classList.add('on'));
    await c.start();
    $('dot').classList.add('on');
  }
  try { me = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))).sub; } catch { me = null; }
  refresh(); setInterval(refresh, 15000); connect().catch((err) => log('feed unavailable: ' + err.message));
})();
