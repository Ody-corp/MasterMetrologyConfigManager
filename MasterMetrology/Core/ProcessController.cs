using MasterMetrology.Models.Visual;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Xml;
using MasterMetrology.Models.Data;
using MasterMetrology.Core.Rendering;
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace MasterMetrology
{
    internal class ProcessController(Canvas viewPort)
    {
        private Canvas viewPort = viewPort;
        private FileReader _fileReader = new FileReader();
        private VisualRendering visualRender = new VisualRendering();

        public event Action<GraphVertex> VertexSelected;

        private List<InputsDefModelData> inputsDefModelDatas;
        private List<OutputModelData> outputsDefModelDatas;
        private List<StateModelData> statesModelDatas;

        public ObservableCollection<TransitionViewModel> AllTransitions = new ObservableCollection<TransitionViewModel>();


        private string filePath;

        public void LoadDataXML(string filePath)
        {
            SaveOldXMLPath(filePath);
            var list = _fileReader.LoadDataFromFile(filePath);

            inputsDefModelDatas = list.InputsDefinition;
            outputsDefModelDatas = list.OutputDefinition;
            statesModelDatas = list.FullListStateModelData;

            PopulateTransitions(statesModelDatas);

            visualRender.RenderGraph(statesModelDatas, viewPort, v => VertexSelected?.Invoke(v));
        }

        private void SaveOldXMLPath(string filePath)
        {
            this.filePath = filePath;
        }

        private void PopulateTransitions(List<StateModelData> roots)
        {

            void doPopulate()
            {
                AllTransitions.Clear();

                void collect(StateModelData s)
                {
                    if (s.TransitionsData != null)
                    {

                        foreach (var t in s.TransitionsData)
                        {
                            t.FromStage = s.FullIndex;
                            AllTransitions.Add(new TransitionViewModel(t, s.FullIndex));
                        }
                    }

                    if (s.SubStatesData != null)
                    {
                        foreach (var sub in s.SubStatesData)
                            collect(sub);
                    }
                }

                foreach (var r in roots)
                    collect(r);

                if (roots != null)
                {
                    foreach (var r in roots)
                        collect(r);
                }
            }

            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.Invoke(doPopulate);
            else
                doPopulate();

        }

        // Vrátí "flattovaný" zoznam všetkých states (pre ComboBox target)
        public List<StateModelData> GetFlatStates()
        {
            var result = new List<StateModelData>();
            if (statesModelDatas == null) return result;

            void collect(StateModelData s)
            {
                result.Add(s);
                if (s.SubStatesData != null)
                    foreach (var sub in s.SubStatesData) collect(sub);
            }

            foreach (var r in statesModelDatas) collect(r);
            return result;
        }

        // Delete prechod - odstráni z dát a z grafu
        public bool DeleteTransition(TransitionViewModel vm)
        {
            if (vm == null) return false;
            var t = vm.Transition;
            if (t == null) return false;

            var owner = FindStateByFullIndex(statesModelDatas, vm.FromFullIndex);
            if (owner != null && owner.TransitionsData != null)
            {
                var removed = owner.TransitionsData.Remove(t);
                // odstrániť z UI kolekcie (na UI vlákne)
                if (removed)
                {
                    if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                    {
                        Application.Current.Dispatcher.Invoke(() => AllTransitions.Remove(vm));
                    }
                    else
                    {
                        AllTransitions.Remove(vm);
                    }

                    // odstrániť z vizuálu (grafu)
                    visualRender.RemoveTransition(t);
                    return true;
                }
            }

            return false;
        }

        // Add nový transition (cez UI): pridá do dát aj do grafu a do AllTransitions
        public bool AddTransition(string fromFullIndex, string input, string toFullIndex)
        {
            if (string.IsNullOrEmpty(fromFullIndex) || string.IsNullOrEmpty(toFullIndex)) return false;

            var owner = FindStateByFullIndex(statesModelDatas, fromFullIndex);
            if (owner == null) return false;

            var newT = new TransitionModelData
            {
                Input = input,
                NextStage = toFullIndex,
                FromStage = owner.FullIndex
            };

            if (owner.TransitionsData == null)         
                owner.TransitionsData = new System.Collections.ObjectModel.ObservableCollection<TransitionModelData>();

            var vm = new TransitionViewModel(newT, owner.FullIndex);

            // add to UI collection
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.Invoke(() => AllTransitions.Add(vm));
            else
                AllTransitions.Add(vm);

            // pridaj do vizuálu (grafu) – VisualRendering sa postará o tom, aby nevykonal celý relayout
            visualRender.AddTransition(newT);

            return true;
        }

        private StateModelData FindStateByFullIndex(List<StateModelData> list, string fullIndex)
        {
            if (list == null || string.IsNullOrEmpty(fullIndex)) return null;

            foreach (var s in list)
            {
                if (s.FullIndex == fullIndex) return s;
                if (s.SubStatesData != null)
                {
                    var r = FindStateByFullIndex(s.SubStatesData.ToList(), fullIndex);
                    if (r != null) return r;
                }
            }
            return null;
        }
    }
}
