using CouchPad.Core.Automation;

namespace CouchPad.Platform.Windows;

/// <summary>Shares one guarded transaction between the composition and its display service.</summary>
public sealed class RescueTransactionContext
{
    private readonly object _sync = new();
    private Binding? _binding;
    public string MutationLockPath { get; }

    public RescueTransactionContext(string stateDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);
        MutationLockPath = Path.Combine(Path.GetFullPath(stateDirectory), "display-mutation.lock");
    }

    internal void Bind(string decisionPath, Guid transactionId, string baselineSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionPath);
        if (transactionId == Guid.Empty) throw new ArgumentException("Missing transaction ID.", nameof(transactionId));
        lock (_sync)
        {
            if (_binding is not null) throw new InvalidOperationException("A rescue transaction is already bound.");
            _binding = new Binding(new RescueDecisionStore(decisionPath), transactionId, baselineSha256);
        }
    }

    internal void Release(Guid transactionId)
    {
        lock (_sync)
        {
            if (_binding is null || _binding.TransactionId != transactionId) throw new InvalidOperationException("Rescue transaction mismatch.");
            _binding = null;
        }
    }

    internal async Task RequireAsync(RescueDecisionState expected, CancellationToken cancellationToken = default)
    {
        Binding binding;
        lock (_sync) { binding = _binding ?? throw new RescueDecisionUncertainException("No rescue transaction is bound."); }
        try
        {
            var decision = await binding.Store.ReadAsync(binding.TransactionId, binding.BaselineSha256, cancellationToken).ConfigureAwait(false);
            if (decision?.State != expected) throw new RescueDecisionUncertainException("Rescue transaction does not permit display mutation.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (RescueDecisionUncertainException) { throw; }
        catch (Exception ex) { throw new RescueDecisionUncertainException("Rescue decision could not be verified.", ex); }
    }

    internal async Task ExecuteIfStateAsync(RescueDecisionState expected, Action action, CancellationToken cancellationToken = default)
    {
        Binding binding;
        lock (_sync) { binding = _binding ?? throw new RescueDecisionUncertainException("No rescue transaction is bound."); }
        var started = false;
        try
        {
            await binding.Store.ExecuteIfStateAsync(binding.TransactionId, binding.BaselineSha256, expected,
                () => { started = true; action(); }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (!started && ex is not RescueDecisionUncertainException)
        {
            throw new RescueDecisionUncertainException("Rescue decision could not be verified.", ex);
        }
    }

    private sealed record Binding(RescueDecisionStore Store, Guid TransactionId, string BaselineSha256);
}
