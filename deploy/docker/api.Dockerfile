FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /src
COPY . .
ARG SOURCE_REVISION=
RUN dotnet publish apps/backend/src/SentinelLAN.Api/SentinelLAN.Api.csproj -c Release -o /app --no-self-contained -p:SourceRevisionId=$SOURCE_REVISION
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13
ARG SOURCE_REVISION=
LABEL org.opencontainers.image.revision=$SOURCE_REVISION
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SentinelLAN.Api.dll"]
