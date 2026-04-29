using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;

namespace Zametek.View.ProjectPlan
{
    public class MoveCaretToEndBehavior
        : Behavior<TextBox>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject is not null)
            {
                AssociatedObject.GotFocus += OnGotFocus;
            }
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            if (AssociatedObject is not null)
            {
                AssociatedObject.GotFocus -= OnGotFocus;
            }
        }

        private void OnGotFocus(object? sender, GotFocusEventArgs e)
        {
            if (AssociatedObject is { Text: not null } tb)
            {
                // Set the cursor to the end of the current text
                tb.CaretIndex = tb.Text.Length;
            }
        }
    }
}
