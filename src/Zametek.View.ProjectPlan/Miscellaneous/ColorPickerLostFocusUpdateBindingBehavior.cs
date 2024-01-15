using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace Zametek.View.ProjectPlan
{
    public class ColorPickerLostFocusUpdateBindingBehavior
        : Behavior<ColorPicker>
    {
        static ColorPickerLostFocusUpdateBindingBehavior()
        {
            ColorProperty.Changed.ObserveOn(RxApp.MainThreadScheduler).Subscribe(e =>
            {
                ((ColorPickerLostFocusUpdateBindingBehavior)e.Sender).OnBindingValueChanged();
            });
        }

        public static readonly StyledProperty<Color> ColorProperty = AvaloniaProperty.Register<ColorPickerLostFocusUpdateBindingBehavior, Color>(
            "Color", defaultBindingMode: BindingMode.TwoWay);

        public Color Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        protected override void OnAttached()
        {
            ColorPicker? cp = AssociatedObject;

            if (cp is not null)
            {
                cp.LostFocus += OnLostFocus;
            }

            base.OnAttached();
        }

        protected override void OnDetaching()
        {
            ColorPicker? cp = AssociatedObject;

            if (cp is not null)
            {
                cp.LostFocus -= OnLostFocus;
            }

            base.OnDetaching();
        }

        private void OnLostFocus(object? sender, RoutedEventArgs e)
        {
            if (AssociatedObject != null)
            {
                Color = AssociatedObject.Color;
            }
        }

        private void OnBindingValueChanged()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.Color = Color;
            }
        }
    }
}
