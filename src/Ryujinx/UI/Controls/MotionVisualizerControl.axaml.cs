using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using System;
using System.Numerics;

namespace Ryujinx.Ava.UI.Controls
{
    public partial class MotionVisualizerControl : UserControl
    {
        private Ellipse _motionDotX;
        private Ellipse _motionDotY;
        private Ellipse _motionDotZ;

        public static readonly StyledProperty<Vector3> MotionDataProperty =
            AvaloniaProperty.Register<MotionVisualizerControl, Vector3>(nameof(MotionData), default);

        public Vector3 MotionData
        {
            get => GetValue(MotionDataProperty);
            set => SetValue(MotionDataProperty, value);
        }

        public MotionVisualizerControl()
        {
            InitializeComponent();

            _motionDotX = this.FindControl<Ellipse>("MotionDotX");
            _motionDotY = this.FindControl<Ellipse>("MotionDotY");
            _motionDotZ = this.FindControl<Ellipse>("MotionDotZ");

            PropertyChanged += (s, e) =>
            {
                if (e.Property == MotionDataProperty)
                    UpdateMotionIndicator();
            };
        }

        private void UpdateMotionIndicator()
        {
            if (_motionDotX == null || _motionDotY == null || _motionDotZ == null)
                return;

            Vector3 motion = MotionData;

            const float MaxRange = 65f;
            const float CenterX = 90f;  
            const float CenterY = 90f;

            float xNormalized = Math.Max(-1f, Math.Min(1f, motion.X / MaxRange));
            float xPos = CenterX + (xNormalized * MaxRange) - 5;
            
            float yNormalized = Math.Max(-1f, Math.Min(1f, motion.Y / MaxRange));
            float yPos = CenterY + (yNormalized * MaxRange) - 5;
            
            float zMagnitude = Math.Max(-1f, Math.Min(1f, motion.Z / MaxRange));
            float zPos = CenterY + (zMagnitude * MaxRange) - 3;

            Canvas.SetLeft(_motionDotX, xPos);
            Canvas.SetTop(_motionDotX, CenterY - 5);

            Canvas.SetLeft(_motionDotY, CenterX - 4);
            Canvas.SetTop(_motionDotY, yPos);

            Canvas.SetLeft(_motionDotZ, CenterX - 3);
            Canvas.SetTop(_motionDotZ, zPos);
        }
    }
}

