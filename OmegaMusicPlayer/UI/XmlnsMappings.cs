using Avalonia.Metadata;

// This assembly has an XML namespace that maps to the specified CLR namespace
[assembly: XmlnsDefinition("https://github.com/avaloniaui", "OmegaMusicPlayer.UI.Markup")]

// This makes the markup extension available directly in the XAML namespace
[assembly: XmlnsDefinition("https://schemas.microsoft.com/winfx/2006/xaml", "OmegaMusicPlayer.UI.Markup")]

// This adds a prefix for the namespace (optional but can make things clearer)
[assembly: XmlnsPrefix("https://github.com/avaloniaui", "app")]

namespace OmegaMusicPlayer.UI
{
    // This class doesn't need any content, it's just here
    // to provide a home for the assembly-level attributes
    internal class XmlnsMappings
    {
    }
}