using ReactiveUI;

namespace WarHub.Rule2ProfileTool.Models
{
    public class DatafileConversionStatus : ReactiveObject
    {
        private double _conversionProgressValue;

        public DatafileConversionStatus(DatafileInfo info)
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