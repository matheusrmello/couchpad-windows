using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using CouchPad.Core.Automation;

namespace CouchPad.Platform.Windows;

/// <summary>Terminal decisions belong to one immutable rescue transaction.</summary>
public enum RescueDecisionState
{
    Armed,
    Committed,
    Aborted,
    RecoveryRequested,
    Recovered,
}

public sealed record RescueDecisionRecord(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] Guid TransactionId,
    [property: JsonRequired] string BaselineSha256,
    [property: JsonRequired] long Revision,
    [property: JsonRequired] RescueDecisionState State);

/// <summary>
/// Stores one transaction's decision. Cooperating processes serialize all
/// reads and writes via a separate exclusive lock file. This does not lock CCD.
/// </summary>
public sealed class RescueDecisionStore
{
    private const int MaxRecordBytes = 16 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };
    private readonly string _path;
    private readonly string _lockPath;

    public RescueDecisionStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        _lockPath = _path + ".lock";
    }

    /// <summary>Bind the decision to the complete baseline currently supported by the Core journal.</summary>
    public static string ComputeBaselineSha256(SessionJournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.RestoreDisplay?.Topology is null) throw new InvalidDataException("Recovery baseline is missing.");
        var bytes = entry.RestoreAudioBaseline is null
            ? JsonSerializer.SerializeToUtf8Bytes(new { Display = entry.RestoreDisplay, Audio = entry.RestoreAudio })
            : JsonSerializer.SerializeToUtf8Bytes(new { Display = entry.RestoreDisplay, Audio = entry.RestoreAudio, AudioBaseline = entry.RestoreAudioBaseline });
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public async Task<RescueDecisionRecord> InitializeAsync(Guid transactionId, string baselineSha256, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(transactionId, baselineSha256);
        await using var held = await AcquireAsync(cancellationToken).ConfigureAwait(false);
        var current = ReadLocked();
        if (current is not null)
        {
            EnsureIdentity(current, transactionId, baselineSha256);
            return current;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var created = new RescueDecisionRecord(1, transactionId, baselineSha256.ToUpperInvariant(), 0, RescueDecisionState.Armed);
        WriteLocked(created);
        return created;
    }

    public async Task<RescueDecisionRecord?> ReadAsync(Guid transactionId, string baselineSha256, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(transactionId, baselineSha256);
        await using var held = await AcquireAsync(cancellationToken).ConfigureAwait(false);
        var current = ReadLocked();
        if (current is not null) EnsureIdentity(current, transactionId, baselineSha256);
        return current;
    }

    /// <summary>
    /// Executes a synchronous action only while the identified decision is in
    /// the required state. The decision lock remains held until the action
    /// returns, preventing a competing transition between the check and the
    /// action. The action must not re-enter this store and cannot be cancelled
    /// once started. Callers must acquire the CCD mutation lock first.
    /// </summary>
    public async Task ExecuteIfStateAsync(Guid transactionId, string baselineSha256,
        RescueDecisionState expected, Action action, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(transactionId, baselineSha256);
        if (!Enum.IsDefined(expected)) throw new ArgumentOutOfRangeException(nameof(expected));
        ArgumentNullException.ThrowIfNull(action);
        await using var held = await AcquireAsync(cancellationToken).ConfigureAwait(false);
        var current = ReadLocked() ?? throw new RescueDecisionUncertainException("Rescue decision is missing.");
        EnsureIdentity(current, transactionId, baselineSha256);
        if (current.State != expected) throw new RescueDecisionUncertainException("Rescue transaction does not permit display mutation.");
        cancellationToken.ThrowIfCancellationRequested();
        action();
    }

    /// <summary>
    /// Returns false when another decision won. Retrying the same transition
    /// after its successful write returns true without rewriting the record.
    /// </summary>
    public async Task<bool> TryTransitionAsync(Guid transactionId, string baselineSha256,
        RescueDecisionState expected, RescueDecisionState next, CancellationToken cancellationToken = default)
    {
        ValidateIdentity(transactionId, baselineSha256);
        if (!IsTransitionAllowed(expected, next)) throw new ArgumentException("Invalid rescue decision transition.");
        await using var held = await AcquireAsync(cancellationToken).ConfigureAwait(false);
        var current = ReadLocked() ?? throw new InvalidOperationException("Rescue decision is missing.");
        EnsureIdentity(current, transactionId, baselineSha256);
        if (current.State == next) return true;
        if (current.State != expected) return false;
        cancellationToken.ThrowIfCancellationRequested();
        WriteLocked(current with { State = next, Revision = checked(current.Revision + 1) });
        return true;
    }

    private async Task<FileStream> AcquireAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException ex) when ((ex.HResult & 0xFFFF) is 32 or 33)
            {
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private RescueDecisionRecord? ReadLocked()
    {
        FileStream opened;
        try { opened = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read); }
        catch (FileNotFoundException) { return null; }
        using var stream = opened;
        if (stream.Length > MaxRecordBytes) throw new InvalidDataException("Rescue decision exceeds size limit.");
        RescueDecisionRecord record;
        try { record = JsonSerializer.Deserialize<RescueDecisionRecord>(stream, JsonOptions) ?? throw new InvalidDataException("Empty rescue decision."); }
        catch (JsonException ex) { throw new InvalidDataException("Invalid rescue decision.", ex); }
        ValidateRecord(record);
        return record;
    }

    private void WriteLocked(RescueDecisionRecord record)
    {
        var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, record, JsonOptions);
                stream.Flush(flushToDisk: true);
            }
            // Same-directory atomic replacement. File flush does not guarantee
            // directory metadata persistence across every power-loss scenario.
            File.Move(temp, _path, overwrite: true);
        }
        finally
        {
            try { File.Delete(temp); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static bool IsTransitionAllowed(RescueDecisionState from, RescueDecisionState to) =>
        (from == RescueDecisionState.Armed && to is RescueDecisionState.Committed or RescueDecisionState.Aborted or RescueDecisionState.RecoveryRequested) ||
        (from == RescueDecisionState.RecoveryRequested && to == RescueDecisionState.Recovered);

    private static void ValidateRecord(RescueDecisionRecord record)
    {
        if (record.SchemaVersion != 1 || !Enum.IsDefined(record.State)) throw new InvalidDataException("Unsupported rescue decision.");
        try { ValidateIdentity(record.TransactionId, record.BaselineSha256); }
        catch (ArgumentException ex) { throw new InvalidDataException("Invalid rescue decision identity.", ex); }
        var requiredRevision = record.State switch
        {
            RescueDecisionState.Armed => 0,
            RescueDecisionState.Recovered => 2,
            _ => 1,
        };
        if (record.Revision != requiredRevision) throw new InvalidDataException("Invalid rescue decision revision.");
    }

    private static void ValidateIdentity(Guid transactionId, string baselineSha256)
    {
        if (transactionId == Guid.Empty) throw new ArgumentException("Transaction ID is empty.", nameof(transactionId));
        if (baselineSha256 is null || baselineSha256.Length != 64 || !baselineSha256.All(Uri.IsHexDigit))
            throw new ArgumentException("Baseline SHA-256 must contain 64 hexadecimal characters.", nameof(baselineSha256));
    }

    private static void EnsureIdentity(RescueDecisionRecord record, Guid transactionId, string baselineSha256)
    {
        if (record.TransactionId != transactionId || !string.Equals(record.BaselineSha256, baselineSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Rescue decision identity mismatch.");
    }
}
