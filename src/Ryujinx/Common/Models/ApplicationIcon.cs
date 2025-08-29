namespace Ryujinx.Ava.Common.Models
{
    public class ApplicationIcon
    {
        public string Name { get; set; }
        public string Filename { get; set; }
        public string FullPath
        {
            get => $"Ryujinx/Assets/Icons/AppIcons/{Filename}";
        }
    }
}