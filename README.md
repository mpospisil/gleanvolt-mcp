# Gleanvolt MCP

An [MCP](https://modelcontextprotocol.io) server that puts one [Gleanvolt](https://github.com/mpospisil/gleanvolt)
installation — a hybrid inverter, a home battery, an EV charger and the roof above them — in front of
an LLM as a set of tools.

It is a **client of Gleanvolt's HTTP API and nothing more**. It shares no code with the controller,
holds no control logic, and invents no capability: every tool here is a call the API already answers,
and every action is a button the web UI already has. If this server is running and the controller is
not, nothing works — which is the correct relationship between the two.

## What it exposes

Nine read tools, always:

| Tool | What it answers |
|---|---|
| `gleanvolt_overview` | What this installation is, and what it is doing right now |
| `gleanvolt_health` | Whether the controller is alive and what it can currently see |
| `gleanvolt_forecast` | The solar forecast the controller is planning from |
| `gleanvolt_vehicle` | What the car last said about itself, and when |
| `gleanvolt_energy_day` | A whole local day, added up |
| `gleanvolt_energy_intervals` | The energy series at recording resolution |
| `gleanvolt_sessions` | Charging sessions in a range |
| `gleanvolt_session` | One session in full |
| `gleanvolt_quote_plan` | **Quote a targeted charge without starting it** |

Four more only when writes are enabled:

| Tool | What it does |
|---|---|
| `gleanvolt_start` | Start charging in `solar`, `forecasted` or `fastNoBattery` |
| `gleanvolt_start_targeted` | Commit to a quoted plan |
| `gleanvolt_stop` | Stop controlled charging |
| `gleanvolt_set_battery_hold` | Arm or release the home battery's discharge hold |

`gleanvolt_quote_plan` is the tool the surface is built around. A targeted charge can be priced before
it is committed — how much sun it expects to catch, how much grid it would have to buy and when,
whether the deadline is reachable at all — so the model can put real numbers in front of a person
before a watt is spent. The quote and the start take the same arguments, so what is quoted is what is
committed.

## Running it

You need an installation with the API switched on, and a key.

The key's **name** is not a credential and is never sent — it is what reaches the log and the recorded
charging session as the source of an action, so a key named `claude-mcp` produces *"API (claude-mcp)
started Targeted"* rather than an anonymous write. Give this server its own key rather than sharing
one, or the attribution buys you nothing.

**Running the controller directly**, both halves live in `.env`:

```bash
Api__Enabled=true
Api__Keys__claude-mcp=$(openssl rand -hex 32)
```

**Running it under the shipped `docker-compose.yml`**, the name is fixed in the compose file and only
the secret comes from `.env` — so an out-of-the-box deployment has exactly one key, named `client`:

```yaml
Api__Enabled: ${API_ENABLED:-false}
Api__Keys__client: ${API_KEY:-}
```

```bash
API_ENABLED=true
API_KEY=<the secret>
```

To name a key after this server, add a line to the compose service and give it its own variable:

```yaml
Api__Keys__claude-mcp: ${MCP_API_KEY:-}
```

Either way, `GLEANVOLT_API_KEY` below is the **secret**, never the name.

Then publish this server and register it:

```bash
dotnet publish src/Gleanvolt.Mcp -c Release

claude mcp add gleanvolt \
  -e GLEANVOLT_URL=http://<the installation>:8090 \
  -e GLEANVOLT_API_KEY=<the key> \
  -- /path/to/gleanvolt-mcp/src/Gleanvolt.Mcp/bin/Release/net10.0/linux-x64/publish/Gleanvolt.Mcp
```

Point it at the published binary rather than at `dotnet run`, which re-checks the build every time the
client starts a server.

Two things about that path. It carries a runtime identifier because the build publishes single-file,
and that pins it to one — `linux-x64` above, `osx-arm64` or `win-x64` elsewhere; `dotnet publish`
prints the directory it wrote, so read it there rather than guessing. And it is absolute because the
client launches the server from its own working directory, not from this repository — a relative path
resolves only by luck, and when it does not, the client drops the server without saying why.

| Variable | Required | Meaning |
|---|---|---|
| `GLEANVOLT_URL` | yes | Base address of the installation, e.g. `http://gleanvolt.local:8090` — use the IP if mDNS does not resolve from the client machine |
| `GLEANVOLT_API_KEY` | yes | One of the installation's `Api:Keys` |
| `GLEANVOLT_MCP_ALLOW_WRITES` | no | `true` registers the four control tools. Anything else leaves this server read-only |
| `GLEANVOLT_MCP_TRANSPORT` | no | `stdio` (the default) or `http`. See [Serving it over HTTP](#serving-it-over-http) |
| `GLEANVOLT_MCP_HTTP_URL` | no | HTTP mode only. What to bind, default `http://0.0.0.0:8091` |
| `GLEANVOLT_MCP_HTTP_TOKEN` | no | HTTP mode only. A bearer token every request must present. Unset leaves the endpoint open |

Missing or malformed configuration **exits at launch** with a message on stderr, rather than starting a
server whose every tool would answer with the same connection error.

`GLEANVOLT_MCP_TRANSPORT` is the one variable that refuses a value it does not recognise, rather than
falling back. `GLEANVOLT_MCP_ALLOW_WRITES` can afford to treat a typo as "no", because that leaves the
hardware alone. A typo here has no safe side to fall on: `htpp` quietly starting a stdio server would
produce a process reading a stdin nobody is writing to, which looks exactly like a hang.

### Writes are opt-in

Four of these tools move real hardware. Left alone, they are not registered at all — the model is not
told they exist, which is a better answer than a tool that always refuses. Enable them deliberately,
per client, and only where a person is watching the conversation.

An MCP client hands a stdio server the environment it was registered with and nothing else, so the
switch belongs on the registration rather than in the shell you type it from:

```bash
claude mcp add gleanvolt \
  -e GLEANVOLT_URL=http://<the installation>:8090 \
  -e GLEANVOLT_API_KEY=<the key> \
  -e GLEANVOLT_MCP_ALLOW_WRITES=true \
  -- <the published binary, exactly as above>
```

A registration is not edited in place: `claude mcp remove gleanvolt` first, then add it again with the
variable. And the tool list is built once, at launch, so the client has to start the server afresh —
changing the variable under a running server changes nothing it has already advertised.

The value is matched against `true`, case-insensitively, and against nothing else. `TRUE` and `True`
count; `1`, `yes` and `on` do not, and anything unset or unrecognised leaves the server read-only.
One spelling rather than a family of them is deliberate: a typo should leave the hardware alone.

Nothing more is needed on the installation. The `Api__Enabled=true` and the key from above are what a
write uses too — this server sends the one key on every call — so the name you gave that key is what
lands in the charging session as the source of the action.

To confirm which mode it came up in, read the server's first line on stderr:

```
Serving http://gleanvolt.local:8090/ as 13 tools over stdio; writes are ENABLED.
```

Nine tools and `disabled` means the variable never reached the process. From the client side, `/mcp`
in Claude Code lists the tools it was actually given: the four control tools are there or they are not.

A read-only server also says so in its own words. The `instructions` it sends at initialize name this
variable, so a model asked to start a charge it has no tool for reports what is actually wrong and who
can change it, instead of saying it does not know how.

`gleanvolt_set_battery_hold` does one thing beyond passing the call through: it reads the inverter back
a few seconds after the write and returns what it saw. A 200 means the register was written, not that
the inverter honoured it, and the tool's description tells the model to report the read-back rather
than the acknowledgement.

### Serving it over HTTP

Everything above launches one server per client, over stdin and stdout, and it stays the default.
`GLEANVOLT_MCP_TRANSPORT=http` builds the other host instead: one long-running process that any number
of clients reach over the network at the same time.

That is what [Home Assistant's `mcp` integration](https://www.home-assistant.io/integrations/mcp/)
needs. It is a client that points at a URL and cannot spawn anything, so something has to be listening
before it can be configured at all.

```bash
docker build -t gleanvolt-mcp .

docker run -d --name gleanvolt-mcp --restart unless-stopped -p 8091:8091 \
  -e GLEANVOLT_URL=http://<the installation>:8090 \
  -e GLEANVOLT_API_KEY=<the key> \
  gleanvolt-mcp
```

The image sets `GLEANVOLT_MCP_TRANSPORT=http` itself — nothing is attached to its stdin, so no other
mode would work — and runs as a non-root user. Confirm it came up on the address you expect:

```
Serving http://gleanvolt.local:8090/ as 9 tools at http://0.0.0.0:8091/mcp; writes are disabled.
```

As a service alongside Home Assistant in the same compose stack:

```yaml
  gleanvolt-mcp:
    build: https://github.com/mpospisil/gleanvolt-mcp.git
    restart: unless-stopped
    environment:
      GLEANVOLT_URL: http://<the installation>:8090
      GLEANVOLT_API_KEY: ${GLEANVOLT_MCP_API_KEY:?}
      GLEANVOLT_MCP_ALLOW_WRITES: ${GLEANVOLT_MCP_ALLOW_WRITES:-false}
    # No `ports:` when Home Assistant is on this network. It reaches the container by service name,
    # and publishing to the host would put the endpoint on the LAN for nothing.
    expose:
      - 8091
```

The first build pulls the .NET SDK image and compiles from source, which on a Pi takes several minutes;
after that it is cached. Build it once on a faster machine and push the image to a registry if you would
rather not.

Then in Home Assistant, **Settings → Devices & services → Add integration → Model Context Protocol**,
and give it the URL:

```
http://gleanvolt-mcp:8091/mcp
```

`http://<the host>:8091/mcp` if you published the port instead. The path is `/mcp` and is not
configurable. Ignore the field's help text, which still says "the SSE endpoint" — the integration tries
Streamable HTTP first and only falls back to SSE if that is refused, and the label has not caught up.
The integration calls `initialize` when you submit the form, so a wrong address or an unreachable
installation is a failure in the dialog rather than a silent no-op later. The tools then
appear to whichever conversation agent you have given the MCP integration's tools to.

**On authentication.** Home Assistant's config flow asks for a URL and nothing else — a bearer token
has no field to go in, and the only credential it can supply is an OAuth one it negotiates itself.
So for Home Assistant, leave `GLEANVOLT_MCP_HTTP_TOKEN` unset and keep the endpoint on a network you
trust: a compose network with no published port, or a LAN behind your router. Setting a token makes the
integration fail at `initialize` with a 401.

The token is there for the clients that *can* send one. Claude Code reaches the same running server
with it:

```bash
claude mcp add --transport http gleanvolt http://<the host>:8091/mcp \
  --header "Authorization: Bearer <the token>"
```

A few consequences of one process serving everybody, worth knowing before you enable writes on it:

- **The write switch is now per-server, not per-client.** Over stdio each client gets its own process
  and its own `GLEANVOLT_MCP_ALLOW_WRITES`. Here one setting decides for every client at once, and a
  Home Assistant voice assistant is not a place where a person is reliably watching the conversation.
  Read-only is the sensible default for a shared instance.
- **Attribution collapses to one name.** This server sends its single `GLEANVOLT_API_KEY` on every
  call, so every action from every client lands in the charging session under that one key's name. If
  you want to tell Home Assistant's writes apart from Claude Code's, run two containers with two keys —
  `Api__Keys__home-assistant` and `Api__Keys__claude-mcp` — on two ports.
- **Home Assistant gives a tool call ten seconds.** `gleanvolt_quote_plan` and a wide
  `gleanvolt_energy_intervals` are the two that can take longer than that on a busy Pi; this server
  allows the installation thirty, so a slow answer reaches Home Assistant as a timeout on its side
  while the call itself completes.

The server is stateless: no session is kept between requests, which is what Home Assistant's pattern of
opening a connection per tool call and closing it again actually wants. Only the Streamable HTTP
endpoint is mapped — the legacy `/sse` and `/message` pair is off, and Home Assistant tries Streamable
HTTP first.

## The contract

`contract/openapi.json` is a checked-in copy of the document the installation serves at
`/api/v1/openapi.json`. Every route this server calls and every field it sends is held against it by
the test suite:

```bash
dotnet test
```

That file is the seam between the two repositories. Refresh it when the API changes, and read the
diff — a failing test here means a tool would have gone on calling something that moved.

```bash
curl -s http://gleanvolt.local:8090/api/v1/openapi.json > contract/openapi.json
dotnet test
```

## Deliberate omissions

- **Tools are curated, not generated one-per-endpoint.** A generated surface gives a model a dozen
  near-identical names and it picks wrong. `gleanvolt_overview` folds two calls together because a
  state of charge means little without the pack size.
- **Parameters are flat, not nested.** The API's targeted request has a nested `editable` object; the
  tools take `notBefore`, `notAfter` and `maxGridEnergyWh` directly, because a model fills flat
  arguments in correctly far more often than nested ones. `forbiddenWindows` is not exposed for the
  same reason — a list of objects is where flat stops working, and it can be added when something
  actually needs it.
- **Responses are passed through as the API's own JSON.** The model reads JSON; a local DTO for every
  response would only be a second place for the contract to be wrong.

## Licence

MIT — see [LICENSE](LICENSE). The controller it talks to is licensed separately.
