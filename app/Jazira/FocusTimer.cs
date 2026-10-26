using System;

namespace Jazira;

// Помодоро: 25 минут фокуса, 5 минут перерыва. Время считается по часам, а не по тикам,
// поэтому таймер не «отстаёт», даже если компьютер был занят.
sealed class FocusTimer
{
    public static readonly TimeSpan FocusLength = TimeSpan.FromMinutes(25);
    public static readonly TimeSpan BreakLength = TimeSpan.FromMinutes(5);

    TimeSpan _left = FocusLength;
    DateTime _endsAt;

    public bool IsBreak { get; private set; }
    public bool Running { get; private set; }
    public TimeSpan Length => IsBreak ? BreakLength : FocusLength;
    public TimeSpan Left => Running ? Max(TimeSpan.Zero, _endsAt - DateTime.Now) : _left;
    public double LeftFraction => Left.TotalSeconds / Length.TotalSeconds;

    public event Action<bool>? Finished; // true — закончился фокус, false — перерыв

    public void Toggle()
    {
        if (Running) { _left = Left; Running = false; }
        else { _endsAt = DateTime.Now + _left; Running = true; }
    }

    public void Reset()
    {
        Running = false;
        _left = Length;
    }

    public void SwitchMode()
    {
        IsBreak = !IsBreak;
        Reset();
    }

    public void Tick()
    {
        if (!Running || DateTime.Now < _endsAt) return;
        bool wasFocus = !IsBreak;
        Running = false;
        IsBreak = !IsBreak;
        _left = Length;
        Finished?.Invoke(wasFocus);
    }

    // Для снимков: таймер «уже идёт»
    public void Preview(TimeSpan left, bool running)
    {
        IsBreak = false;
        _left = left;
        Running = running;
        _endsAt = DateTime.Now + left;
    }

    static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
