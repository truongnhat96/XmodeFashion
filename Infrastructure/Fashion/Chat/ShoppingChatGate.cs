using System.Collections.Concurrent;

namespace Infrastructure.Fashion.Chat;

public sealed class ShoppingChatGate
{
    private readonly ConcurrentDictionary<string, byte> active = new();
    private readonly SemaphoreSlim total = new(4);
    public bool TryEnter(string session)
    {
        if (!active.TryAdd(session, 0)) return false;
        if (total.Wait(0)) return true;
        active.TryRemove(session, out _); return false;
    }
    public void Leave(string session) { if (active.TryRemove(session, out _)) total.Release(); }
}
