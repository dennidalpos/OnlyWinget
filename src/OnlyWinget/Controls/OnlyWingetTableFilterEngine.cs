using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace OnlyWinget.Controls;

internal sealed class OnlyWingetTableFilterEngine
{
    private readonly Dictionary<string, string> columnFilters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Type, Dictionary<string, PropertyInfo?>> propertyCache = [];
    private readonly Dictionary<Type, Dictionary<string, Func<object, object?>?>> getterCache = [];

    public ObservableCollection<object> FilteredItems { get; } = [];

    public void SetColumnFilter(string bindingPath, string filterText)
    {
        if (string.IsNullOrWhiteSpace(filterText))
        {
            columnFilters.Remove(bindingPath);
        }
        else
        {
            columnFilters[bindingPath] = filterText.Trim();
        }
    }

    public void ClearColumnFilters()
    {
        columnFilters.Clear();
    }

    public Func<object, object?>? GetCachedGetter(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type,
        string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return null;
        }

        if (!getterCache.TryGetValue(type, out var typeGetters))
        {
            typeGetters = new Dictionary<string, Func<object, object?>?>(StringComparer.Ordinal);
            getterCache[type] = typeGetters;
        }

        if (typeGetters.TryGetValue(propertyName, out var cachedGetter))
        {
            return cachedGetter;
        }

        var prop = GetCachedProperty(type, propertyName);
        if (prop == null || !prop.CanRead)
        {
            typeGetters[propertyName] = null;
            return null;
        }

        var param = Expression.Parameter(typeof(object), "item");
        var castParam = Expression.Convert(param, type);
        var propertyAccess = Expression.Property(castParam, prop);
        var castResult = Expression.Convert(propertyAccess, typeof(object));
        Func<object, object?> getter = Expression.Lambda<Func<object, object?>>(castResult, param).Compile();

        typeGetters[propertyName] = getter;
        return getter;
    }

    private PropertyInfo? GetCachedProperty(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type,
        string propertyName)
    {
        if (!propertyCache.TryGetValue(type, out var typeProperties))
        {
            typeProperties = new Dictionary<string, PropertyInfo?>(StringComparer.Ordinal);
            propertyCache[type] = typeProperties;
        }

        if (!typeProperties.TryGetValue(propertyName, out var prop))
        {
            prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            typeProperties[propertyName] = prop;
        }

        return prop;
    }

    public void ApplyFilters(IEnumerable? itemsSource)
    {
        FilteredItems.Clear();
        if (itemsSource == null)
        {
            return;
        }

        foreach (var item in itemsSource)
        {
            if (item == null)
            {
                continue;
            }

            if (MatchesFilters(item))
            {
                FilteredItems.Add(item);
            }
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Reflection on UI model item types for column filtering.")]
    public bool MatchesFilters(object item)
    {
        if (columnFilters.Count == 0)
        {
            return true;
        }

        var type = item.GetType();
        foreach (var (bindingPath, filterText) in columnFilters)
        {
            var getter = GetCachedGetter(type, bindingPath);
            if (getter == null)
            {
                return false;
            }

            var val = getter(item)?.ToString();
            if (val == null || val.IndexOf(filterText, StringComparison.CurrentCultureIgnoreCase) < 0)
            {
                return false;
            }
        }

        return true;
    }
}
