# Entity relationship overview

Sơ đồ chỉ tóm tắt các quan hệ nghiệp vụ chính. Schema EF Core và migration là nguồn chuẩn cho bảng, cột, constraint và index hiện hành. SQL hiện tại chưa khai báo foreign key vật lý giữa các bảng nghiệp vụ; tính toàn vẹn liên bảng phụ thuộc kiểm tra của ứng dụng. Xem [SQL PostgreSQL sinh từ migrations](schema.postgresql.sql), [bản đồ hệ thống HTML](../system-map.html) và [data dictionary](data-dictionary.md).

```mermaid
erDiagram
  ORGANIZATION ||--o{ USER : contains
  USER ||--o{ ACCOUNT_ACTIVATION_TOKEN : activates
  ORGANIZATION ||--o{ DEVICE : owns
  DEVICE ||--o{ DEVICE_QR_LABEL : identifies
  DEVICE ||--|| DEVICE_CREDENTIAL : authenticates
  DEVICE ||--o{ HEARTBEAT : emits
  DEVICE ||--o{ TELEMETRY_SNAPSHOT : emits
  DEVICE ||--o{ DEVICE_COMMAND : receives
  DEVICE_COMMAND ||--o| COMMAND_RESULT : produces
  ORGANIZATION ||--o{ POLICY : defines
  POLICY ||--o{ POLICY_ASSIGNMENT : assigns
  DEVICE ||--o{ AUDIT_LOG : concerns
  DEVICE ||--o{ INCIDENT_TICKET : reports
  DEVICE ||--o{ WORK_ORDER : schedules
  DEVICE ||--o{ ASSET_LOAN : tracks
```
