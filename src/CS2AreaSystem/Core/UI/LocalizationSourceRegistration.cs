using System;
using System.Collections.Generic;
namespace CS2AreaSystem.Core.UI
{
    // Tracks only successfully added sources so a partial failure never blocks other locales or disposal.
    public sealed class LocalizationSourceRegistration<TSource> where TSource : class
    {
        private readonly List<KeyValuePair<string, TSource>> added = new List<KeyValuePair<string, TSource>>();
        public int Count => added.Count;
        public int Register(IEnumerable<string> locales, Func<string, TSource> create, Action<string, TSource> add, Action<string, Exception> report)
        {
            int registered = 0;
            foreach (var locale in locales)
            {
                try
                {
                    var source = create(locale);
                    add(locale, source);
                    added.Add(new KeyValuePair<string, TSource>(locale, source));
                    registered++;
                }
                catch (Exception error) { report(locale, error); }
            }
            return registered;
        }
        public void Unregister(Action<string, TSource> remove, Action<string, Exception> report)
        {
            foreach (var entry in added)
            {
                try { remove(entry.Key, entry.Value); }
                catch (Exception error) { report(entry.Key, error); }
            }
            added.Clear();
        }
    }
}
