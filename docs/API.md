# Host Agent API v1

The Host Agent exposes only the three endpoints required by `Plan.md`. Requests are accepted only from loopback or the configured IPv4 LAN CIDR. Normal operation uses plain HTTP inside that restricted LAN; no internet or cloud service is involved.

Default base URL: `http://192.168.219.104:5055`

## `GET /status`

No token is required, but the LAN restriction still applies.

Successful response (`200 OK`):

```json
{
  "status": "online",
  "hostname": "HOSTNAME",
  "ip": "192.168.219.104",
  "uptimeSeconds": 12345,
  "smbReady": true,
  "apiVersion": "1"
}
```

`status: online` means the Agent itself is running. Controllers classify the user-facing state as ONLINE only when this compatible response and SMB readiness are both confirmed.

## `POST /sleep`

Requires `Authorization: Bearer <configured token>`. It queues a normal Windows sleep operation and returns before the network disappears.

Successful response (`202 Accepted`):

```json
{
  "action": "sleep",
  "accepted": true,
  "message": "Power action accepted."
}
```

## `POST /shutdown`

Requires the same Bearer token. It queues `shutdown.exe /s /t 0` without `/f`, so the request is a normal, non-forced Windows shutdown.

Successful response (`202 Accepted`) uses the same schema with `action: shutdown`.

## Error responses

- `401 Unauthorized`: missing or incorrect Bearer token.
- `403 Forbidden`: source address is outside the configured LAN CIDR.
- `409 Conflict`: another power action is pending.
- `503 Service Unavailable`: the Agent has no configured token and refuses all destructive commands.

All controller requests use finite timeouts. Authentication tokens must not be placed in URLs or source control.
