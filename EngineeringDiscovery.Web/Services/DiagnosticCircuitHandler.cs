using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace EngineeringDiscovery.Web.Services
{
    // Temporary diagnostic CircuitHandler to log circuit lifecycle events for troubleshooting.
    // Logging is Console-only and this class is intended to be removed after diagnosis.
    public sealed class DiagnosticCircuitHandler : CircuitHandler
    {
        public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            try { Console.WriteLine($"[CIRCUIT] Opened {DateTime.UtcNow:o} CircuitId={circuit.Id}"); } catch { }
            return Task.CompletedTask;
        }

        public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            try { Console.WriteLine($"[CIRCUIT] Closed {DateTime.UtcNow:o} CircuitId={circuit.Id}"); } catch { }
            return Task.CompletedTask;
        }

        public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            try { Console.WriteLine($"[CIRCUIT] ConnectionUp {DateTime.UtcNow:o} CircuitId={circuit.Id}"); } catch { }
            return Task.CompletedTask;
        }

        public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            try { Console.WriteLine($"[CIRCUIT] ConnectionDown {DateTime.UtcNow:o} CircuitId={circuit.Id}"); } catch { }
            return Task.CompletedTask;
        }
    }
}
