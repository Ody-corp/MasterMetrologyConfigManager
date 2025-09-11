using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterMetrology.Models.Data
{
    class OutputModelData
    {
        public string Name { get; set; }
        public string ID { get; set; }
        public bool UpdateDefinition { get; set; }
        public bool UpdateParameters { get; set; }
        public bool UpdateCalibration { get; set; }
        public bool UpdateMeasuredData { get; set; }
    }
}
