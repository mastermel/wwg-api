using Wwg.Api.Features.Maps;

namespace Wwg.Api.IntegrationTests.Support;

/// <summary>Dice that fall as the test says, in order (then 1s).</summary>
internal sealed class FixedDice : IDice
{
    private readonly Queue<int> _rolls = new();

    public void WillRoll(params int[] rolls)
    {
        foreach (var roll in rolls)
        {
            _rolls.Enqueue(roll);
        }
    }

    public int D6() => _rolls.TryDequeue(out var roll) ? roll : 1;
}
