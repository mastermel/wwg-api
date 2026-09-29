# syntax=docker/dockerfile:1

# WWG Campaigner: the API serving the built front-end, in one image.
# Built for linux/amd64 and linux/arm64. The first two stages run on the build machine's own
# platform (BUILDPLATFORM) and target the requested one; the final stage only copies files (no
# RUN), so no emulation is needed for the other architecture.

# 1. Front-end: web/dist
FROM --platform=$BUILDPLATFORM node:24-slim AS web
# The image tag (e.g. v20260927.143005), shown on the About page. "dev" when built by hand.
ARG APP_VERSION=dev
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
# Scripts are skipped here: `prepare` generates the API client, which needs the sources below.
RUN npm ci --ignore-scripts
COPY api/openapi.json /src/api/openapi.json
COPY web/ ./
RUN APP_VERSION=$APP_VERSION npm run build

# 2. API: published for the target architecture
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS api
ARG TARGETARCH
WORKDIR /src
# .editorconfig carries the analyzer rule tuning; without it the build fails on suppressed rules.
COPY global.json .editorconfig ./
COPY api/Directory.Build.props api/Directory.Build.targets api/Directory.Packages.props api/BannedSymbols.txt api/
COPY api/src/Wwg.Api/Wwg.Api.csproj api/src/Wwg.Api/
RUN dotnet restore api/src/Wwg.Api -a $TARGETARCH
COPY api/src/ api/src/
# OpenApiGenerateDocuments=false: generating openapi.json runs the app, which can't run when
# cross-compiling; CI already checks the committed contract.
RUN dotnet publish api/src/Wwg.Api --no-restore -c Release -a $TARGETARCH \
      -p:OpenApiGenerateDocuments=false -o /out/app \
 && mkdir -p /out/data

# 3. Runtime: minimal, non-root (UID 1654), no shell
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY --from=api /out/app ./
COPY --from=web /src/web/dist ./wwwroot
# The SQLite database, its backups and the Data Protection keys live here; mount a volume.
COPY --from=api --chown=1654:1654 /out/data /data
VOLUME /data
EXPOSE 8080
# Probed every second while starting (--start-interval), so the container reports healthy as soon
# as it is (deploys and `compose up --wait` don't wait out a 30s interval); then every 30s.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --start-interval=1s --retries=3 \
  CMD ["./Wwg.Api", "--health-check"]
ENTRYPOINT ["./Wwg.Api"]
