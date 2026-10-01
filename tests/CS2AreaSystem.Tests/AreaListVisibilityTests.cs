using CS2AreaSystem.Core.UI;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
public class AreaListVisibilityTests {
 [Test] public void CollapsedPreferenceKeepsSelectionAvailableAndAllowsOpening() {
  var state = new AreaListVisibility();
  state.Update(true, false);
  Assert.That(state.Available, Is.True);
  Assert.That(state.Open, Is.False);
  state.SetOpen(true);
  state.Update(true, false);
  Assert.That(state.Open, Is.True, "the next update must not undo the player's expansion");
 }
 [Test] public void ExpandedPreferenceDoesNotUndoManualCollapse() {
  var state = new AreaListVisibility();
  state.Update(true, true);
  Assert.That(state.Open, Is.True);
  state.SetOpen(false);
  state.Update(true, true);
  Assert.That(state.Open, Is.False);
 }
 [Test] public void ChangedPreferenceAppliesOnceAndKeepsTheButtonAvailable() {
  var state = new AreaListVisibility();
  state.Update(true, true);
  state.Update(true, false);
  Assert.That(state.Available, Is.True);
  Assert.That(state.Open, Is.False);
  state.Update(true, true);
  Assert.That(state.Open, Is.True);
 }
 [Test] public void LoadingClearsTheListAndRestoresThePreferenceInTheNextSession() {
  var state = new AreaListVisibility();
  state.Update(true, false); state.SetOpen(true);
  state.Update(false, false); state.SetOpen(true);
  Assert.That(state.Available, Is.False);
  Assert.That(state.Open, Is.False);
  state.Update(true, false);
  Assert.That(state.Available, Is.True);
  Assert.That(state.Open, Is.False);
 }
}
