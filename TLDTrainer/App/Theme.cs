using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace TLDTrainer.App
{
    // Тёмная тема в духе Wand. Стили задаются строкой XAML, чтобы не зависеть от компиляции разметки.
    internal static class Theme
    {
        public static readonly Brush Window = Hex("#0F1014");
        public static readonly Brush Panel = Hex("#16171D");
        public static readonly Brush Card = Hex("#1B1D24");
        public static readonly Brush Control = Hex("#262832");
        public static readonly Brush ControlHi = Hex("#3A3D48");
        public static readonly Brush Text = Hex("#E6E8EE");
        public static readonly Brush Muted = Hex("#8A8F9C");
        public static readonly Brush Accent = Hex("#2FB5A3");
        public static readonly Brush Warn = Hex("#E0A43A");
        public static readonly Brush Bad = Hex("#D9534F");
        public static readonly Brush Good = Hex("#3CC46A");

        private const string Styles = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style TargetType='Button'>
    <Setter Property='Foreground' Value='#E6E8EE'/>
    <Setter Property='Background' Value='#2A2C35'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Padding' Value='14,7'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='b' Background='{TemplateBinding Background}' CornerRadius='6' Padding='{TemplateBinding Padding}'>
            <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.82'/></Trigger>
            <Trigger Property='IsPressed' Value='True'><Setter TargetName='b' Property='Opacity' Value='0.65'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter TargetName='b' Property='Opacity' Value='0.35'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='TextBox'>
    <Setter Property='Foreground' Value='#E6E8EE'/>
    <Setter Property='Background' Value='#262832'/>
    <Setter Property='BorderBrush' Value='#3A3D48'/>
    <Setter Property='CaretBrush' Value='#E6E8EE'/>
    <Setter Property='Padding' Value='8,6'/>
    <Setter Property='FontSize' Value='13'/>
  </Style>
  <Style TargetType='ToolTip'>
    <Setter Property='Foreground' Value='#E6E8EE'/>
    <Setter Property='Background' Value='#262832'/>
    <Setter Property='BorderBrush' Value='#3A3D48'/>
  </Style>
  <Style TargetType='ScrollBar'>
    <Setter Property='Width' Value='8'/>
    <Setter Property='MinWidth' Value='8'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ScrollBar'>
          <Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='{TemplateBinding Orientation}'>
            <Track.Thumb>
              <Thumb>
                <Thumb.Template>
                  <ControlTemplate TargetType='Thumb'>
                    <Border Background='#3A3D48' CornerRadius='4'/>
                  </ControlTemplate>
                </Thumb.Template>
              </Thumb>
            </Track.Thumb>
          </Track>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";

        public static void Apply(Application app)
        {
            app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(Styles));
        }

        public static SolidColorBrush Hex(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }

        public static TextBlock Label(string text, double size = 13, Brush color = null, bool bold = false)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = color ?? Text,
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        public static Button Button(string text, Brush background = null)
        {
            var b = new Button { Content = text };
            if (background != null) b.Background = background;
            return b;
        }

        public static Border CardBorder(UIElement child)
        {
            return new Border
            {
                Background = Card,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 0, 14),
                Child = child,
            };
        }
    }
}
