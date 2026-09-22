namespace Threadlink.SentinelModules.Steam
{
    using System;
    using System.Text;
    using Steamworks;

    internal static class SteamJoinPayloadCodec
    {
        private const string COMMAND = "+threadlink_join";

        internal static bool TryEncode(string payload, out string connectString, out string error)
        {
            connectString = null;
            error = null;

            if (string.IsNullOrWhiteSpace(payload))
            {
                error = "Join payload is empty.";
                return false;
            }

            var encoded = Convert
                .ToBase64String(Encoding.UTF8.GetBytes(payload))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            connectString = COMMAND + " " + encoded;

            if (
                Encoding.UTF8.GetByteCount(connectString)
                < Constants.k_cchMaxRichPresenceValueLength
            )
            {
                return true;
            }

            error =
                $"Encoded Steam join string exceeds the Steam rich-presence limit "
                + $"({Constants.k_cchMaxRichPresenceValueLength - 1} UTF-8 bytes).";

            connectString = null;
            return false;
        }

        internal static bool TryDecode(string connectString, out string payload)
        {
            payload = null;

            if (string.IsNullOrWhiteSpace(connectString))
                return false;

            var parts = connectString.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (!string.Equals(parts[i], COMMAND, StringComparison.OrdinalIgnoreCase))
                    continue;

                var encoded = parts[i + 1].Replace('-', '+').Replace('_', '/');

                var padding = encoded.Length % 4;

                if (padding != 0)
                    encoded = encoded.PadRight(encoded.Length + 4 - padding, '=');

                try
                {
                    payload = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    return !string.IsNullOrEmpty(payload);
                }
                catch
                {
                    payload = null;
                    return false;
                }
            }

            return false;
        }
    }
}
