using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;

namespace MasterMetrology.Controllers
{
    internal class PanAndZoomController
    {
        private readonly FrameworkElement _viewport;   
        private readonly ScaleTransform _zoomTransform;
        private readonly TranslateTransform _panTransform;
        private readonly double _canvasSize;

        private Point _lastMousePos;
        private bool _isPanning;

        private const double MinZoom = 0.4;
        private const double MaxZoom = 3.0;

        public PanAndZoomController(FrameworkElement viewport, ScaleTransform zoom, TranslateTransform pan, double canvasSize)
        {
            _viewport = viewport;
            _zoomTransform = zoom;
            _panTransform = pan;
            _canvasSize = canvasSize;

            _viewport.MouseWheel += OnMouseWheel;
            _viewport.MouseDown += OnMouseDown;
            _viewport.MouseMove += OnMouseMove;
            _viewport.MouseUp += OnMouseUp;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left)
            {
                _isPanning = true;
                _lastMousePos = e.GetPosition(_viewport);
                _viewport.CaptureMouse();
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isPanning = false;
            _viewport.ReleaseMouseCapture();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(_viewport);
                _panTransform.X += pos.X - _lastMousePos.X;
                _panTransform.Y += pos.Y - _lastMousePos.Y;
                _lastMousePos = pos;
                ClampPan();
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            Point center = new Point(_viewport.ActualWidth / 2, _viewport.ActualHeight / 2);
            double zoomFactor = e.Delta > 0 ? 1.1 : 0.9;

            double newScaleX = _zoomTransform.ScaleX * zoomFactor;
            double newScaleY = _zoomTransform.ScaleY * zoomFactor;

            newScaleX = Math.Max(MinZoom, Math.Min(MaxZoom, newScaleX));
            newScaleY = Math.Max(MinZoom, Math.Min(MaxZoom, newScaleY));

            _panTransform.X = (_panTransform.X - center.X) * (newScaleX / _zoomTransform.ScaleX) + center.X;
            _panTransform.Y = (_panTransform.Y - center.Y) * (newScaleY / _zoomTransform.ScaleY) + center.Y;

            _zoomTransform.ScaleX = newScaleX;
            _zoomTransform.ScaleY = newScaleY;

            ClampPan();
        }

        private void ClampPan()
        {
            double zoomX = _zoomTransform.ScaleX;
            double zoomY = _zoomTransform.ScaleY;

            double minX = -(_canvasSize * zoomX) + _viewport.ActualWidth;
            double minY = -(_canvasSize * zoomY) + _viewport.ActualHeight;

            _panTransform.X = Math.Min(0, Math.Max(minX, _panTransform.X));
            _panTransform.Y = Math.Min(0, Math.Max(minY, _panTransform.Y));
        }

        public void CenterView()
        {
            _panTransform.X = -(_canvasSize / 2) + _viewport.ActualWidth / 2;
            _panTransform.Y = -(_canvasSize / 2) + _viewport.ActualHeight / 2;
        }
    }
}
