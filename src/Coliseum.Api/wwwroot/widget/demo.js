// Mounts the widget with the pasted token and shows the exact snippet a third-party page would use.
(() => {
  const $ = (id) => document.getElementById(id);
  $('mount').addEventListener('click', () => {
    const token = $('token').value.trim();
    if (!token) return;
    $('host').innerHTML = '';
    const s = document.createElement('script');
    s.src = '/widget/coliseum-widget.js';
    s.dataset.api = location.origin;
    s.dataset.token = token;
    s.dataset.limit = '10';
    s.dataset.title = 'Coliseum leaderboard';
    $('host').appendChild(s);
    $('snippet').textContent = `<script src="${location.origin}/widget/coliseum-widget.js"\n        data-api="${location.origin}" data-token="<player token>" data-limit="10" data-title="Coliseum leaderboard"><\/script>`;
  });
})();
