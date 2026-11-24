// LanAddresses.cs
// This PC's own addresses, for the host page to show (M11): when the join list stays empty on the other PC (its firewall drops the
// announcements, separate networks, a router that keeps wireless devices apart), the guest types what the host's screen says.
// Also reads a typed address that carries a port ("192.168.1.5:47810", the form people copy).
// Plain C# 9 without the engine; System.Net only.
// Depends on: nothing in the game.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Halobellum.Net
{
    public static class LanAddresses
    {
        /// <summary>
        /// The IPv4 addresses of this PC's network cards that are up: loopback and self-assigned (169.254.x.x, no network behind
        /// it) left out, in the order the system lists them, without repeats. Empty when the cards can't be read.
        /// </summary>
        public static List<string> LocalIPv4()
        {
            var result = new List<string>();
            try
            {
                foreach (NetworkInterface card in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (card.OperationalStatus != OperationalStatus.Up || card.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation info in card.GetIPProperties().UnicastAddresses)
                    {
                        if (!Shown(info.Address)) continue;
                        string text = info.Address.ToString();
                        if (!result.Contains(text)) result.Add(text);
                    }
                }
            }
            catch { /* no list: the computer name may still be shown */ }
            return result;
        }

        /// <summary>True for an address worth showing: IPv4, not loopback, not self-assigned.</summary>
        public static bool Shown(IPAddress address)
        {
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address)) return false;
            byte[] b = address.GetAddressBytes();
            return !(b[0] == 169 && b[1] == 254) && !(b[0] == 0);
        }

        /// <summary>This PC's name on the network, or "" when it can't be read.</summary>
        public static string ComputerName()
        {
            try { return Dns.GetHostName() ?? ""; }
            catch { return ""; }
        }

        /// <summary>
        /// The host page's line: "Others can join at 192.168.1.5 (PC-NAME)." With several addresses they are joined with "or";
        /// with none, only the name; "" when neither is known. At most three addresses are listed.
        /// </summary>
        public static string JoinAtText(IReadOnlyList<string> addresses, string computerName)
        {
            var shown = new List<string>();
            if (addresses != null)
            {
                foreach (string a in addresses)
                {
                    if (!string.IsNullOrWhiteSpace(a) && shown.Count < 3) shown.Add(a.Trim());
                }
            }
            string name = (computerName ?? "").Trim();
            if (shown.Count == 0) return name.Length == 0 ? "" : "Others can join at " + name + ".";
            return "Others can join at " + string.Join(" or ", shown) + (name.Length > 0 ? " (" + name + ")" : "") + ".";
        }

        /// <summary>
        /// Splits a typed address into the computer and a port when it ends in ":port" (one colon, a number 1-65535), as in
        /// "192.168.1.5:47810". Otherwise the whole text is the computer and the port is 0 (the usual one).
        /// </summary>
        public static string SplitPort(string typed, out int port)
        {
            port = 0;
            string text = (typed ?? "").Trim();
            int colon = text.IndexOf(':');
            if (colon <= 0 || colon != text.LastIndexOf(':')) return text; // none, or an IPv6 address: left as it is
            string tail = text.Substring(colon + 1).Trim();
            if (!int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number < 1 || number > 65535) return text;
            port = number;
            return text.Substring(0, colon).Trim();
        }
    }
}
