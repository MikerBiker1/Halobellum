# M11 network transport (Stage 2, transport part)

The engine-free code that carries messages between the two games. It lives in `Assets/_Project/Scripts/Net/Transport` (assembly `Halobellum.Net`, namespace `Halobellum.Net`). It uses only `System.Net.Sockets` and knows nothing about the Match, Combat or Economy, so it compiles on its own. The lockstep coordinator (`LanMatchCoordinator`, in `Net/Lockstep`, see `lockstep.md`) sits on top of `LanSession` through `ILanChannel`.

Tests: `dotnet test Tools/SimTests` (with everything else; filter `FullyQualifiedName~Halobellum.Tests.Net` for the Net tests alone, about 1.5 min). They use real sockets on 127.0.0.1 with ports the system picks, so Windows Firewall never asks.

## Files

| File | What it holds |
|---|---|
| `NetJson.cs` | `JsonObject`, `JsonNumber`, `NetJson` (a small JSON writer and reader) |
| `NetMessage.cs` | `NetMessage` (a JSON object with a type in `"t"`), `NetMessageTypes` |
| `Framing.cs` | `Framing` (encode a frame), `FrameReader` (cut a byte stream into frames), `FrameException` |
| `LanTimings.cs` | `LanTimings` (times), `LanDefaults` (ports, protocol, game name), `LanLimits` (sizes and counts, see Limits), `NetLog` (log hook and clock) |
| `LanConnection.cs` | `LanConnection`, `LanConnectionState`, `LanCloseReason`, `LanClient` |
| `LanHost.cs` | `LanHost` |
| `LanAddresses.cs` | `LanAddresses` (this PC's addresses and name for the host page; a typed "address:port") |
| `LanDiscovery.cs` | `LanAnnouncer`, `LanBrowser`, `LanGameInfo` |
| `LanHandshake.cs` | `LanHello` (Hello / Welcome contents and the match check) |
| `LanSession.cs` | `LanSession`, `LanSessionState` |

## Messages and frames

- A message is a UTF-8 JSON object with a string field `"t"`, its type. `new NetMessage("Ready").Set("round", 3)`; read with `m.Type`, `m.GetInt("round")`, `GetString`, `GetBool`, `GetLong`, `GetULong`, `GetFloat`, `GetDouble`, `GetObject`, `GetStringList`, `GetObjectList`, `GetArray`, `Has`.
- Values: strings, numbers, true/false, null, nested objects, arrays (of strings, objects or anything). Numbers keep their exact text. `Set(key, float)` writes 9 significant digits and `Set(key, double)` 17, so they read back to the same bits on any machine (the lockstep needs exact positions). NaN and infinity are refused. `Set(key, ulong)` writes the exact whole number (a state hash or content fingerprint); read it back with `GetULong`. Without that overload C# would quietly pick `Set(key, float)` for a ulong.
- On the wire: a 4-byte big-endian length, then the payload. Limit 1 MB (`Framing.MaxFrameBytes` = `LanLimits.MaxFrameBytes`): `Framing.Encode` throws `FrameException` above it, and `FrameReader` refuses a longer frame as soon as its header arrives.
- `NetJson.ParseObject` refuses a text longer than `NetJson.MaxChars` (1 M characters), more than `NetJson.MaxValues` (10 000) values, or nesting deeper than `MaxDepth` (32), with a `FormatException`. `FrameReader.Push(bytes, offset, count)` then `TryRead` / `TryReadText` until false; it copes with half frames and several frames in one read.
- The transport's own types: `Ping`, `Pong` (heartbeat, never handed to the game), `Hello`, `Welcome`, `Reject` (handshake). The layers above add theirs (MatchStart, DraftPick, Ready, StateHash, BattleResult, Desync, Leave, Status).

## LanConnection

One TCP connection (NoDelay). A reader thread fills an inbox; a writer thread sends the queue, sends a Ping every `PingIntervalMs` (1 s) and closes after `TimeoutMs` (10 s) with nothing received. The game drains the inbox on its own thread.

- `bool Send(JsonObject)`: thread-safe; queues and returns at once. False once closed. While Connecting, messages wait and go out once connected. It never throws for a message's size: a message over the frame limit, or one that takes the outbox past its bounds (the other game does not read), closes the connection with Error and a reason, and Send says false.
- `bool TryReceive(out NetMessage)`, `List<NetMessage> ReceiveAll()`, `int Pending`: work after the close too, until the inbox is empty (a Reject arrives just before the close).
- `State`: Connecting, Connected, Closed (final). `CloseReason`: None, LocalClose, RemoteClosed, Timeout, Error. `CloseDetail`: the reason in words for the player and the log.
- `Close()`: idempotent, any thread, does not wait. It sends what is still queued (up to `CloseFlushMs`), shuts the sending side, and waits up to `CloseLingerMs` for the other side to close in turn, so the last message is not cut off. `WaitClosed(ms)` waits until the socket is released and the threads have ended. A Close while still connecting shuts the connecting socket, so the attempt stops at once and `WaitClosed` returns once the connect thread has ended.
- `RoundTripMs` (last Ping, -1 before the first Pong), `RemoteAddress`, `Label`.
- A broken frame (too long, bad UTF-8, bad JSON, too many values, no `"t"`) closes with Error. So does an inbox the game does not empty fast enough (more than `MaxInboxMessages` or `MaxInboxBytes` waiting: "The other game sent more than this one can take at once").
- A Ping is answered only when its `"ms"` is a whole number of at most 20 characters (what `NowMs` gives); any other Ping is ignored, never echoed back.

`LanClient.Connect(address, port, timings, log)` returns at once with a connection in Connecting; it becomes Connected, or Closed with Error and a detail ("No answer from 192.168.1.5:47810 within 5 s. Is the game hosting there, and does Windows Firewall on that PC allow Halobellum?") after `ConnectTimeoutMs`. The address can be an IPv4 address or a computer name.

## LanHost

`new LanHost(port = 47810, bindAddress = null (all cards), timings, log)`, then `TryStart(out error)` (false with a reason if the port is taken). `Port` gives the port in use (useful with port 0).

- One guest at a time. `TryAcceptGuest(out LanConnection)` hands over a newly taken guest (polled, never blocks).
- Anyone else who connects after `Lock()` gets `Reject {reason: "This game already has a player."}` (`BusyReason`); while the guest's connection is open but before `Lock()` (its handshake is still going), `Reject {reason: "Another player is joining this game right now. Try again in a few seconds."}` (`JoiningReason`). Either way it is disconnected cleanly. A refused connection costs no thread of its own: the Reject is written on the accept thread, the sending side is shut, and a thread-pool task drops what the other side still sends until it closes in turn or `CloseLingerMs` passes. At most `LanLimits.MaxRefusing` (8) wait like that at once; more are closed at once. `Refusing` gives the count (tests).
- When the guest's connection closes before `Lock()` (refused handshake, guest left), the next guest is taken.
- `Stop()`: idempotent; closes the listener and the guest's connection.
- An accept that fails for a passing reason (`IsPassingAcceptError`: ConnectionReset, as Windows gives when a guest connects and resets before it is taken, ConnectionAborted, NoBufferSpaceAvailable, TooManyOpenSockets, Interrupted, TryAgain, WouldBlock, NetworkReset) is logged and the host goes on accepting after a back-off (50 ms, doubling up to 1 s). Any other accept failure ends the accept thread for good: `Failed` is true, `FailReason` says why and `IsListening` is false. `LanSession.HostFailed` passes it on, and `LanLobby` turns it into `HostFailed` ("Hosting stopped: ... Trying again...") and listens again after `RetryMs`. A connection that cannot be set up (no thread for it) is dropped and the host keeps accepting.
- `BeforeAccept` (tests only) runs on the accept thread before each accept and may throw to act out an accept error.

## LanAddresses

This PC's addresses for the host page (`LanLobby.JoinAt`): `LocalIPv4()` (the IPv4 addresses of the cards that are up; loopback and self-assigned 169.254.x.x left out), `ComputerName()` (`Dns.GetHostName`), `JoinAtText(addresses, name)` ("Others can join at 192.168.1.5 (PC-NAME)."), and `SplitPort(typed, out port)`, which reads "192.168.1.5:47810" as the computer and the port (`LanLobby.Join` uses the port when one is typed).

## Discovery

- `new LanAnnouncer(hostName, version, gamePort, discoveryPort = 47811, bindAddress = null, log)`, `TryStart(out error)`, `Close()`. Every `IntervalMs` (1 s) it sends `{"game":"Halobellum","proto":1,"version":"0.416.2","name":"Mike","port":47810}` to 255.255.255.255 and to the broadcast address of every IPv4 card that is up (refreshed every 10 s), plus any `AddTarget(IPEndPoint)` address. `Broadcast = false` sends only to the added targets.
- `new LanBrowser(discoveryPort = 47811, bindAddress = null, log, clockMs = null)`, `TryStart(out error)`, `Close()`. Listens with ReuseAddress (another program, or a second copy of the game, can share the port). `Games()` gives copies of the games heard in the last `StaleMs` (4 s), sorted by name, each with `Name`, `Version`, `Proto`, `Address` (where the packet came from), `Port`, `LastHeardMs`. Packets that are not a Halobellum announcement are ignored. It keeps at most `MaxGames` (32): when full, the stale ones go first, and if it is still full a new game is not kept (`Add` says false) until a place frees up. A name or version longer than 64 characters is cut short.
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
- `Mismatch` gives at most `LanLimits.MaxReasonChars` (300) characters (a version from the other game is cut to 64), and the host cuts both reasons again before its Reject, so a Reject always fits in a frame whatever the guest sent.

## LanSession

Single-threaded: call `Poll()` every frame (`TryReceive` calls it).

- `LanSession.Host(LanHost host, LanHello mine, log)`: starts the host if needed (Lost at once with the reason if it cannot listen). The session owns the host: `Close()` stops it. States: Waiting (no guest) → Handshaking (waiting for the Hello, up to `HelloTimeoutMs`, 3 s: a real guest sends it the moment it is connected, so a connection that sends nothing loses the slot soon; a guest who comes meanwhile is told another player is joining) → Connected. A guest that does not match is sent a Reject and dropped, and the host goes back to Waiting (`LastRefusal` says why). A guest whose connection already closed is not taken, even with its Hello in the inbox: the host goes back to Waiting without locking. On Connected the host is locked: anyone else is refused.
- `LanSession.Join(address, port, LanHello mine, timings, log)` (or `Join(LanConnection, ...)`): Waiting (connecting) → Handshaking (Hello sent) → Connected, or Refused (Reject received, or the Welcome does not match), or Lost (no connection, no answer, host closed).
- Connected: `Send(JsonObject)` (false unless Connected), `TryReceive(out NetMessage)`. One Poll moves at most `LanLimits.MaxMessagesPerUpdate` (256) messages from the connection into the session's queue, and only tops it up to that; the session turns Lost only once every message from before the drop has been moved. When the connection drops the session becomes Lost with `Reason`; messages received before the drop can still be read. Every step reads whether the connection is closed before it empties the inbox (the reader thread queues a message before it marks the close), so the last message before a close (a Leave, a final BattleResult, a Reject) always comes out, and the guest sees Refused with the reason rather than Lost.
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
| HandshakeTimeoutMs | 10000 | guest: wait for the Welcome or Reject |
| HelloTimeoutMs | 3000 | host: wait for the guest's Hello (at most HandshakeTimeoutMs) |
| CloseFlushMs | 1000 | local close: time to send what is queued |
| CloseLingerMs | 1000 | local close: wait for the other side's close |
| SendTimeoutMs | 10000 | one blocking send |

Every object copies the timings it is given, so changing them afterwards has no effect.

## Limits (`LanLimits`)

A broken or hostile game on the network must not make this one use much memory or freeze its main thread. Measured first (`LanMessageSizeTests`): the largest real message in whole AI matches is a Ready of about 8.6 KB (16 rounds at 2000 base health; a MatchStart is about 150 bytes, a BattleResult under 3 KB). A Ready with 3001 actions in one round (a squad dropped 3000 times) is about 770 KB, some 255 bytes an action, so the 1 MB frame holds about 4100 actions in one round; a game that ever tried to send more would close the connection with a reason (both players see the connection lost), not throw.

| Limit | Value | What happens past it |
|---|---|---|
| `MaxFrameBytes` | 1 MB | a longer frame from the other game closes the connection (Error); a longer message of this game's own closes it too, and Send says false |
| `MaxJsonValues` | 10 000 values | the message is refused (FormatException; on a connection: Error). 1 MB of `[{},{},...]` used to become over 200 MB of objects |
| `MaxJsonChars` | 1 M characters | NetJson refuses the text |
| `MaxPingDigits` | 20 | a Ping whose `"ms"` is not a whole number this short is not answered |
| `MaxOutboxMessages`, `MaxOutboxBytes` | 4096 messages, 4 MB waiting to be sent | the connection closes (Error, "it is not reading") |
| `MaxInboxMessages`, `MaxInboxBytes` | 4096 messages, 8 MB waiting for the game | the connection closes (Error, "sent more than this one can take at once") |
| `MaxMessagesPerUpdate` | 256 | one coordinator Update, LanGuestStart Poll, lobby Update or session Poll handles no more; the rest wait for the next frame, and a close is noticed after them |
| `MaxReasonChars` | 300 characters | text from the other game in a reason (a Reject, a Desync sent back, a Leave, a refusal shown) is cut, ending "..." |
| `MaxNameChars` | 64 characters | a name, version or message type from the other game in a reason or a log line, or in the join list, is cut |
| `MaxGamesListed` | 32 games | the join list keeps no more (stale ones go first) |
| `MaxRefusing` | 8 connections | more refused connections waiting for their close are closed at once |
| `LanMatchCoordinator.MaxRoundsAhead` | 2 rounds | a Ready, StateHash or BattleResult for a later round is a Desync ("more than 2 rounds past this game's round"); the other game is never more than one round ahead |

## Unity and the test harnesses

- `Halobellum.Net.asmdef`: noEngineReferences, references Core, Combat and Economy (the coordinator plays a Match), autoReferenced. `Halobellum.Presentation`, `Halobellum.UI` and both test assemblies reference it.
- Tests: `Assets/Tests/EditMode/Net/Transport` and `Net/Lockstep`, in the ordinary EditMode test assembly `Halobellum.Tests.EditMode` (namespace `Halobellum.Tests.Net`). They are plain NUnit with no engine code.
- `Tools/SimTests` compiles `Scripts/Net/**` with Core, Combat (without Presentation) and Economy, and every test under `Assets/Tests/EditMode` except `Unity/**`, so one `dotnet test Tools/SimTests` runs every engine-free test. (The separate `Tools/NetTests` harness, the `Halobellum.Tests.Net` assembly and the `#if HALOBELLUM_NET_TESTS` wrappers are gone.)
