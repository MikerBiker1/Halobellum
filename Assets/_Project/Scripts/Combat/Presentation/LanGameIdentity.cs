// LanGameIdentity.cs
// This game's side of the LAN handshake (M11 Stage 4): its version (Unity's Player Settings), the code fingerprint and the unit stats
// files in force (LanIdentity), and the player's name. Also where the LAN log lines go (Debug.Log, which any thread may call).
// Depends on: Halobellum.Net (LanIdentity, LanHello), Halobellum.Core (UnitStatOverrides), UnityEngine.

using Halobellum.Core;
using Halobellum.Net;
using UnityEngine;

namespace Halobellum.Presentation
{
    public static class LanGameIdentity
    {
        /// <summary>This game's description for the handshake, with this player's name.</summary>
        public static LanHello Hello(string name) => LanIdentity.Hello(name, Application.version, UnitStatOverrides.Active?.Changes);

        /// <summary>The log hook of the LAN layer: one line in Player.log (thread-safe).</summary>
        public static void Log(string line) => Debug.Log(line);
    }
}
