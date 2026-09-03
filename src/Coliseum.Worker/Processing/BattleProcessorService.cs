// BattleProcessorService.cs
// Project: Coliseum.Worker
// Purpose: BackgroundService: XGROUP CREATE, claim stale (start + every 15s), XREADGROUP BLOCK 500, dispatch via scheduler, Task.Run per battle, Channel completions, XACK after settle, DLQ after 5 failures, graceful shutdown
// Status: STUB - implemented in MP-06. Design: docs/adr (public) and _referencia (private).
