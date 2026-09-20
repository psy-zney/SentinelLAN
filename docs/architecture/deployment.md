# Deployment

Development uses Compose for PostgreSQL, API, and Web; Redis is an optional `extended` profile. The Windows Agent is never containerized. Production requires managed secrets, HTTPS termination, database backups, certificate/key rotation, observability, and an explicit migration job.
