using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterMetrology
{
    /// <summary>
    /// Values for whole project. It's nice to have it all in one place. "Better organisation"
    /// </summary>
    internal class Config
    {

        /// <summary>
        /// Default values for CANVAS
        /// CANVAS_X and CANVAS_Y must always be the same value or some stuff will not work
        /// </summary>
        internal static double DEFAULT_VALUE_CANVAS_X = 50000;
        internal static double DEFAULT_VALUE_CANVAS_Y = 50000;
        internal static double DEFAULT_VALUE_CANVAS_CENTER = DEFAULT_VALUE_CANVAS_X / 2;

        /// <summary>
        /// Default values for spacing, states (tables)
        /// </summary>
        internal static double DEFAULT_VALUE_SPACING_X = 200;
        internal static double DEFAULT_VALUE_SPACING_Y = 100;

        /// <summary>
        /// Default values ModelVisual
        /// </summary>
        internal static double DEFAULT_VALUE_GRID_MARGIN = 40;
        internal static double DEFAULT_VALUE_INNER_PANEL_MARGIN = 40;
        internal static double DEFAULT_VALUE_BORDER_PADDING = 25;
        internal static double DEFAULT_VALUE_BORDER_THICKNESS = 2;
        internal static double DEFAULT_VALUE_TEXT_MARGIN = 10;

        /// <summary>
        /// 
        /// </summary>
        internal static double TEMP_VALUE_X_CORD = 0;
        internal static double TEMP_VALUE_Y_CORD = 0;
    }
}
