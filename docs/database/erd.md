# Entity relationship overview

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
