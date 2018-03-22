using ReactiveUI;

namespace WarHub.Rule2ProfileTool.Models
{
    public class DatafileConversionViewModel : ReactiveObject
    {
        private double _conversionProgressValue;

        public DatafileConversionViewModel(DatafileInfo info)
        {
            Info = info;
        }

        public DatafileInfo Info { get; }

        public double ConversionProgressValue
        {
            get => _conversionProgressValue;
            set => this.RaiseAndSetIfChanged(ref _conversionProgressValue, value);
        }
    }
}