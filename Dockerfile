# syntax=docker/dockerfile:1
#
# The image exists for HTTP mode. A stdio server is spawned by its client and dies with it, which a
# container is the wrong shape for; an HTTP server is a service that has to already be running before
# Home Assistant can be pointed at it, which is exactly what a compose stack is for.
#
# Both Linux architectures come out of this one file, cross-compiled rather than emulated: the SDK
# stage is pinned to the *builder's* architecture with $BUILDPLATFORM and targets the requested one
# via `dotnet publish -a $TARGETARCH`, so an amd64 CI runner produces an arm64 image at native speed.
# The runtime stage below contains no RUN instruction, so no foreign-architecture binary is ever
# executed at build time and QEMU is not needed at all.
#
#   docker build --platform linux/arm64 -t gleanvolt-mcp .   # the Pi
#   docker build --platform linux/amd64 -t gleanvolt-mcp .   # an x64 host
#
# Windows Nano Server needs its own file -- a Dockerfile targets one OS. See Dockerfile.windows.
# CI publishes all three under one name as a multi-platform manifest list, so a deploy names a tag
# and never an architecture (.github/workflows/publish-image.yml).

ARG DOTNET_VERSION=10.0

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG TARGETARCH

# What the running server reports at startup and to a client at initialize. The defaults match
# Directory.Build.props, so a plain `docker build` is honestly labelled a local build; CI passes the
# release version and the commit. See src/Gleanvolt.Mcp/BuildInfo.cs.
ARG VERSION=0.0.0-dev
ARG SOURCE_REVISION=

WORKDIR /src

# The project files first, so a change to a .cs file does not re-restore every package.
COPY Directory.Build.props Gleanvolt.Mcp.slnx ./
COPY src/Gleanvolt.Mcp/Gleanvolt.Mcp.csproj src/Gleanvolt.Mcp/
COPY tests/Gleanvolt.Mcp.Tests/Gleanvolt.Mcp.Tests.csproj tests/Gleanvolt.Mcp.Tests/
RUN dotnet restore src/Gleanvolt.Mcp/Gleanvolt.Mcp.csproj -a "$TARGETARCH"

COPY . .

# PublishSingleFile is turned off here, and only here. It is right for the stdio build, where the whole
# point is one absolute path to hand `claude mcp add`; inside an image there is no path to simplify, and
# single-file would publish a native host for one architecture -- the opposite of what -a buys us here.
RUN dotnet publish src/Gleanvolt.Mcp/Gleanvolt.Mcp.csproj \
      -a "$TARGETARCH" \
      --configuration Release \
      --no-restore \
      --self-contained false \
      -p:PublishSingleFile=false \
      -p:Version="$VERSION" \
      -p:SourceRevisionId="$SOURCE_REVISION" \
      --output /app

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app ./

# The terms travel with the artifact: a registry label can be stripped by a re-tag, a file inside the
# image cannot.
COPY LICENSE ./

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
