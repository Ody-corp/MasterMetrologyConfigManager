using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MasterMetrology.Core.UI.Controllers
{
    internal sealed class KeyboardPanController
    {
        private readonly Window _window;
        private readonly PanAndZoomController _panZoom;

        private readonly DispatcherTimer _timer;

        private bool _left, _right, _up, _down;
        private readonly double _baseSpeedPxPerSec;

        public KeyboardPanController(Window window, PanAndZoomController panZoom, double baseSpeedPxPerSec = 800)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _panZoom = panZoom ?? throw new ArgumentNullException(nameof(panZoom));
            _baseSpeedPxPerSec = baseSpeedPxPerSec;

            _timer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _timer.Tick += OnTick;

            // dôležité: radšej Preview... aby to fungovalo aj keď focus má textbox/combobox
            _window.KeyDown += OnKeyDown;
            _window.KeyUp += OnKeyUp;

            // ak stratíš focus, pustíme klávesy (aby "nezostalo držané")
            _window.Deactivated += (_, __) => ResetKeys();
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat) return;

            switch (e.Key)
            {
                case Key.Left: _left = true; e.Handled = true; break;
                case Key.Right: _right = true; e.Handled = true; break;
                case Key.Up: _up = true; e.Handled = true; break;
                case Key.Down: _down = true; e.Handled = true; break;

                // voliteľné: rýchle centrovanie
                case Key.Home:
                    _panZoom.CenterView();
                    e.Handled = true;
                    break;

                default:
                    return;
            }

            EnsureTimer();
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left: _left = false; e.Handled = true; break;
                case Key.Right: _right = false; e.Handled = true; break;
                case Key.Up: _up = false; e.Handled = true; break;
                case Key.Down: _down = false; e.Handled = true; break;
                default:
                    return;
            }

            StopTimerIfIdle();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            int ix = (_right ? 1 : 0) - (_left ? 1 : 0);
            int iy = (_down ? 1 : 0) - (_up ? 1 : 0);

            if (ix == 0 && iy == 0)
            {
                StopTimerIfIdle();
                return;
            }

            // dt ~ 0.016s
            double dt = _timer.Interval.TotalSeconds;

            // aby sa to správalo prirodzene:
            // - pri väčšom zoome je "kamera citlivejšia", takže delíme zoomom
            double zoom = Math.Max(0.01, _panZoom.GetZoom());
            double speed = _baseSpeedPxPerSec / zoom;

            double dx = ix * speed * dt;
            double dy = iy * speed * dt;

            _panZoom.PanBy(dx, dy);
        }

        private void EnsureTimer()
        {
            if (!_timer.IsEnabled)
                _timer.Start();
        }

        private void StopTimerIfIdle()
        {
            if (_left || _right || _up || _down) return;
            if (_timer.IsEnabled) _timer.Stop();
        }

        private void ResetKeys()
        {
            _left = _right = _up = _down = false;
            StopTimerIfIdle();
        }

        public void Detach()
        {
            _timer.Stop();
            _timer.Tick -= OnTick;

            _window.PreviewKeyDown -= OnKeyDown;
            _window.PreviewKeyUp -= OnKeyUp;
        }
    }
}