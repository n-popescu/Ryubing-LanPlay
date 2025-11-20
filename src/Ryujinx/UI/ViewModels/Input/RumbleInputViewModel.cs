using System.Globalization;

namespace Ryujinx.Ava.UI.ViewModels.Input
{
    public partial class RumbleInputViewModel : BaseModel
    {
        private float strongRumble;
        private float weakRumble;

        public float StrongRumble
        {
            get => strongRumble;
            set
            {
                if (strongRumble != value)
                {
                    strongRumble = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StrongRumbleText));
                }
            }
        }

        public float WeakRumble
        {
            get => weakRumble;
            set
            {
                if (weakRumble != value)
                {
                    weakRumble = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(WeakRumbleText));
                }
            }
        }

        public string StrongRumbleText
        {
            get
            {
                if (StrongRumble == 10)
                {
                    return StrongRumble.ToString("F1", CultureInfo.CurrentCulture);
                }
                else
                {
                    return StrongRumble.ToString("F2", CultureInfo.CurrentCulture);
                }
            }
        }

        public string WeakRumbleText
        {
            get
            {
                if (WeakRumble == 10)
                {
                    return WeakRumble.ToString("F1", CultureInfo.CurrentCulture);
                }
                else
                {
                    return WeakRumble.ToString("F2", CultureInfo.CurrentCulture);
                }
            }
        }
    }
}
