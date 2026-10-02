using System.Reflection;
using CS2AreaSystem.Core.UI;
using CS2AreaSystem.Tests.Support;
using NUnit.Framework;
namespace CS2AreaSystem.Tests;
// Sections register themselves with SelectedInfoUISystem.AddMiddleSection when created, so creation order is panel order.
// The vanilla district panel shows PoliciesSection before its statistics; the Area panel follows it.
public class AreaSectionOrderTests {
 [Test] public void PolicySectionIsRegisteredBeforeAreaSection() {
  var onCreate=typeof(AreaManagementUISystem).GetMethod("OnCreate",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)!;
  var created=IlCallScanner.Find(onCreate,m=>m.Name=="GetOrCreateSystemManaged"&&m.IsGenericMethod)
   .Select(site=>(type:site.Target.GetGenericArguments()[0],site.Offset)).ToList();
  int policy=created.Single(c=>c.type==typeof(AreaPolicyInfoSection)).Offset;
  int area=created.Single(c=>c.type==typeof(AreaInfoSection)).Offset;
  Assert.That(policy,Is.LessThan(area));
 }
}
