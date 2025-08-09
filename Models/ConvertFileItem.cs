using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MortysDLP.Models
{
    public class ConvertFileItem
    {
        public string SourcePath { get; set; } = "";
        public string Name => System.IO.Path.GetFileName(SourcePath);
        public string Status { get; set; } = "Bereit";
        public double Progress { get; set; } = 0;
    }
}
