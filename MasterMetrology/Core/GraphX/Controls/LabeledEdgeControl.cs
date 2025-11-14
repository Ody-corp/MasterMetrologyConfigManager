using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using GraphX.Controls;
using MasterMetrology.Models.Visual;

namespace MasterMetrology.Core.GraphX.Controls
{
    public class LabeledEdgeControl : EdgeControl
    {
        private readonly GraphEdge _edgeModel;
        private readonly AttachableEdgeLabelControl _attachableLabel;
        private bool _labelAttached;

        public LabeledEdgeControl(VertexControl source, VertexControl target, GraphEdge edge)
            : base(source, target, edge)
        {
            if (edge == null) throw new ArgumentNullException(nameof(edge));
            _edgeModel = edge;

            // vytvor attachable label (EdgeLabelControl implementuje ContentControl)
            _attachableLabel = new AttachableEdgeLabelControl
            {
                ShowLabel = true,
                DataContext = _edgeModel
            };

            // bind Content -> GraphEdge.Text (jednosmerne)
            var binding = new Binding(nameof(GraphEdge.Text)) { Source = _edgeModel, Mode = BindingMode.OneWay };
            _attachableLabel.SetBinding(ContentControl.ContentProperty, binding);

            // Ak si chceš nastaviť nejaké základné vizuálne vlastnosti:
            // (Pozor: nie všetky závisia od default template, môžeš pridať padding / style)
            // _attachableLabel.Padding = new Thickness(6, 2, 6, 2);

            // subscribe na model (voliteľné — nepotrebné pre text, binding stačí;
            // ale môžeš to využiť ak potrebuješ iné side-effects)
            _edgeModel.PropertyChanged += EdgeModel_PropertyChanged;

            // pripojíme sa na Loaded/Unloaded aby sme attachli label keď RootArea bude k dispozícii
            Loaded += LabeledEdgeControl_Loaded;
            Unloaded += LabeledEdgeControl_Unloaded;
        }

        private void LabeledEdgeControl_Loaded(object? sender, RoutedEventArgs e)
        {
            // Attach až keď máme RootArea (GraphArea by mal nastaviť RootArea pred OnApplyTemplate/Loaded, ale
            // v prípade race-condition použijeme Dispatcher.BeginInvoke aby sa to odložilo).
            if (_labelAttached) return;

            TryAttachLabelWhenReady();
        }

        private void TryAttachLabelWhenReady()
        {
            // Ak RootArea je null, naplánujme attach na neskôr (jednorazovo)
            if (this.RootArea == null)
            {
                // odlož len raz; použijeme Loaded/Dispatcher
                Dispatcher.BeginInvoke((Action)(() => TryAttachLabelWhenReady()), DispatcherPriority.Loaded);
                return;
            }

            try
            {
                _attachableLabel.Attach(this); // toto volá node.AttachLabel(this) vo vnútri GraphX implementácie
                _labelAttached = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AttachableEdgeLabelControl.Attach failed: {ex.Message}");
                // fallback: nie je koniec sveta, label nebude zobrazený cez GraphArea attach
            }
        }

        private void LabeledEdgeControl_Unloaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_labelAttached)
                {
                    // detachujeme (remove z RootArea.Children)
                    _attachableLabel.Detach();
                    _labelAttached = false;
                }
                _edgeModel.PropertyChanged -= EdgeModel_PropertyChanged;
                Loaded -= LabeledEdgeControl_Loaded;
                Unloaded -= LabeledEdgeControl_Unloaded;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LabeledEdgeControl_Unloaded cleanup failed: " + ex.Message);
            }
        }

        // voliteľné: ak chceš robiť niečo pri zmene Text/ToolTip v modeli
        private void EdgeModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GraphEdge.Text))
            {
                // Binding už urobí zmenu obsahu labelu automaticky.
                // Ale môžeš tu zavolať UpdateEdge aby sa prepolohoval label.
                try
                {
                    // zabezpečíme, že geometria/label pozícia sa prerátajú
                    this.Dispatcher.BeginInvoke((Action)(() => this.UpdateEdge(true)), DispatcherPriority.Normal);
                }
                catch { }
            }
        }

        protected override void OnRender(System.Windows.Media.DrawingContext dc)
        {
            base.OnRender(dc);
            // môžeš pridať debug drawing ak chceš
        }

        public override void Dispose()
        {
            base.Dispose();
            // additional cleanup if needed
            try
            {
                if (_labelAttached)
                {
                    _attachableLabel.Detach();
                    _labelAttached = false;
                }
                _edgeModel.PropertyChanged -= EdgeModel_PropertyChanged;
            }
            catch { }
        }
    }
}
