using System;
using StardewModdingAPI;

namespace AnimatedPortraitFramework.Framework
{
    /// <summary>
    /// Minimal GMCM API interface (only methods we use).
    /// Signatures must match GMCM's actual API exactly for Pintail proxying.
    /// </summary>
    public interface IGmcmApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string> tooltip = null, string[] allowedValues = null, Func<string, string> formatAllowedValue = null, string fieldId = null);
    }
}
