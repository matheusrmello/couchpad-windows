using CouchPad.Core.Automation;

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
        catch (RescueDecisionUncertainException)
        {
            // A different process may already own recovery; do not race it.
            throw;
        }
        catch (Exception original)
        {
            try { rollback(); }
            catch (RescueDecisionUncertainException uncertain)
            {
                throw new RescueDecisionUncertainException("Rollback ownership is uncertain.", new AggregateException(original, uncertain));
            }
            catch (Exception rollbackFailure) { throw new AggregateException("Display change and rollback failed.", original, rollbackFailure); }
            throw;
        }
    }
}
