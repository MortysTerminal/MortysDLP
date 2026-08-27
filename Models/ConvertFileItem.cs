using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MortysDLP.Models
{
    public class ConvertFileItem : INotifyPropertyChanged
    {
        private string _sourcePath = "";
        private string _status = "Bereit";
        private double _progress;

        public string SourcePath
        {
            get => _sourcePath;
            set { _sourcePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(Name)); }
        }

        public string Name => System.IO.Path.GetFileName(SourcePath);

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
