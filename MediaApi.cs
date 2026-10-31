using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;

namespace NowPlaying
{
    class TrackInfo
    {
        public string App = "";
        public string Title = "";
        public string Artist = "";
        public string Album = "";
        public int Status;          // 0 Closed,1 Opened,2 Changing,3 Stopped,4 Playing,5 Paused
        public object Session;

        public double PositionSec;
        public double DurationSec;
        public DateTime TimelineAt = DateTime.UtcNow;

        /// <summary>Данные взяты из кэша: плеер сейчас отдаёт пустые метаданные.</summary>
        public bool Stale;

        /// <summary>Позиция воспроизведения с учётом времени, прошедшего с последнего опроса.</summary>
        public double CurrentPosition
        {
            get
            {
                if (DurationSec <= 0) return 0;
                double p = PositionSec;
                if (IsPlaying) p += (DateTime.UtcNow - TimelineAt).TotalSeconds;
                if (p < 0) p = 0;
                if (p > DurationSec) p = DurationSec;
                return p;
            }
        }

        public string Key { get { return App + "\u0001" + Title + "\u0001" + Artist + "\u0001" + Album; } }
        public bool IsPlaying { get { return Status == 4; } }

        /// <summary>В сессии есть хоть какие-то метаданные (не пустая заглушка).</summary>
        public bool HasText { get { return Title.Length > 0 || Artist.Length > 0; } }

        public string AppName
        {
            get
            {
                var a = App ?? "";
                int bang = a.IndexOf('!');
                if (bang > 0) a = a.Substring(0, bang);          // AUMID пакета: "Microsoft.ZuneMusic_8wekyb3d8bbwe!App"
                else
                {
                    int slash = a.LastIndexOfAny(new[] { '\\', '/' });
                    if (slash >= 0) a = a.Substring(slash + 1);
                }
                int underscore = a.IndexOf('_');
                if (underscore > 0 && bang > 0) a = a.Substring(0, underscore);
                if (a.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) a = a.Substring(0, a.Length - 4);
                // "CloudMus.CloudMus" — некоторые приложения регистрируют AUMID из двух
                // одинаковых половин, в подписи это выглядит мусором.
                int dot = a.IndexOf('.');
                if (dot > 0 && dot < a.Length - 1)
                {
                    string left = a.Substring(0, dot), right = a.Substring(dot + 1);
                    if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) a = left;
                }
                return a.Trim();
            }
        }

        public string SecondText(Config cfg)
        {
            switch (cfg.SecondLine)
            {
                case "none": return "";
                case "album": return Album;
                case "app": return AppName;
                case "artist_app":
                    if (Artist.Length > 0 && AppName.Length > 0) return Artist + "  ·  " + AppName;
                    return Artist.Length > 0 ? Artist : AppName;
                case "app_artist":
                    if (Artist.Length > 0 && AppName.Length > 0) return AppName + "  ·  " + Artist;
                    return Artist.Length > 0 ? Artist : AppName;
                default:
                    if (Artist.Length > 0) return Artist;
                    if (Album.Length > 0) return Album;
                    return AppName;
            }
        }
    }

    static class MediaApi
    {
        static Type _mgrT, _propsT, _streamT, _thumbRefT;
        static MethodInfo _asTask;
        static MethodInfo _asStreamForRead;
        static object _mgr;
        static DateTime _mgrBirth = DateTime.MinValue;
        static readonly object _lock = new object();
        static int _initFailed;

        public static string LastError = "";

        static bool Init()
        {
            if (_mgrT != null) return true;
            if (_initFailed > 3) return false;
            try
            {
                _mgrT = Type.GetType("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media, ContentType=WindowsRuntime");
                _propsT = Type.GetType("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties, Windows.Media, ContentType=WindowsRuntime");
                _streamT = Type.GetType("Windows.Storage.Streams.IRandomAccessStreamWithContentType, Windows.Storage, ContentType=WindowsRuntime");
                _thumbRefT = Type.GetType("Windows.Storage.Streams.RandomAccessStreamReference, Windows.Storage, ContentType=WindowsRuntime");
                if (_mgrT == null || _propsT == null || _streamT == null) { _initFailed++; return false; }

                foreach (var m in typeof(System.WindowsRuntimeSystemExtensions).GetMethods())
                {
                    if (m.Name == "AsTask" && m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType.Name == "IAsyncOperation`1") { _asTask = m; break; }
                }
                // ВАЖНО: перегрузка AsStreamForRead принимает IInputStream (а не IRandomAccessStream),
                // поэтому ищем строго по имени типа параметра и по количеству аргументов.
                foreach (var m in typeof(System.IO.WindowsRuntimeStreamExtensions).GetMethods())
                {
                    if (m.Name != "AsStreamForRead") continue;
                    var ps = m.GetParameters();
                    if (ps.Length != 1) continue;
                    if (ps[0].ParameterType.Name != "IInputStream") continue;
                    if (m.ReturnType != typeof(Stream)) continue;
                    _asStreamForRead = m;
                    break;
                }
                if (_asStreamForRead == null)
                {
                    // запасной путь: AsStream(IRandomAccessStream)
                    foreach (var m in typeof(System.IO.WindowsRuntimeStreamExtensions).GetMethods())
                    {
                        if (m.Name != "AsStream") continue;
                        var ps = m.GetParameters();
                        if (ps.Length != 1) continue;
                        if (ps[0].ParameterType.Name != "IRandomAccessStream") continue;
                        if (m.ReturnType != typeof(Stream)) continue;
                        _asStreamForRead = m;
                        break;
                    }
                }
                if (_asStreamForRead == null) { _initFailed++; LastError = "no AsStreamForRead"; return false; }
                return _asTask != null;
            }
            catch (Exception ex)
            {
                _initFailed++;
                LastError = "" + ex.Message;
                return false;
            }
        }

        static object Await(object op, Type t, int timeoutMs)
        {
            var task = (System.Threading.Tasks.Task)_asTask.MakeGenericMethod(t).Invoke(null, new object[] { op });
            if (!task.Wait(timeoutMs)) throw new TimeoutException("WinRT timeout");
            return task.GetType().GetProperty("Result").GetValue(task);
        }

        static object Manager()
        {
            if (_mgr != null && (DateTime.UtcNow - _mgrBirth).TotalMinutes < 30) return _mgr;
            var req = _mgrT.GetMethod("RequestAsync").Invoke(null, null);
            _mgr = Await(req, _mgrT, 4000);
            _mgrBirth = DateTime.UtcNow;
            return _mgr;
        }

        public static void InvalidateManager() { _mgr = null; }

        // Последние непустые метаданные по каждому плееру. Некоторые приложения
        // (например CloudMus) периодически отдают пустые Title/Artist, хотя музыка
        // играет — тогда показываем последнее известное, а не «Без названия».
        class CachedProps
        {
            public string Title = "", Artist = "", Album = "";
            public DateTime At = DateTime.UtcNow;
        }

        static readonly Dictionary<string, CachedProps> _cache = new Dictionary<string, CachedProps>();
        const double CacheTtlSec = 300;

        static CachedProps CacheGet(string app)
        {
            CachedProps c;
            if (app == null) return null;
            if (!_cache.TryGetValue(app, out c)) return null;
            if ((DateTime.UtcNow - c.At).TotalSeconds > CacheTtlSec) { _cache.Remove(app); return null; }
            return c;
        }

        static void CachePut(TrackInfo t)
        {
            if (t == null || t.App == null || !t.HasText) return;
            var c = new CachedProps();
            c.Title = t.Title; c.Artist = t.Artist; c.Album = t.Album;
            _cache[t.App] = c;
        }

        /// <summary>Читает метаданные одной сессии. Возвращает null при ошибке/таймауте.</summary>
        static object FetchProps(object session, int timeoutMs)
        {
            try
            {
                var st = session.GetType();
                var op = st.GetMethod("TryGetMediaPropertiesAsync").Invoke(session, null);
                return Await(op, _propsT, timeoutMs);
            }
            catch { return null; }
        }

        /// <summary>Читает список сессий SMTC. Возвращает null при фатальной ошибке.</summary>
        public static List<TrackInfo> Read()
        {
            lock (_lock)
            {
                if (!Init()) return null;
                var list = new List<TrackInfo>();
                try
                {
                    var mgr = Manager();
                    var sessions = (IEnumerable)_mgrT.GetMethod("GetSessions").Invoke(mgr, null);
                    if (sessions == null) return list;
                    // Общий бюджет на опрос: один зависший плеер не должен
                    // растягивать Read() на десятки секунд.
                    var deadline = DateTime.UtcNow.AddMilliseconds(2200);
                    int guard = 0;
                    foreach (object s in sessions)
                    {
                        if (++guard > 12) break;
                        if (s == null) continue;
                        var st = s.GetType();
                        var t = new TrackInfo();
                        t.Session = s;
                        try { t.App = (string)st.GetProperty("SourceAppUserModelId").GetValue(s, null) ?? ""; } catch { }
                        try
                        {
                            var pi = st.GetMethod("GetPlaybackInfo").Invoke(s, null);
                            t.Status = Convert.ToInt32(pi.GetType().GetProperty("PlaybackStatus").GetValue(pi, null));
                        }
                        catch { t.Status = 0; }

                        try
                        {
                            var tl = st.GetMethod("GetTimelineProperties").Invoke(s, null);
                            var tlt = tl.GetType();
                            var startT = (TimeSpan)tlt.GetProperty("StartTime").GetValue(tl, null);
                            var endT = (TimeSpan)tlt.GetProperty("EndTime").GetValue(tl, null);
                            var posT = (TimeSpan)tlt.GetProperty("Position").GetValue(tl, null);
                            t.PositionSec = posT.TotalSeconds - startT.TotalSeconds;
                            t.DurationSec = endT.TotalSeconds - startT.TotalSeconds;
                            t.TimelineAt = DateTime.UtcNow;
                        }
                        catch { }

                        object props = null;
                        int leftMs = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                        if (leftMs > 150) props = FetchProps(s, Math.Min(1200, leftMs));
                        if (props != null)
                        {
                            var pt = props.GetType();
                            try { t.Title = ((string)pt.GetProperty("Title").GetValue(props, null) ?? "").Trim(); } catch { }
                            try { t.Artist = ((string)pt.GetProperty("Artist").GetValue(props, null) ?? "").Trim(); } catch { }
                            try { t.Album = ((string)pt.GetProperty("AlbumTitle").GetValue(props, null) ?? "").Trim(); } catch { }
                        }

                        if (t.HasText)
                        {
                            CachePut(t);
                        }
                        else
                        {
                            // Плеер молчит по метаданным (CloudMus и подобные умеют так
                            // делать). Если раньше он же отдавал нормальный трек —
                            // показываем его, иначе виджет превращается в «Без названия».
                            var c = CacheGet(t.App);
                            if (c != null)
                            {
                                t.Title = c.Title; t.Artist = c.Artist; t.Album = c.Album;
                                t.Stale = true;
                            }
                        }
                        list.Add(t);
                    }
                }
                catch (Exception ex)
                {
                    LastError = "" + ex.Message;
                    _mgr = null;
                }
                return list;
            }
        }

        /// <summary>Загружает обложку для сессии. Возвращает null, если обложки нет/ещё не готова.</summary>
        public static Image ReadThumbnail(object session, int timeoutMs)
        {
            lock (_lock)
            {
                if (!Init() || session == null) return null;
                try
                {
                    var st = session.GetType();
                    var op = st.GetMethod("TryGetMediaPropertiesAsync").Invoke(session, null);
                    var props = Await(op, _propsT, timeoutMs);
                    if (props == null) return null;
                    var pt = props.GetType();
                    var thumbRef = pt.GetProperty("Thumbnail").GetValue(props, null);
                    if (thumbRef == null) return null;

                    var op2 = thumbRef.GetType().GetMethod("OpenReadAsync").Invoke(thumbRef, null);
                    var stream = Await(op2, _streamT, timeoutMs);
                    if (stream == null || _asStreamForRead == null) return null;

                    var net = (Stream)_asStreamForRead.Invoke(null, new object[] { stream });
                    if (net == null) return null;
                    using (var ms = new MemoryStream())
                    {
                        net.CopyTo(ms);
                        if (ms.Length == 0) return null;
                        ms.Position = 0;
                        using (var img = Image.FromStream(ms))
                            return new Bitmap(img);
                    }
                }
                catch (Exception ex)
                {
                    LastError = "thumb: " + ex.Message;
                    return null;
                }
            }
        }

        public static void TogglePlayPause(object session)
        {
            try
            {
                if (session == null) return;
                var m = session.GetType().GetMethod("TryTogglePlayPauseAsync");
                if (m != null) m.Invoke(session, null);
            }
            catch { }
        }

        public static void Skip(object session, bool next)
        {
            try
            {
                if (session == null) return;
                var m = session.GetType().GetMethod(next ? "TrySkipNextAsync" : "TrySkipPreviousAsync");
                if (m != null) m.Invoke(session, null);
            }
            catch { }
        }
    }
}
