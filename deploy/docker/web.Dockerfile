FROM node:24.18.0-alpine AS build
WORKDIR /src
COPY package.json package-lock.json ./
COPY scripts/dependency-security scripts/dependency-security
COPY apps/company/package.json apps/company/package.json
COPY apps/employee/package.json apps/employee/package.json
COPY apps/mobile/package.json apps/mobile/package.json
COPY packages/api-client/package.json packages/api-client/package.json
COPY packages/shared-config/package.json packages/shared-config/package.json
COPY packages/web-ui/package.json packages/web-ui/package.json
ARG WEB_APP=company
# Shared UI is imported by both portals and must be present in filtered workspace installs.
RUN --mount=type=secret,id=proxy_ca \
    if [ -f /run/secrets/proxy_ca ]; then export NODE_EXTRA_CA_CERTS=/run/secrets/proxy_ca; fi; \
    npm ci --workspace=@sentinellan/$WEB_APP --workspace=@sentinellan/web-ui --include-workspace-root --strict-ssl=true
COPY apps/company apps/company
COPY apps/employee apps/employee
COPY packages packages
ARG NEXT_PUBLIC_API_URL=
ARG NEXT_PUBLIC_COMPANY_URL=/company
ARG NEXT_PUBLIC_EMPLOYEE_URL=/employee
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL NEXT_PUBLIC_COMPANY_URL=$NEXT_PUBLIC_COMPANY_URL NEXT_PUBLIC_EMPLOYEE_URL=$NEXT_PUBLIC_EMPLOYEE_URL
RUN npm run build --workspace=@sentinellan/$WEB_APP
FROM node:24.18.0-alpine
ARG SOURCE_REVISION=
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
WORKDIR /app
ARG WEB_APP=company
ENV NODE_ENV=production PORT=3000 HOSTNAME=0.0.0.0 WEB_APP=$WEB_APP
COPY --from=build /src/apps/$WEB_APP/.next/standalone ./
COPY --from=build /src/apps/$WEB_APP/.next/static ./apps/$WEB_APP/.next/static
EXPOSE 3000
CMD ["sh", "-c", "node apps/$WEB_APP/server.js"]
