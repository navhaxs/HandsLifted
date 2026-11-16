using System;
using HandsLiftedApp.Core.Models;
using HandsLiftedApp.Core.ViewModels;

namespace HandsLiftedApp.Core.Undo
{
    /// <summary>
    /// "Set as default for Songs / Songs (motion bg) / Scripture" in the theme designer: makes the theme the
    /// app-wide default (saved in the app preferences straight away) and clears the open playlist's own override
    /// of that default. Undo restores both the app preference and the playlist override.
    ///
    /// The playlist's own undo recorder is told to stand aside while this runs: the change belongs to the theme
    /// designer's history, and the playlist's history must not also pick up the override being cleared.
    /// </summary>
    public sealed class SetDefaultThemeAction : IUndoableAction
    {
        private readonly AppPreferencesViewModel _prefs;
        private readonly PlaylistInstance _playlist;
        private readonly DefaultThemeKind _kind;
        private readonly Guid _newId;
        private readonly Action _save;
        private readonly Guid? _oldAppDefault;
        private readonly Guid? _oldPlaylistOverride;

        public SetDefaultThemeAction(AppPreferencesViewModel prefs, PlaylistInstance playlist, DefaultThemeKind kind,
            Guid newThemeId, Action save)
        {
            _prefs = prefs;
            _playlist = playlist;
            _kind = kind;
            _newId = newThemeId;
            _save = save;
            _oldAppDefault = GetApp();
            _oldPlaylistOverride = GetPlaylist();
            Description = kind switch
            {
                DefaultThemeKind.Song => "Set Default Song Theme",
                DefaultThemeKind.SongMotionBackground => "Set Default Song Theme (motion)",
                _ => "Set Default Scripture Theme",
            };
        }

        public string Description { get; }

        public void Do() => Write(_newId, null);
        public void Undo() => Write(_oldAppDefault, _oldPlaylistOverride);

        private void Write(Guid? appDefault, Guid? playlistOverride)
        {
            using (_playlist.PropertyRecorder.Suppress())
            {
                SetApp(appDefault);
                SetPlaylist(playlistOverride);
            }

            _save();
        }

        private Guid? GetApp() => _kind switch
        {
            DefaultThemeKind.Song => _prefs.DefaultSongThemeId,
            DefaultThemeKind.SongMotionBackground => _prefs.DefaultSongMotionThemeId,
            _ => _prefs.DefaultScriptureThemeId,
        };

        private Guid? GetPlaylist() => _kind switch
        {
            DefaultThemeKind.Song => _playlist.DefaultSongThemeId,
            DefaultThemeKind.SongMotionBackground => _playlist.DefaultSongMotionThemeId,
            _ => _playlist.DefaultScriptureThemeId,
        };

        private void SetApp(Guid? id)
        {
            switch (_kind)
            {
                case DefaultThemeKind.Song: _prefs.DefaultSongThemeId = id; break;
                case DefaultThemeKind.SongMotionBackground: _prefs.DefaultSongMotionThemeId = id; break;
                default: _prefs.DefaultScriptureThemeId = id; break;
            }
        }

        private void SetPlaylist(Guid? id)
        {
            switch (_kind)
            {
                case DefaultThemeKind.Song: _playlist.DefaultSongThemeId = id; break;
                case DefaultThemeKind.SongMotionBackground: _playlist.DefaultSongMotionThemeId = id; break;
                default: _playlist.DefaultScriptureThemeId = id; break;
            }
        }
    }
}
