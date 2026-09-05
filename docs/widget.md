# Embeddable widget

`/widget/coliseum-widget.js` renders a live leaderboard and a latest-battles feed inside any page with one script
tag. Demo page: `/widget/` (paste a player token).

```html
<script src="https://<api-host>/widget/coliseum-widget.js"
        data-api="https://<api-host>" data-token="<player token>" data-limit="10" data-title="Coliseum"></script>
```

| Attribute | Meaning |
|-----------|---------|
| `data-api` | Base URL of the Coliseum API (defaults to the page origin) |
| `data-token` | Bearer token used for REST and the hub (**required**) |
| `data-limit` | Leaderboard rows, 1–50 (default 10) |
| `data-title` | Panel title |

The widget renders in a shadow root (host CSS does not leak in or out), polls `/leaderboard` and `/players` every
15 s, and subscribes to the hub for `battle.queued`, `battle.done` and `leaderboard.changed`. It loads
`@microsoft/signalr` from cdnjs when the page does not already provide it.

## Security

- **The token is visible to the embedding page and to anyone who reads its source.** Use the least-privileged token
  that does the job: a **player token** (1 hour lifetime) sees the public leaderboard and that player's own
  battles, nothing else. Never embed an API key or a 24-hour service token.
- Cross-origin embedding works only if the API's CORS allow-list contains the host page's origin
  (`Cors:Origins`); the hub uses the same origin check.
- The widget is read-only: it never calls a mutating endpoint. If the token leaks, the worst case is that someone
  can read that player's battle reports until the token expires.
- The API applies rate limits per token, so an abusive host page throttles only itself.

## Threat model in one line

Give the widget what you would give a browser tab of that player, because that is exactly what it is.
