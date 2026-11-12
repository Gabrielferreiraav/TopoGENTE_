using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace TopoGente.UI.E2ETests.Infrastructure;

/// <summary>
/// Utilitários para inspeção da árvore visual WPF em testes E2E.
/// </summary>
internal static class VisualTreeTestHelper
{
    public static List<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var list = new List<T>();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                list.Add(typed);
            }
            list.AddRange(FindVisualChildren<T>(child));
        }
        return list;
    }
}
