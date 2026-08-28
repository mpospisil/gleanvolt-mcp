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
  -- src/Gleanvolt.Mcp/bin/Release/net10.0/publish/Gleanvolt.Mcp
```

Point it at the published binary rather than at `dotnet run`, which re-checks the build every time the
client starts a server.

| Variable | Required | Meaning |
|---|---|---|
| `GLEANVOLT_URL` | yes | Base address of the installation, e.g. `http://gleanvolt.local:8090` — use the IP if mDNS does not resolve from the client machine |
| `GLEANVOLT_API_KEY` | yes | One of the installation's `Api:Keys` |
| `GLEANVOLT_MCP_ALLOW_WRITES` | no | `true` registers the four control tools. Anything else leaves this server read-only |

Missing or malformed configuration **exits at launch** with a message on stderr, rather than starting a
server whose every tool would answer with the same connection error.

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
Serving http://gleanvolt.local:8090/ as 13 tools; writes are ENABLED.
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
