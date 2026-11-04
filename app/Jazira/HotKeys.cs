using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Jazira;

// Глобальные горячие клавиши (RegisterHotKey): работают, в каком бы окне вы ни были.
// Если сочетание уже занято другой программой, Windows его не отдаст — тогда Register вернёт false,
// а в меню рядом с клавишей будет написано «занято». Выключили функцию — клавиша освобождается.
sealed class HotKeys : IDisposable
{
    public const uint Alt = 0x1, Ctrl = 0x2, Shift = 0x4, Win = 0x8;
    const uint NoRepeat = 0x4000; // зажатая клавиша не срабатывает много раз подряд
    const int WM_HOTKEY = 0x0312;

    readonly IntPtr _hwnd;
    readonly HwndSource? _source;
    readonly Dictionary<int, Action> _actions = new();
    readonly Dictionary<string, int> _ids = new();
    int _next = 0x4A00;

    public HotKeys(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(Hook);
    }

    // name — чтобы потом снять именно эту клавишу; повторная регистрация того же имени заменяет старую
    public bool Set(string name, uint mods, uint vk, Action action)
    {
        Remove(name);
        int id = _next++;
        if (!RegisterHotKey(_hwnd, id, mods | NoRepeat, vk)) return false;
        _actions[id] = action;
        _ids[name] = id;
        return true;
    }

    public void Remove(string name)
    {
        if (!_ids.Remove(name, out int id)) return;
        UnregisterHotKey(_hwnd, id);
        _actions.Remove(id);
    }

    IntPtr Hook(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == WM_HOTKEY && _actions.TryGetValue(w.ToInt32(), out var action))
        {
            handled = true;
            Dispatcher.CurrentDispatcher.BeginInvoke(action); // не внутри оконной процедуры
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_hwnd, id);
        _actions.Clear();
        _ids.Clear();
        _source?.RemoveHook(Hook);
    }

    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
