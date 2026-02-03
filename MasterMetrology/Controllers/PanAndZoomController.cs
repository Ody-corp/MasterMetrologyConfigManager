using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Diagnostics;
using GraphX.Controls;

namespace MasterMetrology.Controllers
{
    internal class PanAndZoomController
    {
        private readonly FrameworkElement _viewport;   
        private readonly ScaleTransform _zoomTransform;
        private readonly TranslateTransform _panTransform;
        private readonly double _canvasSize;

        private Point? _downPos;
        private Point _lastMousePos;
        private bool _isPanning;

        private const double MinZoom = 0.4;
        private const double MaxZoom = 3.0;
        private const double DragThreshold = 6; // px

        public PanAndZoomController(FrameworkElement viewport, ScaleTransform zoom, TranslateTransform pan, double canvasSize)
        {
            _viewport = viewport;
            _zoomTransform = zoom;
            _panTransform = pan;
            _canvasSize = canvasSize;

            _viewport.MouseWheel += OnMouseWheel;
            _viewport.PreviewMouseDown += OnMouseDown;
            _viewport.PreviewMouseMove += OnMouseMove;
            _viewport.PreviewMouseUp += OnMouseUp;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // len LMB
            if (e.ChangedButton != MouseButton.Left) return;

            // ak klik na vertex/edge/label -> GraphX
            if (IsInteractiveClick(e.OriginalSource as DependencyObject))
                return;

            _downPos = e.GetPosition(_viewport);
            _lastMousePos = _downPos.Value;

            // NE-capture-ujeme tu, aby click fungoval normálne.
            // Capture spravíme až keď sa z toho stane pan (po prahu).
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            if (_isPanning)
            {
                _isPanning = false;
                _viewport.ReleaseMouseCapture();
                e.Handled = true;
            }

            _downPos = null;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_downPos == null) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var pos = e.GetPosition(_viewport);

            if (!_isPanning)
            {
                var d = pos - _downPos.Value;
                if (Math.Abs(d.X) <= DragThreshold && Math.Abs(d.Y) <= DragThreshold)
                    return;

                // od teraz je to pan
                _isPanning = true;
                _viewport.CaptureMouse();
            }

            // panning
            _panTransform.X += pos.X - _lastMousePos.X;
            _panTransform.Y += pos.Y - _lastMousePos.Y;
            _lastMousePos = pos;

            ClampPan();
            e.Handled = true; // len keď už panujeme
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

        private static bool IsInteractiveClick(DependencyObject? src)
        {
            while (src != null)
            {
                if (src is VertexControl) return true;
                if (src is EdgeControl) return true;
                if (src is IEdgeLabelControl) return true;
                src = VisualTreeHelper.GetParent(src);
            }
            return false;
        }

        public void CenterView()
        {
            var centerX = _viewport.ActualWidth * 0.5;
            var centerY = _viewport.ActualHeight * 0.5;

            var zoomX = _zoomTransform.ScaleX;
            var zoomY = _zoomTransform.ScaleY;

            var offset = _canvasSize * 0.5; // namiesto hardcoded 25000

            _panTransform.X = centerX - (offset * zoomX);
            _panTransform.Y = centerY - (offset * zoomY);

            ClampPan();
        }


        public Point GetViewCenterWorld()
        {
            // screen what you see
            var sx = _viewport.ActualWidth * 0.5;
            var sy = _viewport.ActualHeight * 0.5;

            // zoom
            var zoomX = _zoomTransform.ScaleX;
            var zoomY = _zoomTransform.ScaleY;

            // pan
            var panX = _panTransform.X;
            var panY = _panTransform.Y;

            var offset = _canvasSize * 0.5;

            // world = (screen - pan) / zoom
            var wx = ((sx - panX) / zoomX) - offset;
            var wy = ((sy - panY) / zoomY) - offset;

            Debug.WriteLine($"center screen=({sx},{sy}) pan=({panX},{panY}) zoom=({zoomX},{zoomY}) => world=({wx},{wy})");
            return new Point(wx, wy);
        }

    }
}
