# The image exists for HTTP mode. A stdio server is spawned by its client and dies with it, which a
# container is the wrong shape for; an HTTP server is a service that has to already be running before
# Home Assistant can be pointed at it, which is exactly what a compose stack is for.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The project files first, so a change to a .cs file does not re-restore every package.
COPY Gleanvolt.Mcp.slnx ./
COPY src/Gleanvolt.Mcp/Gleanvolt.Mcp.csproj src/Gleanvolt.Mcp/
COPY tests/Gleanvolt.Mcp.Tests/Gleanvolt.Mcp.Tests.csproj tests/Gleanvolt.Mcp.Tests/
RUN dotnet restore src/Gleanvolt.Mcp/Gleanvolt.Mcp.csproj

COPY . .

# PublishSingleFile is turned off here, and only here. It is right for the stdio build, where the whole
# point is one absolute path to hand `claude mcp add`; inside an image there is no path to simplify, and
# single-file needs a runtime identifier pinned at build time -- which would mean one Dockerfile per
# architecture for no gain. The result runs under `dotnet` below and builds unchanged on arm64.
RUN dotnet publish src/Gleanvolt.Mcp/Gleanvolt.Mcp.csproj \
      --configuration Release \
      --no-restore \
      -p:PublishSingleFile=false \
      --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# The transport is baked in rather than left to the compose file. An image built for this is not useful
# in any other mode -- nothing is attached to its stdin -- so a missing variable should not be able to
# start a server that silently answers nobody.
ENV GLEANVOLT_MCP_TRANSPORT=http

# Cleared, not set. The base image ships ASPNETCORE_HTTP_PORTS=8080, and Kestrel logs a warning on
# every start about overriding it -- true but useless noise, since GLEANVOLT_MCP_HTTP_URL is the only
# address this server ever listens on.
ENV ASPNETCORE_HTTP_PORTS=

# Matches TransportOptions.DefaultBindAddress. Declared so `docker run -P` and `docker ps` both know it;
# the server binds all interfaces inside the container regardless.
EXPOSE 8091

# Not root. This server holds an API key that can move a charger, and it needs nothing from the
# filesystem beyond what it was built with.
USER $APP_UID

ENTRYPOINT ["dotnet", "Gleanvolt.Mcp.dll"]
