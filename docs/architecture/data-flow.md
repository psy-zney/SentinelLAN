# Luồng dữ liệu lõi

Các sơ đồ dưới đây cùng mô tả vòng đời thiết bị đầu cuối được ủy quyền. Quản trị VPS qua SSH là phần mở rộng riêng, không nằm trong chuỗi này.

```mermaid
sequenceDiagram
  participant B as Browser
  participant API as Backend
  participant DB as PostgreSQL
  B->>API: login(organization code, email, password)
  API->>DB: verify tenant user + store refresh hash
  API-->>B: HttpOnly access + refresh cookies
  B->>API: tenant-scoped request + anti-CSRF header
  B->>API: refresh after access expiry
  API->>DB: revoke old refresh + store replacement hash
  API-->>B: rotated HttpOnly cookies
  B->>API: logout
  API->>DB: revoke refresh session
```

```mermaid
sequenceDiagram
  participant A as Agent
  participant API as Backend
  participant DB as PostgreSQL
  participant W as Dashboard
  A->>API: enroll(one-time token)
  API->>DB: consume token + hash credential + audit log
  API-->>W: SignalR device-status
  API-->>A: device ID + credential
  A->>API: heartbeat(idempotency key) + technical telemetry
  API->>DB: verify idempotency + record snapshot
  API-->>W: SignalR device-status (live online)
  W->>API: create safe command + reason
  API->>DB: signed command + audit
  A->>API: poll
  API-->>A: verified, unexpired command
  A->>API: simulated result
  API->>DB: idempotent result + audit outcome
  API-->>W: SignalR command-status
```

Agent command trong sơ đồ trên trả kết quả mô phỏng; quy trình cô lập thật chưa được triển khai. Restart dịch vụ VPS qua SSH là luồng riêng, có host-key pinning, quyền, lý do, xác nhận, nonce và audit; xem [bối cảnh](context.md) và [threat model](../security/threat-model.md).

```mermaid
sequenceDiagram
  participant Admin
  participant API
  participant DB as PostgreSQL
  participant Employee as Employee web/mobile
  participant Tech as Technician
  Admin->>API: gán thiết bị cho Employee
  API->>DB: kiểm tra tenant và lưu assignment + audit
  Employee->>API: xem /my-device và telemetry được công bố
  API->>DB: đọc theo tenant và assignment
  Employee->>API: báo sự cố cho máy được gán
  API->>DB: lưu incident idempotent
  Tech->>API: xử lý incident theo quyền
  API->>DB: cập nhật trạng thái + audit
```
