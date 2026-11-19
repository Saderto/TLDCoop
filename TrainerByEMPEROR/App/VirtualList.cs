using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace TrainerByEMPEROR.App
{
    // Длинный список, у которого строятся только видимые строки. Раньше разделы строили все строки сразу
    // (сотни карточек с кнопками) на каждую букву поиска — окно подвисало при вводе и прокрутке.
    internal sealed class VirtualList : ItemsControl
    {
        private sealed class Lazy
        {
            public Func<FrameworkElement> Build;
        }

        private sealed class Host : ContentControl
        {
            public Host()
            {
                Focusable = false;
                DataContextChanged += (_, e) => Content = (e.NewValue as Lazy)?.Build();
            }
        }

        public VirtualList()
        {
            Focusable = false;
            VirtualizingPanel.SetIsVirtualizing(this, true);
            VirtualizingPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
            VirtualizingPanel.SetScrollUnit(this, ScrollUnit.Pixel);
            VirtualizingPanel.SetCacheLengthUnit(this, VirtualizationCacheLengthUnit.Page);
            VirtualizingPanel.SetCacheLength(this, new VirtualizationCacheLength(1));
            ScrollViewer.SetCanContentScroll(this, true);

            ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
            ItemTemplate = new DataTemplate { VisualTree = new FrameworkElementFactory(typeof(Host)) };

            var sv = new FrameworkElementFactory(typeof(ScrollViewer));
            sv.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            sv.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            sv.SetValue(ScrollViewer.CanContentScrollProperty, true);
            sv.SetValue(Control.PaddingProperty, new Thickness(0, 0, 10, 0));
            sv.SetValue(UIElement.FocusableProperty, false);
            sv.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
            Template = new ControlTemplate(typeof(ItemsControl)) { VisualTree = sv };
        }

        public void Show<T>(IEnumerable<T> items, Func<T, FrameworkElement> build)
        {
            ItemsSource = items.Select(i => new Lazy { Build = () => build(i) }).ToList();
        }

        public void Clear() => ItemsSource = null;

        // Поиск запускается не на каждую букву, а когда ввод на миг остановился.
        public static void Debounce(TextBox box, Action action, int ms = 180)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                action();
            };
            box.TextChanged += (_, _) =>
            {
                t.Stop();
                t.Start();
            };
        }
    }
}
