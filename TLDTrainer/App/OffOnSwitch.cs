using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TLDTrainer.App
{
    // Переключатель Off / On, как в Wand.
    internal sealed class OffOnSwitch : Border
    {
        private readonly Border _off;
        private readonly Border _on;
        private bool _value;

        public event Action<bool> Changed;

        public OffOnSwitch()
        {
            Background = Theme.Control;
            CornerRadius = new CornerRadius(7);
            Padding = new Thickness(3);
            HorizontalAlignment = HorizontalAlignment.Right;
            Cursor = Cursors.Hand;

            _off = Segment("Off");
            _on = Segment("On");
            _off.MouseLeftButtonUp += (_, _) => Click(false);
            _on.MouseLeftButtonUp += (_, _) => Click(true);
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(_off);
            sp.Children.Add(_on);
            Child = sp;
            IsEnabledChanged += (_, _) => Opacity = IsEnabled ? 1 : 0.4;
            Value = false;
        }

        public bool Value
        {
            get => _value;
            set
            {
                _value = value;
                _off.Background = value ? Brushes.Transparent : Theme.ControlHi;
                _on.Background = value ? Theme.Accent : Brushes.Transparent;
                ((TextBlock)_off.Child).Foreground = value ? Theme.Muted : Theme.Text;
                ((TextBlock)_on.Child).Foreground = value ? Brushes.White : Theme.Muted;
            }
        }

        private void Click(bool v)
        {
            if (!IsEnabled || v == _value) return;
            Value = v;
            Changed?.Invoke(v);
        }

        private static Border Segment(string text) => new()
        {
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(16, 5, 16, 5),
            Child = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold },
        };
    }
}
