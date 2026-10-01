using System;
using System.Collections.Generic;
using System.Linq;
namespace CS2AreaSystem
{
    // Registers the systems of one module as a unit. Game.UpdateSystem (1.6.2f1) has no API to unregister a system, so a
    // failure part-way through disables every system of the module through ComponentSystemBase.Enabled, the same switch the
    // game's GameSystemBase uses to isolate a failing system: a module runs completely or not at all, never partially
    // (for example Area finance without its save flush). Mod.OnLoad calls Step() after each successful UpdateSystem call.
    public sealed class ModuleRegistration
    {
        private readonly Type[] systems;
        public string Name { get; }
        public IReadOnlyList<Type> Systems => systems;
        public int Registered { get; private set; }
        public bool Succeeded { get; private set; }
        public bool Failed { get; private set; }
        public ModuleRegistration(string name, params Type[] systems)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            if (systems == null || systems.Length == 0) throw new ArgumentException("A module registers at least one system.", nameof(systems));
            this.systems = systems;
        }
        public void Step()
        {
            if (Failed || Succeeded || Registered >= systems.Length) throw new InvalidOperationException(Name + ": unexpected registration step");
            Registered++;
        }
        // True once every system of the module was registered.
        public bool Complete()
        {
            if (Failed) return false;
            if (Registered != systems.Length) throw new InvalidOperationException(Name + ": " + Registered + " of " + systems.Length + " systems registered");
            return Succeeded = true;
        }
        // Disables every system of the module (registered ones would otherwise keep running) and reports exactly which
        // systems were registered before the failure. A system that cannot be disabled is reported too.
        public void Fail(Exception error, Action<Type> disable, Action<Exception, string> report)
        {
            Failed = true;
            Succeeded = false;
            foreach (var system in systems)
            {
                try { disable(system); }
                catch (Exception disableError) { report(disableError, Name + ": " + system.Name + " could not be disabled after the registration failure"); }
            }
            report(error, Describe());
        }
        public string Describe()
        {
            if (!Failed) return Name + (Succeeded ? " registered" : " pending");
            var registered = systems.Take(Registered).Select(t => t.Name).ToArray();
            var missing = systems.Skip(Registered).Select(t => t.Name).ToArray();
            return Name + " unavailable: registration failed at " + (missing.Length > 0 ? missing[0] : "completion") +
                "; registered before the failure and now disabled: [" + string.Join(", ", registered) +
                "]; not registered: [" + string.Join(", ", missing) + "]. Other modules are unaffected.";
        }
        // A dependent module is registered only when its prerequisite registered completely.
        public static bool Requires(ModuleRegistration prerequisite, string dependent, Action<string> report)
        {
            if (prerequisite != null && prerequisite.Succeeded) return true;
            report?.Invoke(dependent + " not registered: it requires " + (prerequisite?.Name ?? "an unavailable module") + ", which is unavailable.");
            return false;
        }
    }
}
