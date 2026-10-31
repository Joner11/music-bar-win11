using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace NowPlaying
{
    /// <summary>
    /// CloudMus перестал отдавать название/исполнителя/альбом в SMTC (остались только
    /// статус, таймлайн и обложка). Зато сам плеер пишет текущий трек в свои файлы:
    ///   %LOCALAPPDATA%\cloudmus\fronts\qt\history.json          — последние прослушанные
    ///   %LOCALAPPDATA%\cloudmus\fronts\qt\active-playlist.json  — очередь
    /// Берём оттуда недостающие метаданные.
    /// </summary>
    static class CloudMus
    {
        static readonly object _lock = new object();
        static string _lastPath = "";
        static DateTime _lastWrite = DateTime.MinValue;
        static long _lastLen = -1;

        static string _title = "", _artist = "", _album = "";
        static double _durSec;
        static double _expectedDur;
        static bool _valid;

        static long _activeStamp = -1;
        static bool _activeOk;
        static string _activeTitle = "", _activeArtist = "", _activeAlbum = "";
        static double _activeDur;

        public static bool IsCloudMusApp(string app)
        {
            if (string.IsNullOrEmpty(app)) return false;
            return app.IndexOf("cloudmus", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string StateDir()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(Path.Combine(Path.Combine(local, "cloudmus"), "fronts"), "qt");
        }

        static string ReadAllShared(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs, Encoding.UTF8, true))
                return sr.ReadToEnd();
        }

        /// <summary>Текущий трек по данным самого плеера: config.ini + active-playlist.json.</summary>
        static bool TryFromActive(out string title, out string artist, out string album, out double durSec)
        {
            title = ""; artist = ""; album = ""; durSec = 0;
            string dir = StateDir();
            string cfgPath = Path.Combine(dir, "config.ini");
            string listPath = Path.Combine(dir, "active-playlist.json");
            if (!File.Exists(cfgPath) || !File.Exists(listPath)) return false;

            // Файлы перечитываем только при изменении: опрос идёт каждые 700 мс,
            // а active-playlist.json бывает на сотни килобайт.
            var ci = new FileInfo(cfgPath);
            var li = new FileInfo(listPath);
            long stamp = ci.LastWriteTimeUtc.Ticks ^ (li.LastWriteTimeUtc.Ticks << 1) ^ (li.Length << 3);
            if (stamp == _activeStamp)
            {
                if (!_activeOk) return false;
                title = _activeTitle; artist = _activeArtist;
                album = _activeAlbum; durSec = _activeDur;
                return true;
            }
            _activeStamp = stamp;

            // activeTrackId=141067975:37441641
            string id = "";
            try
            {
                foreach (string line in ReadAllShared(cfgPath).Split('\n'))
                {
                    string t = line.Trim();
                    if (t.StartsWith("activeTrackId=", StringComparison.Ordinal))
                    {
                        id = t.Substring("activeTrackId=".Length).Trim();
                        break;
                    }
                }
            }
            catch { return false; }
            if (id.Length == 0) return false;

            try
            {
                var root = Json.Parse(ReadAllShared(listPath));
                var tracks = Json.Path(root, "tracks") as List<object>;
                if (tracks == null) return false;
                object hit = null;
                foreach (object it in tracks)
                {
                    if (Json.Str(it, "track", "id") == id) { hit = it; break; }
                }
                if (hit == null) return false;

                title = Json.Str(hit, "track", "title").Trim();
                if (title.Length == 0) return false;
                album = Json.Str(hit, "track", "album", "title").Trim();
                double ms = Json.Num(hit, "track", "durationMs");
                durSec = ms > 0 ? ms / 1000.0 : 0;

                var artists = Json.Path(hit, "track", "artists") as List<object>;
                var names = new List<string>();
                if (artists != null)
                    foreach (object a in artists)
                    {
                        string n = Json.Str(a, "name").Trim();
                        if (n.Length > 0) names.Add(n);
                    }
                artist = string.Join(", ", names.ToArray());
                _activeOk = true; _activeTitle = title; _activeArtist = artist;
                _activeAlbum = album; _activeDur = durSec;
                return true;
            }
            catch { _activeOk = false; return false; }
        }

        static object Pick(List<object> list, double expectedDur)
        {
            int limit = list.Count < 6 ? list.Count : 6;
            if (expectedDur > 5)
            {
                for (int i = 0; i < limit; i++)
                {
                    double d = Json.Num(list[i], "track", "durationMs") / 1000.0;
                    if (d > 0 && Math.Abs(d - expectedDur) < 1.5) return list[i];
                }
            }
            return list[0];
        }

        static void Load()
        {
            string path = Path.Combine(StateDir(), "history.json");
            FileInfo fi = new FileInfo(path);
            if (!fi.Exists) { _valid = false; return; }
            if (path == _lastPath && fi.LastWriteTimeUtc == _lastWrite && fi.Length == _lastLen) return;

            try
            {
                string text = ReadAllShared(path);
                var list = Json.Parse(text) as List<object>;
                if (list == null || list.Count == 0) { _valid = false; return; }

                // В истории первым идёт последний прослушанный трек, но он может
                // отставать на пару секунд или, наоборот, забегать вперёд. Если
                // SMTC отдаёт длительность, выбираем запись с такой же — это
                // надёжнее, чем «просто первая».
                object e = Pick(list, _expectedDur);
                string playedAt = Json.Str(e, "playedAt");
                string title = Json.Str(e, "track", "title").Trim();
                if (title.Length == 0) { _valid = false; return; }

                var artists = Json.Path(e, "track", "artists") as List<object>;
                var names = new List<string>();
                if (artists != null)
                    foreach (object a in artists)
                    {
                        string n = Json.Str(a, "name").Trim();
                        if (n.Length > 0) names.Add(n);
                    }

                double durMs = Json.Num(e, "track", "durationMs");

                _title = title;
                _artist = string.Join(", ", names.ToArray());
                _album = Json.Str(e, "track", "album", "title").Trim();
                _durSec = durMs > 0 ? durMs / 1000.0 : 0;

                _lastPath = path;
                _lastWrite = fi.LastWriteTimeUtc;
                _lastLen = fi.Length;

                // Запись считается актуальной, пока не истекло время трека (+ минута
                // на паузы). Иначе это трек из прошлого запуска, и подставлять его нельзя.
                DateTime when;
                if (!DateTime.TryParse(playedAt, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out when))
                    when = fi.LastWriteTimeUtc;
                double age = (DateTime.UtcNow - when).TotalSeconds;
                _valid = age >= -5 && (_durSec <= 0 ? age < 3600 : age <= _durSec + 60);
            }
            catch
            {
                _valid = false;
            }
        }

        /// <summary>Дополняет трек метаданными из состояния CloudMus. true, если что-то подставили.</summary>
        public static bool TryFill(TrackInfo t)
        {
            if (t == null || !IsCloudMusApp(t.App)) return false;
            lock (_lock)
            {
                // Длительность из SMTC — ключ для выбора нужной записи в истории.
                if (t.DurationSec > 5) _expectedDur = t.DurationSec;

                // 1) Самый точный источник — config.ini + active-playlist.json:
                //    плеер держит там именно текущий трек.
                string title, artist, album;
                double dur;
                if (TryFromActive(out title, out artist, out album, out dur))
                {
                    t.Title = title; t.Artist = artist; t.Album = album;
                    if (t.DurationSec <= 5 && dur > 0)
                    {
                        t.DurationSec = dur;
                        t.TimelineAt = DateTime.UtcNow;
                    }
                    return true;
                }

                // 2) Иначе — история прослушивания.
                Load();
                if (!_valid || _title.Length == 0) return false;

                t.Title = _title;
                t.Artist = _artist;
                t.Album = _album;
                if (t.DurationSec <= 0 && _durSec > 0)
                {
                    t.DurationSec = _durSec;
                    t.PositionSec = 0;
                    t.TimelineAt = DateTime.UtcNow;
                }
                return true;
            }
        }
    }
}
