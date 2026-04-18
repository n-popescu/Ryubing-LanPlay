using Ryujinx.Ava.UI.ViewModels;
using System.Collections.Generic;

namespace Ryujinx.Ava.UI.Models.Input
{
    public partial class MotionRotationOptionsConfig : BaseModel
    {
        public string Name { get; set; } = string.Empty;
        public int Id { get; set; }

        public MotionRotationOptionsConfig(string name, int id)
        {
            Name = name;
            Id = id;
        }

        public static List<MotionRotationOptionsConfig> RotationOptions { get; } = new List<MotionRotationOptionsConfig>
        {
            new("0°", 0),
            new("90°", 90),
            new("180°", 180),
            new("270°", 270)
        };
    }
}
