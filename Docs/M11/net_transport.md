# M11 network transport (Stage 2, transport part)

The engine-free code that carries messages between the two games. It lives in `Assets/_Project/Scripts/Net/Transport` (assembly `Halobellum.Net`, namespace `Halobellum.Net`). It uses only `System.Net.Sockets` and knows nothing about the Match, Combat or Economy, so it compiles on its own. The lockstep coordinator (`LanMatchCoordinator`, in `Net/Lockstep`, see `lockstep.md`) sits on top of `LanSession` through `ILanChannel`.

Tests: `dotnet test Tools/SimTests` (with everything else; filter `FullyQualifiedName~Halobellum.Tests.Net` for the 46 Net tests, about 15 s). They use real sockets on 127.0.0.1 with ports the system picks, so Windows Firewall never asks.

## Files

| File | What it holds |
|---|---|
| `NetJson.cs` | `JsonObject`, `JsonNumber`, `NetJson` (a small JSON writer and reader) |
| `NetMessage.cs` | `NetMessage` (a JSON object with a type in `"t"`), `NetMessageTypes` |
| `Framing.cs` | `Framing` (encode a frame), `FrameReader` (cut a byte stream into frames), `FrameException` |
| `LanTimings.cs` | `LanTimings` (times), `LanDefaults` (ports, protocol, game name), `NetLog` (log hook and clock) |
| `LanConnection.cs` | `LanConnection`, `LanConnectionState`, `LanCloseReason`, `LanClient` |
| `LanHost.cs` | `LanHost` |
| `LanAddresses.cs` | `LanAddresses` (this PC's addresses and name for the host page; a typed "address:port") |
| `LanDiscovery.cs` | `LanAnnouncer`, `LanBrowser`, `LanGameInfo` |
| `LanHandshake.cs` | `LanHello` (Hello / Welcome contents and the match check) |
| `LanSession.cs` | `LanSession`, `LanSessionState` |

## Messages and frames

- A message is a UTF-8 JSON object with a string field `"t"`, its type. `new NetMessage("Ready").Set("round", 3)`; read with `m.Type`, `m.GetInt("round")`, `GetString`, `GetBool`, `GetLong`, `GetULong`, `GetFloat`, `GetDouble`, `GetObject`, `GetStringList`, `GetObjectList`, `GetArray`, `Has`.
- Values: strings, numbers, true/false, null, nested objects, arrays (of strings, objects or anything). Numbers keep their exact text. `Set(key, float)` writes 9 significant digits and `Set(key, double)` 17, so they read back to the same bits on any machine (the lockstep needs exact positions). NaN and infinity are refused. `Set(key, ulong)` writes the exact whole number (a state hash or content fingerprint); read it back with `GetULong`. Without that overload C# would quietly pick `Set(key, float)` for a ulong.
- On the wire: a 4-byte big-endian length, then the payload. Limit 4 MB (`Framing.MaxFrameBytes`): `Framing.Encode` throws `FrameException` above it, and `FrameReader` refuses a longer frame as soon as its header arrives. `FrameReader.Push(bytes, offset, count)` then `TryRead` / `TryReadText` until false; it copes with half frames and several frames in one read.
- The transport's own types: `Ping`, `Pong` (heartbeat, never handed to the game), `Hello`, `Welcome`, `Reject` (handshake). The layers above add theirs (MatchStart, DraftPick, Ready, StateHash, BattleResult, Desync, Leave, Status).

## LanConnection

One TCP connection (NoDelay). A reader thread fills an inbox; a writer thread sends the queue, sends a Ping every `PingIntervalMs` (1 s) and closes after `TimeoutMs` (10 s) with nothing received. The game drains the inbox on its own thread.

- `bool Send(JsonObject)`: thread-safe; queues and returns at once. False once closed. While Connecting, messages wait and go out once connected.
- `bool TryReceive(out NetMessage)`, `List<NetMessage> ReceiveAll()`, `int Pending`: work after the close too, until the inbox is empty (a Reject arrives just before the close).
- `State`: Connecting, Connected, Closed (final). `CloseReason`: None, LocalClose, RemoteClosed, Timeout, Error. `CloseDetail`: the reason in words for the player and the log.
- `Close()`: idempotent, any thread, does not wait. It sends what is still queued (up to `CloseFlushMs`), shuts the sending side, and waits up to `CloseLingerMs` for the other side to close in turn, so the last message is not cut off. `WaitClosed(ms)` waits until the socket is released and the threads have ended. A Close while still connecting shuts the connecting socket, so the attempt stops at once and `WaitClosed` returns once the connect thread has ended.
- `RoundTripMs` (last Ping, -1 before the first Pong), `RemoteAddress`, `Label`.
- A broken frame (too long, bad UTF-8, bad JSON, no `"t"`) closes with Error.

`LanClient.Connect(address, port, timings, log)` returns at once with a connection in Connecting; it becomes Connected, or Closed with Error and a detail ("No answer from 192.168.1.5:47810 within 5 s. Is the game hosting there, and does Windows Firewall on that PC allow Halobellum?") after `ConnectTimeoutMs`. The address can be an IPv4 address or a computer name.

## LanHost

`new LanHost(port = 47810, bindAddress = null (all cards), timings, log)`, then `TryStart(out error)` (false with a reason if the port is taken). `Port` gives the port in use (useful with port 0).

- One guest at a time. `TryAcceptGuest(out LanConnection)` hands over a newly taken guest (polled, never blocks).
- Anyone else who connects while the guest's connection is open, or after `Lock()`, gets `Reject {reason: "This game already has a player."}` and is disconnected cleanly.
- When the guest's connection closes before `Lock()` (refused handshake, guest left), the next guest is taken.
- `Stop()`: idempotent; closes the listener and the guest's connection.
- An accept that fails for a passing reason (`IsPassingAcceptError`: ConnectionReset, as Windows gives when a guest connects and resets before it is taken, ConnectionAborted, NoBufferSpaceAvailable, TooManyOpenSockets, Interrupted, TryAgain, WouldBlock, NetworkReset) is logged and the host goes on accepting after a back-off (50 ms, doubling up to 1 s). Any other accept failure ends the accept thread for good: `Failed` is true, `FailReason` says why and `IsListening` is false. `LanSession.HostFailed` passes it on, and `LanLobby` turns it into `HostFailed` ("Hosting stopped: ... Trying again...") and listens again after `RetryMs`. A connection that cannot be set up (no thread for it) is dropped and the host keeps accepting.
- `BeforeAccept` (tests only) runs on the accept thread before each accept and may throw to act out an accept error.

## LanAddresses

This PC's addresses for the host page (`LanLobby.JoinAt`): `LocalIPv4()` (the IPv4 addresses of the cards that are up; loopback and self-assigned 169.254.x.x left out), `ComputerName()` (`Dns.GetHostName`), `JoinAtText(addresses, name)` ("Others can join at 192.168.1.5 (PC-NAME)."), and `SplitPort(typed, out port)`, which reads "192.168.1.5:47810" as the computer and the port (`LanLobby.Join` uses the port when one is typed).

## Discovery

- `new LanAnnouncer(hostName, version, gamePort, discoveryPort = 47811, bindAddress = null, log)`, `TryStart(out error)`, `Close()`. Every `IntervalMs` (1 s) it sends `{"game":"Halobellum","proto":1,"version":"0.416.2","name":"Mike","port":47810}` to 255.255.255.255 and to the broadcast address of every IPv4 card that is up (refreshed every 10 s), plus any `AddTarget(IPEndPoint)` address. `Broadcast = false` sends only to the added targets.
- `new LanBrowser(discoveryPort = 47811, bindAddress = null, log, clockMs = null)`, `TryStart(out error)`, `Close()`. Listens with ReuseAddress (another program, or a second copy of the game, can share the port). `Games()` gives copies of the games heard in the last `StaleMs` (4 s), sorted by name, each with `Name`, `Version`, `Proto`, `Address` (where the packet came from), `Port`, `LastHeardMs`. Packets that are not a Halobellum announcement are ignored.
- Helpers: `LanAnnouncer.BuildAnnouncement`, `LanAnnouncer.BroadcastAddresses()`, `LanAnnouncer.BroadcastAddress(address, mask)`, `LanBrowser.ParseAnnouncement`.
- Tested on loopback with a unicast announcement (`AddTarget`) to a browser bound to 127.0.0.1: broadcasts do not reach a loopback-bound socket, and binding all cards in a test would bring up the firewall prompt. The real broadcast path is only covered by the two-PC check in Stage 5.

## Handshake

`LanHello { Proto, Version, Code, Content, Name }`, `ToMessage(type)`, `FromMessage(m)`.

- The guest sends `Hello` with its description. The host answers `Welcome` (its own description, including the name) or `Reject {reason}` and closes.
- `Code` is a fingerprint of the build; `Content` a fingerprint of the content after the unit stats files. `Content` may be one hash or sections `"units=1a2b;cards=3c4d;spells=...;rules=..."`, so a mismatch can name what differs.
- `LanHello.Mismatch(mine, theirs)` gives null or the first difference, from `mine`'s point of view, checked in order protocol, version, code, content:
  - "The other game uses LAN protocol 2; this one uses 1. Both need the same version of the game."
  - "The other game is version 0.416.1; this one is 0.416.2."
  - "The two games are different builds of version 0.416.2 (code 1a2b3c4d there, 5e6f7a8b here). Both need the same build."
  - "Unit stats differ: units, spells. Both games need the same unit stats files." (or "Unit stats differ: fingerprint 1a2b3c4d there, 5e6f7a8b here. ..." for a single hash)
- The host sends the guest the reason from the guest's point of view (so "the other game" is the host), and keeps its own version in `LastRefusal`. The guest checks the Welcome's description too.

## LanSession

Single-threaded: call `Poll()` every frame (`TryReceive` calls it).

- `LanSession.Host(LanHost host, LanHello mine, log)`: starts the host if needed (Lost at once with the reason if it cannot listen). The session owns the host: `Close()` stops it. States: Waiting (no guest) → Handshaking (waiting for the Hello, up to `HandshakeTimeoutMs`) → Connected. A guest that does not match is sent a Reject and dropped, and the host goes back to Waiting (`LastRefusal` says why). A guest whose connection already closed is not taken, even with its Hello in the inbox: the host goes back to Waiting without locking. On Connected the host is locked: anyone else is refused.
- `LanSession.Join(address, port, LanHello mine, timings, log)` (or `Join(LanConnection, ...)`): Waiting (connecting) → Handshaking (Hello sent) → Connected, or Refused (Reject received, or the Welcome does not match), or Lost (no connection, no answer, host closed).
- Connected: `Send(JsonObject)` (false unless Connected), `TryReceive(out NetMessage)`. When the connection drops the session becomes Lost with `Reason`; messages received before the drop can still be read. Every step reads whether the connection is closed before it empties the inbox (the reader thread queues a message before it marks the close), so the last message before a close (a Leave, a final BattleResult, a Reject) always comes out, and the guest sees Refused with the reason rather than Lost.
- `Close()`: idempotent; state Closed.
- `State`, `Reason`, `LastRefusal`, `Peer` (the other side's `LanHello`), `PeerName`, `IsHost`, `IsFinished`, `Connection`.
- It implements `ILanChannel` for the coordinator: `IsOpen` is `State == Connected` (as of the last Poll), `LostReason` is `Reason` once finished.

## Logging

Every class takes an `Action<string> log`; `NetLog.Write` adds `"[LAN] "` and swallows exceptions from the hook. It is called from the background threads too, so the hook must be thread-safe (Unity's `Debug.Log` is; a file log needs a lock).

## Timings (`LanTimings`, milliseconds)

| Field | Default | Meaning |
|---|---|---|
| PingIntervalMs | 1000 | heartbeat; 0 sends none (tests) |
| TimeoutMs | 10000 | silence before the connection counts as lost |
| ConnectTimeoutMs | 5000 | guest's connect attempt |
| HandshakeTimeoutMs | 10000 | wait for Hello / Welcome |
| CloseFlushMs | 1000 | local close: time to send what is queued |
| CloseLingerMs | 1000 | local close: wait for the other side's close |
| SendTimeoutMs | 10000 | one blocking send |

Every object copies the timings it is given, so changing them afterwards has no effect.

## Unity and the test harnesses

- `Halobellum.Net.asmdef`: noEngineReferences, references Core, Combat and Economy (the coordinator plays a Match), autoReferenced. `Halobellum.Presentation`, `Halobellum.UI` and both test assemblies reference it.
- Tests: `Assets/Tests/EditMode/Net/Transport` and `Net/Lockstep`, in the ordinary EditMode test assembly `Halobellum.Tests.EditMode` (namespace `Halobellum.Tests.Net`). They are plain NUnit with no engine code.
- `Tools/SimTests` compiles `Scripts/Net/**` with Core, Combat (without Presentation) and Economy, and every test under `Assets/Tests/EditMode` except `Unity/**`, so one `dotnet test Tools/SimTests` runs every engine-free test. (The separate `Tools/NetTests` harness, the `Halobellum.Tests.Net` assembly and the `#if HALOBELLUM_NET_TESTS` wrappers are gone.)
