using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MasterMetrology.Models.Data
{
    public class NodeModel
    {
        public string Name { get; set; }
        public double X { get; set; }   
        public double Y { get; set; }
        public double Width { get; set; } = 120;
        public double Height { get; set; } = 60;
    }
}
