# Metrology Config Manager

`Metrology Config Manager` is a WPF desktop application for creating, editing, and validating XML-based state machine configurations.

The project focuses on configuration files for `MeasurementModule`, especially:
- input definitions
- output definitions
- hierarchical states
- transitions between states

The application provides both structured editing and a visual graph view, making it easier to manage and review complex state-machine configurations.

## Features

- Import an XML configuration file
- Create and edit input definitions
- Create and edit output definitions
- Create root states and nested substates
- Manage hierarchical state structure
- Add and remove transitions between states
- Visualize the state machine as a graph
- Undo and redo changes
- Warn about unsaved changes before closing or opening another file
- Validate missing input/output definitions before saving

## Technology Stack

- .NET 8
- WPF
- GraphX for graph rendering

## Requirements

To build and run the project you need:
- Windows
- .NET SDK 8.0 or newer
- WPF support

## Running the Project

```bash
dotnet restore
dotnet build
dotnet run
```

## Purpose of the Configuration

The configuration is built from three main parts:

### 1. Input Definitions

Defines the list of inputs that can trigger transitions between states.

Each input contains:
- `ID`
- `Name`

Example:

```xml
<InputsDefinition>
  <Input ID="1" Name="START" />
  <Input ID="2" Name="STOP" />
</InputsDefinition>
```

### 2. Output Definitions

Defines outputs that can be assigned to individual states.

Each output contains:
- `ID`
- `Name`
- `UpdateDefinition`
- `UpdateParameters`
- `UpdateCalibration`
- `UpdateMeasuredData`
- `UpdateProcessedData`

Example:

```xml
<OutputsDefinition>
  <Output
    ID="10"
    Name="MEASURE"
    UpdateDefinition="true"
    UpdateParameters="false"
    UpdateCalibration="true"
    UpdateMeasuredData="true"
    UpdateProcessedData="false" />
</OutputsDefinition>
```

### 3. State Machine

Defines the actual state-machine structure:
- states
- nested substates
- transitions
- output references

Example:

```xml
<StateMachine Depth="2">
  <State Name="Idle" Index="1" Output="10">
    <Transition Input="1" NextState="2" />
  </State>

  <State Name="Run" Index="2" Output="11">
    <Transition Input="2" NextState="1" />
  </State>
</StateMachine>
```

## XML Format

The application works with XML documents in the following structure:

```xml
<MeasurementModule>
  <InputsDefinition>
    <Input ID="1" Name="START" />
  </InputsDefinition>

  <OutputsDefinition>
    <Output
      ID="10"
      Name="MEASURE"
      UpdateDefinition="true"
      UpdateParameters="false"
      UpdateCalibration="false"
      UpdateMeasuredData="true"
      UpdateProcessedData="false" />
  </OutputsDefinition>

  <StateMachine Depth="1">
    <State Name="Idle" Index="1" Output="10">
      <Transition Input="1" NextState="2" />
    </State>
    <State Name="Run" Index="2" Output="11" />
  </StateMachine>
</MeasurementModule>
```

## Configuration Rules

When working with the state machine, these rules apply:
- input and output `ID` values are numeric
- state `Index` values are numeric and must be unique within the same hierarchy level
- `FullIndex` is derived automatically from the state hierarchy
- a state cannot have multiple transitions with the same input
- states with child states are treated as sections
- leaf states are expected to use output definitions
- transition targets should point to an existing state
- before saving, the application checks whether all used input and output references have definitions

## Typical Workflow

1. Create a new XML file or import an existing one.
2. Define inputs.
3. Define outputs.
4. Create states and their hierarchy.
5. Assign outputs to leaf states.
6. Define transitions using input IDs.
7. Save the configuration back to XML.

## Application Actions

The UI currently supports:
- `New file`
- `Import file`
- `Save`
- `Save as`
- `Undo`
- `Redo`
- Add state to root
- Add state as substate
- Delete a single state
- Delete a whole section
- Center the view
- Zoom and pan across the canvas

## Project Structure

- `Core/`
  Core application logic for loading, saving, process control, history, UI controllers, and graph rendering.
- `Models/Data/`
  Internal data models for states, transitions, inputs, and outputs.
- `Models/XML/`
  DTO models used for XML serialization and export.
- `Models/Visual/`
  View models for visual representation of states and transitions.
- `Utils/`
  Utility classes, converters, popup dialogs, and debug helpers.
- `MainWindow.xaml`
  Main application UI.

## Notes

- This project is an editor for XML state-machine configurations, not a runtime engine for executing them.
- `StateMachine.Depth` is generated automatically during export based on the deepest state hierarchy.
- XML is saved in a formatted, readable structure.
- The application warns about missing definitions and unsaved changes before critical actions.
