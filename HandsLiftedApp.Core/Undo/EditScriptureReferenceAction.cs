using System;
using System.Linq;
using System.Threading.Tasks;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.Models.RuntimeData.Items;
using HandsLiftedApp.Data.Models.Items;
using Serilog;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>The passage a scripture item points at, plus the title derived from it.</summary>
    public readonly record struct ScriptureReference(
        string Book, int StartChapter, int StartVerse, int EndChapter, int EndVerse, string Translation, string Title)
    {
        public static ScriptureReference Of(ScriptureItem item) => new(
            item.Book, item.StartChapter, item.StartVerse, item.EndChapter, item.EndVerse, item.Translation, item.Title);
    }

    /// <summary>
    /// Changes which passage a scripture item shows. Several fields change together and the slides have to be
    /// regenerated afterwards, so this is one explicit step rather than something the generic property recorder
    /// could capture field by field.
    /// </summary>
    public sealed class EditScriptureReferenceAction : IUndoableAction
    {
        private readonly PlaylistInstance _playlist;
        private readonly Guid _itemId;
        private readonly ScriptureReference _old;
        private readonly ScriptureReference _new;

        public EditScriptureReferenceAction(PlaylistInstance playlist, ScriptureItemInstance item,
            ScriptureReference newReference)
        {
            _playlist = playlist;
            _itemId = item.UUID;
            _old = ScriptureReference.Of(item);
            _new = newReference;
        }

        public string Description => "Edit Scripture";

        public void Do() => Apply(_new);

        public void Undo() => Apply(_old);

        private void Apply(ScriptureReference reference)
        {
            var item = _playlist.Items.FirstOrDefault(i => i.UUID == _itemId) as ScriptureItemInstance
                       ?? throw new InvalidOperationException($"Scripture item {_itemId} is no longer in the playlist");

            item.Book = reference.Book;
            item.StartChapter = reference.StartChapter;
            item.StartVerse = reference.StartVerse;
            item.EndChapter = reference.EndChapter;
            item.EndVerse = reference.EndVerse;
            item.Translation = reference.Translation;
            item.Title = reference.Title;

            // forceInvalidateCache: UpdatePages reuses existing ScriptureSlideInstances by page index and only
            // resets a reused slide's Cached bitmap when the resolved theme changed; an edited verse range can
            // produce the same page count with entirely different text, which that check alone would miss,
            // leaving a stale cached thumbnail.
            _ = item.GenerateSlidesAsync(forceInvalidateCache: true).ContinueWith(
                t => Log.Error(t.Exception, "Failed to generate scripture slides for {Title}", item.Title),
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
