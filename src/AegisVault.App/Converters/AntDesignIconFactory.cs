using System;
using System.Collections.Generic;
using System.Linq;
using AtomUI.Controls;
using AtomUI.Icons.AntDesign;

namespace AegisVault.App.Converters;

/// <summary>
/// Creates Ant Design icons by name. Only the creator functions are cached: an
/// <see cref="Icon"/> instance must never be shared, otherwise a second binding
/// throws "already has a visual parent" (see pitfall P-10).
/// </summary>
public static class AntDesignIconFactory
{
    private static readonly Dictionary<string, Func<Icon>?> Cache = new(StringComparer.Ordinal);

    public static Icon? Create(string name, double size = 16)
    {
        var icon = GetCreator(name)?.Invoke();
        if (icon is not null)
        {
            icon.Width = size;
            icon.Height = size;
        }

        return icon;
    }

    private static Func<Icon>? GetCreator(string name)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            var info = AntDesignIconCatalog.GetIcons()
                .FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
            var creator = info.Name is null ? null : info.Creator;
            Cache[name] = creator;
            return creator;
        }
    }
}
