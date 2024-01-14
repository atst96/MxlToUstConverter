namespace MxlToUstConverter.Models.UTAU;

internal class SettingElement : IUtauElement
{
    public required decimal Tempo { get; set; }

    public required int Tracks { get; set; }

    public string VoiceDir { get; set; } = string.Empty;
}
