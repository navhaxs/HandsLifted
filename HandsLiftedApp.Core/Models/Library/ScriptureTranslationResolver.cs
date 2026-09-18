using System.Collections.Generic;
using System.Linq;
using HandsLiftedApp.Core.Models.Library.Config;

namespace HandsLiftedApp.Core.Models.Library
{
    public static class ScriptureTranslationResolver
    {
        // Falls back to the first configured translation if the label doesn't match any (e.g.
        // stale ScriptureItem.Translation data from before a translation was renamed/removed);
        // returns "" if none are configured at all, matching ScriptureLocalUsxStore's existing
        // not-found handling (empty root path -> File.Exists false -> graceful placeholder).
        public static string ResolveDirectory(string? translationLabel, IEnumerable<LibraryConfig.LibraryDefinition> configuredTranslations)
        {
            var list = configuredTranslations as IReadOnlyCollection<LibraryConfig.LibraryDefinition>
                       ?? configuredTranslations.ToList();
            var match = list.FirstOrDefault(d => d.Label == translationLabel) ?? list.FirstOrDefault();
            return match?.Directory ?? "";
        }
    }
}
