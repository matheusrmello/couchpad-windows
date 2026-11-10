namespace CouchPad.Platform.Windows;

/// <summary>Runs a display change with mandatory rollback after an attempted mutation.</summary>
public static class DisplayTransaction
{
    public static void Execute(Action validate, Action apply, Action verify, Action rollback, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validate);
        ArgumentNullException.ThrowIfNull(apply);
        ArgumentNullException.ThrowIfNull(verify);
        ArgumentNullException.ThrowIfNull(rollback);
        cancellationToken.ThrowIfCancellationRequested();
        validate();
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            apply();
            cancellationToken.ThrowIfCancellationRequested();
            verify();
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception original)
        {
            try { rollback(); }
            catch (Exception rollbackFailure) { throw new AggregateException("Display change and rollback failed.", original, rollbackFailure); }
            throw;
        }
    }
}
