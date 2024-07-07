using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using System;

namespace Zametek.View.ProjectPlan
{
    // https://github.com/AvaloniaUI/Avalonia/issues/6071
    // Temporary fix until UpdateSourceTrigger is available in Avalonia
    // https://docs.avaloniaui.net/docs/basics/data/data-binding/data-binding-syntax#updatesourcetrigger-
    public class LostFocusUpdateBindingBehavior
        : Behavior<TextBox>
    {
        static LostFocusUpdateBindingBehavior()
        {
            TextProperty.Changed.Subscribe(e =>
            {
                ((LostFocusUpdateBindingBehavior)e.Sender).OnBindingValueChanged();
            });
        }


        public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<LostFocusUpdateBindingBehavior, string>(
            "Text", defaultBindingMode: BindingMode.TwoWay);

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        protected override void OnAttached()
        {
            TextBox? cp = AssociatedObject;

            if (cp is not null)
            {
                cp.LostFocus += OnLostFocus;
            }

            base.OnAttached();
        }

        protected override void OnDetaching()
        {
            TextBox? cp = AssociatedObject;

            if (cp is not null)
            {
                cp.LostFocus -= OnLostFocus;
            }

            base.OnDetaching();
        }

        private void OnLostFocus(object? sender, RoutedEventArgs e)
        {
            if (AssociatedObject is not null)
            {
                Text = AssociatedObject.Text ?? string.Empty;
            }
        }

        private void OnBindingValueChanged()
        {
            if (AssociatedObject is not null)
            {
                AssociatedObject.Text = Text;
            }
        }
    }
}
