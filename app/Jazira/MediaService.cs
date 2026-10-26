using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Jazira;

// Что играет в Windows: системный медиа-пульт (тот же, что в панели громкости).
// Работает с любым плеером, который его поддерживает: Spotify, браузеры, Media Player…
// События приходят из потоков WinRT — всё переносим в поток интерфейса.
sealed class MediaService
{
    readonly Dispatcher _ui;
    GlobalSystemMediaTransportControlsSessionManager? _manager;
    GlobalSystemMediaTransportControlsSession? _session;
    TimeSpan _position, _duration;
    DateTimeOffset _stamp;

    public bool HasSession => _session != null || _fake;
    public string Title { get; private set; } = "";
    public string Artist { get; private set; } = "";
    public string AppName { get; private set; } = "";
    public BitmapSource? Cover { get; private set; }
    public bool IsPlaying { get; private set; }
    public TimeSpan Duration => _duration;

    public event Action? Changed;       // обновились любые данные
    public event Action? TrackChanged;  // началась другая песня

    public MediaService(Dispatcher ui) { _ui = ui; }

    public async Task StartAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += (m, _) => _ui.InvokeAsync(() => Attach(m.GetCurrentSession()));
            Attach(_manager.GetCurrentSession());
        }
        catch (Exception ex) { App.Log(ex); } // нет медиа-пульта — остров просто без музыки
    }

    // Позиция «сейчас»: плеер сообщает её с отметкой времени, дальше досчитываем сами
    public TimeSpan Position
    {
        get
        {
            var p = _position;
            if (IsPlaying && _stamp.Year > 2000) p += DateTimeOffset.Now - _stamp;
            if (_duration > TimeSpan.Zero && p > _duration) p = _duration;
            return p < TimeSpan.Zero ? TimeSpan.Zero : p;
        }
    }

    void Attach(GlobalSystemMediaTransportControlsSession? s)
    {
        if (_session != null)
        {
            _session.MediaPropertiesChanged -= OnProps;
            _session.PlaybackInfoChanged -= OnPlayback;
            _session.TimelinePropertiesChanged -= OnTimeline;
        }
        _session = s;
        if (s == null)
        {
            Title = Artist = AppName = "";
            Cover = null;
            IsPlaying = false;
            _duration = TimeSpan.Zero;
            Changed?.Invoke();
            return;
        }
        s.MediaPropertiesChanged += OnProps;
        s.PlaybackInfoChanged += OnPlayback;
        s.TimelinePropertiesChanged += OnTimeline;
        AppName = PrettyApp(s.SourceAppUserModelId);
        ReadPlayback();
        ReadTimeline();
        _ = ReadPropsAsync();
    }

    void OnProps(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e) =>
        _ui.InvokeAsync(() => _ = ReadPropsAsync());

    void OnPlayback(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e) =>
        _ui.InvokeAsync(() => { ReadPlayback(); Changed?.Invoke(); });

    void OnTimeline(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e) =>
        _ui.InvokeAsync(() => { ReadTimeline(); Changed?.Invoke(); });

    async Task ReadPropsAsync()
    {
        var s = _session;
        if (s == null) return;
        try
        {
            var p = await s.TryGetMediaPropertiesAsync();
            if (s != _session || p == null) return;
            bool newTrack = p.Title != Title || p.Artist != Artist;
            Title = p.Title ?? "";
            Artist = p.Artist ?? "";
            if (newTrack) Cover = await LoadCoverAsync(p.Thumbnail);
            Changed?.Invoke();
            if (newTrack && Title.Length > 0) TrackChanged?.Invoke();
        }
        catch (Exception ex) { App.Log(ex); }
    }

    void ReadPlayback()
    {
        var info = _session?.GetPlaybackInfo();
        IsPlaying = info?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
    }

    void ReadTimeline()
    {
        var t = _session?.GetTimelineProperties();
        if (t == null) return;
        _position = t.Position;
        _duration = t.EndTime - t.StartTime;
        _stamp = t.LastUpdatedTime;
    }

    static async Task<BitmapSource?> LoadCoverAsync(IRandomAccessStreamReference? thumb)
    {
        if (thumb == null) return null;
        try
        {
            using var ras = await thumb.OpenReadAsync();
            using var src = ras.AsStreamForRead();
            var ms = new MemoryStream();
            await src.CopyToAsync(ms);
            ms.Position = 0;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 128;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception) { return null; } // без обложки — покажем заглушку
    }

    public async void PlayPause()
    {
        try { if (_session != null) await _session.TryTogglePlayPauseAsync(); } catch (Exception ex) { App.Log(ex); }
    }

    public async void Next()
    {
        try { if (_session != null) await _session.TrySkipNextAsync(); } catch (Exception ex) { App.Log(ex); }
    }

    public async void Previous()
    {
        try { if (_session != null) await _session.TrySkipPreviousAsync(); } catch (Exception ex) { App.Log(ex); }
    }

    static string PrettyApp(string? id)
    {
        var s = (id ?? "").ToLowerInvariant();
        if (s.Contains("spotify")) return "Spotify";
        if (s.Contains("msedge")) return "Edge";
        if (s.Contains("chrome")) return "Chrome";
        if (s.Contains("firefox") || s.Contains("308046b0af4a39cb")) return "Firefox";
        if (s.Contains("yandex")) return "Yandex";
        if (s.Contains("telegram")) return "Telegram";
        if (s.Contains("zunemusic") || s.Contains("mediaplayer")) return "Media Player";
        var name = id ?? "";
        int bang = name.IndexOf('!');
        if (bang > 0) name = name[..bang];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return name.Length > 18 ? name[..18] : name;
    }

    // ---- Подставные данные для снимков (Jazira.exe --snapshot) ----
    bool _fake;

    public void Fake(string title, string artist, string app, BitmapSource? cover, bool playing, TimeSpan pos, TimeSpan dur)
    {
        _fake = true;
        Title = title; Artist = artist; AppName = app; Cover = cover; IsPlaying = playing;
        _position = pos; _duration = dur; _stamp = DateTimeOffset.Now;
    }

    public void FakeNone()
    {
        _fake = false;
        Title = Artist = AppName = "";
        Cover = null; IsPlaying = false; _duration = TimeSpan.Zero;
    }
}
