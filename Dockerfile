FROM node:24-alpine AS styles
WORKDIR /source
COPY package.json package-lock.json ./
RUN npm ci
COPY src/DevSync.Web ./src/DevSync.Web
COPY tests/client ./tests/client
COPY scripts/build-code-editor.mjs ./scripts/build-code-editor.mjs
RUN npm run css:build && npm run test:client

FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /source
COPY global.json Directory.Build.props NuGet.Config DevSync.slnx ./
COPY src/DevSync.Domain/*.csproj src/DevSync.Domain/packages.lock.json ./src/DevSync.Domain/
COPY src/DevSync.Infrastructure/*.csproj src/DevSync.Infrastructure/packages.lock.json ./src/DevSync.Infrastructure/
COPY src/DevSync.Web/*.csproj src/DevSync.Web/packages.lock.json ./src/DevSync.Web/
COPY tests/DevSync.Tests/*.csproj tests/DevSync.Tests/packages.lock.json ./tests/DevSync.Tests/
RUN dotnet restore DevSync.slnx --locked-mode
COPY src ./src
COPY tests ./tests
COPY --from=styles /source/src/DevSync.Web/wwwroot/css/site.css ./src/DevSync.Web/wwwroot/css/site.css
COPY --from=styles /source/src/DevSync.Web/wwwroot/vendor/code ./src/DevSync.Web/wwwroot/vendor/code
RUN dotnet build DevSync.slnx -c Release --no-restore -m:1 \
    && dotnet test tests/DevSync.Tests -c Release --no-build --no-restore --filter "Category!=Database" \
    && dotnet publish src/DevSync.Web -c Release --no-build --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS runtime
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build --chown=app:app /publish ./
RUN mkdir -p /app/.local && chown app:app /app/.local
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 \
    CMD curl --fail --silent --show-error http://127.0.0.1:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "DevSync.Web.dll"]
