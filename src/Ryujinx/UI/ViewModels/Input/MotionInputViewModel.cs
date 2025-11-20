using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;

namespace Ryujinx.Ava.UI.ViewModels.Input
{
    public partial class MotionInputViewModel : BaseModel
    {
        [ObservableProperty]
        public partial int Slot { get; set; }

        [ObservableProperty]
        public partial int AltSlot { get; set; }

        [ObservableProperty]
        public partial string DsuServerHost { get; set; }

        [ObservableProperty]
        public partial int DsuServerPort { get; set; }

        [ObservableProperty]
        public partial bool MirrorInput { get; set; }

        [ObservableProperty]
        public partial int Sensitivity { get; set; }

        private double _gyroDeadzone;
        
        public double GyroDeadzone
        {
            get => _gyroDeadzone;
            set
            {
                if (_gyroDeadzone != value)
                {
                    _gyroDeadzone = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GyroDeadzoneText));
                }
            }
        }

        public string GyroDeadzoneText
        {
            get
            {
                if (_gyroDeadzone == 100)
                {
                    return _gyroDeadzone.ToString("F1", CultureInfo.CurrentCulture);
                }
                else
                {
                    return _gyroDeadzone.ToString("F2", CultureInfo.CurrentCulture);
                }
            }
        }

        [ObservableProperty]
        public partial bool EnableCemuHookMotion { get; set; }
    }
}
