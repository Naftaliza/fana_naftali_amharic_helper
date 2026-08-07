using System.Security.Cryptography;
using System.Text;

namespace AmharicHelper.Application.Common;

/// <summary>
/// Identifies whoever a unit of usage (an analysis, a TTS synthesis, a chat turn) is billed
/// against — a registered account, or a not-yet-registered contact identified only by a hashed
/// phone number or device id (an anonymous web visitor, or a future WhatsApp user). Exactly one
/// of UserId/ContactHash is set; UsageLedger enforces the same invariant with a CHECK constraint.
/// Never construct ContactHash by hand — always go through ForPhone/ForDevice so the same raw
/// value always hashes to the same subject.
/// </summary>
public readonly record struct UsageSubject(Guid? UserId, string? ContactHash)
{
    public static UsageSubject ForUser(Guid id) => new(id, null);

    /// <summary>An E.164 phone number, hashed — the raw number is never stored in the ledger.</summary>
    public static UsageSubject ForPhone(string e164) => new(null, Sha256Hex("phone:" + e164.Trim()));

    /// <summary>A client-generated device id (anonymous web trial) — weaker than a phone number
    /// (spoofable by clearing storage), but still a real server-side ceiling rather than none.</summary>
    public static UsageSubject ForDevice(string deviceId) => new(null, Sha256Hex("dev:" + deviceId.Trim()));

    /// <summary>A stable fallback subject when no device id was supplied at all — coarser than
    /// ForDevice (shared by every request from the same IP) but still bounds spend.</summary>
    public static UsageSubject ForIpFallback(string ip) => new(null, Sha256Hex("ip:" + ip.Trim()));

    private static string Sha256Hex(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}
