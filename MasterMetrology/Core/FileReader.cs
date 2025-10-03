using MasterMetrology.Models.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace MasterMetrology
{
    internal class FileReader
    {
        List<InputsDefModelData> InputsDefinition = new List<InputsDefModelData>();
        List<OutputModelData> OutputDefinition = new List<OutputModelData>();
        List<StateModelData> FullListStateModelData = new List<StateModelData>();
        Stack<StateModelData> stack = new Stack<StateModelData>();

        Stack<string> stackIndex = new Stack<string>();

        public (List<InputsDefModelData> InputsDefinition, List<OutputModelData> OutputDefinition, List<StateModelData> FullListStateModelData) LoadDataFromFile(string filePath)
        {
            using (XmlReader reader = XmlReader.Create(filePath))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "Input")
                        {
                            var input = new InputsDefModelData()
                            {
                                Name = reader.GetAttribute("Name"),
                                ID = reader.GetAttribute("ID")
                            };
                            InputsDefinition.Add(input);
                        }
                        else if (reader.Name == "Output")
                        {
                            var output = new OutputModelData()
                            {
                                Name = reader.GetAttribute("Name"),
                                ID = reader.GetAttribute("ID"),
                                UpdateDefinition = Boolean.Parse(reader.GetAttribute("UpdateDefinition")),
                                UpdateParameters = Boolean.Parse(reader.GetAttribute("UpdateParameters")),
                                UpdateCalibration = Boolean.Parse(reader.GetAttribute("UpdateCalibration")),
                                UpdateMeasuredData = Boolean.Parse(reader.GetAttribute("UpdateMeasuredData")),

                            };
                            OutputDefinition.Add(output);
                        }
                        else if (reader.Name == "Transition")
                        {
                            if (stack.Count > 0)
                            {
                                stack.Peek().TransitionsData.Add(new TransitionModelData
                                {
                                    Input = reader.GetAttribute("Input"),
                                    NextStage = reader.GetAttribute("NextState")
                                });
                            }
                        }
                        else if (reader.Name == "State")
                        {
                            stackIndex.Push(reader.GetAttribute("Index"));
                            string tempFullIndex;

                            if (stackIndex.Count > 0)
                            {
                                tempFullIndex = string.Join(".", stackIndex);
                            }
                            else
                            {
                                tempFullIndex = stackIndex.First();
                            }


                            var state = new StateModelData()
                            {
                                Name = reader.GetAttribute("Name"),
                                Index = reader.GetAttribute("Index"),
                                Output = reader.GetAttribute("Output"),
                                FullIndex = tempFullIndex,
                            };

                            if (stack.Count > 0)
                            {
                                stack.Peek().SubStatesData.Add(state);
                            }
                            else
                            {
                                FullListStateModelData.Add(state);
                            }

                            stack.Push(state);
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "State")
                    {
                        stack.Pop();
                        stackIndex.Pop();
                    }
                }
                
            }

            return (InputsDefinition, OutputDefinition, FullListStateModelData);
        }
    }
}
