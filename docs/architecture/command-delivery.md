# Agent command delivery and recovery

The Agent polls `POST /api/v1/agent/commands/poll`. The server claims an eligible command and returns its original signed envelope. A claim sets `Status = Delivered` and a 30-second `DeliveryLeaseExpiresAt`. The lease timestamp is a database concurrency token, so simultaneous polls cannot claim the same pending command or expired lease twice.

If the poll response is lost, the command remains eligible after the lease expires. The server redelivers the same command ID, nonce, signature, issue time, and expiry; it does not create or sign a replacement command. The Agent's persistent nonce store rejects a command it has already accepted. Before polling again, the Agent retries its durable pending result receipt. The result endpoint is idempotent by command ID.

Each poll expires both `Pending` and `Delivered` commands whose signed `ExpiresAt` has passed. A delivery lease is capped at the command expiry, and the Agent rejects commands after that expiry. Commands with an existing result do not become eligible for delivery.

The lease provides recovery from a lost poll response; it does not promise exactly-once execution across arbitrary process or hardware failure. The Agent records the nonce before execution and stores the result after execution. A crash between those steps can leave the command without a receipt, while the persistent nonce prevents a second execution. This favors at-most-once handling over silently repeating a command. Current commands remain allow-listed and simulated by default.

The production migration backfills active legacy `Delivered` rows with a short initial lease so they can re-enter this recovery flow. No database cleanup job is required: polling expires stale rows for the authenticated device.
