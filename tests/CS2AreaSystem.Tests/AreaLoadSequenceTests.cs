using CS2AreaSystem.Core.Persistence;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaLoadSequenceTests {
    [Test] public void RecoveryWaitsThenObservesRebuildsValidatesExactlyOnceBeforeUpdates() {
        var sequence=new AreaLoadSequence();
        var operations=new List<string>();
        void Recover(bool ready) => sequence.TryRecover(ready,()=>operations.Add("ids"),()=>operations.Add("membership"),()=>operations.Add("activation"));
        Recover(false); Assert.That(sequence.Ready, Is.False); Assert.That(operations, Is.Empty);
        Recover(true); Recover(true);
        Assert.That(operations, Is.EqualTo(new[]{"ids","membership","activation"})); Assert.That(sequence.Ready, Is.True);
        sequence.Reset(); Recover(false); Assert.That(sequence.Ready, Is.False);
        Recover(true); Assert.That(operations.Count, Is.EqualTo(6));
    }
}
