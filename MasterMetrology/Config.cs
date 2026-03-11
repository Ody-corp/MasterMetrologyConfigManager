
namespace MasterMetrology
{
    /// <summary>
    /// Values for whole project.
    /// </summary>
    internal class Config
    {
        internal static bool DEBUG_MODE = false;

        // Default values for CANVAS
        // CANVAS_X and CANVAS_Y must always be the same value or some stuff will not work
        internal static double DEFAULT_VALUE_CANVAS_X = 50000;
        internal static double DEFAULT_VALUE_CANVAS_Y = 50000;
        internal static double DEFAULT_VALUE_CANVAS_CENTER = DEFAULT_VALUE_CANVAS_X / 2;

        // Default values for spacing, states (tables)
        internal static double DEFAULT_VALUE_SPACING_X = 200;
        internal static double DEFAULT_VALUE_SPACING_Y = 100;

        // Default values ModelVisual
        internal static double DEFAULT_VALUE_GRID_MARGIN = 40;
        internal static double DEFAULT_VALUE_INNER_PANEL_MARGIN = 40;
        internal static double DEFAULT_VALUE_BORDER_PADDING = 25;
        internal static double DEFAULT_VALUE_BORDER_THICKNESS = 2;
        internal static double DEFAULT_VALUE_TEXT_MARGIN = 10;

        internal static double TEMP_VALUE_X_CORD = 0;
        internal static double TEMP_VALUE_Y_CORD = 0;
    }
}
