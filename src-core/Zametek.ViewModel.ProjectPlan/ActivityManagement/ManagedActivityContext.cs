using System.Windows;
using System.Windows.Data;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// https://stackoverflow.com/questions/3862385/wpf-validationrule-with-dependency-property
    /// </summary>
    public class ManagedActivityContext
        : DependencyObject
    {
        public int Id
        {
            get { return (int)GetValue(IdProperty); }
            set { SetValue(IdProperty, value); }
        }

        public static readonly DependencyProperty IdProperty =
            DependencyProperty.Register(nameof(Id), typeof(int), typeof(ManagedActivityContext), new PropertyMetadata(default(int), OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ManagedActivityContext context = (ManagedActivityContext)d;
            BindingExpressionBase bindingExpressionBase = BindingOperations.GetBindingExpressionBase(context, BindingToTriggerProperty);
            bindingExpressionBase?.UpdateSource();
        }

        public object BindingToTrigger
        {
            get { return GetValue(BindingToTriggerProperty); }
            set { SetValue(BindingToTriggerProperty, value); }
        }

        public static readonly DependencyProperty BindingToTriggerProperty = DependencyProperty.Register(
            nameof(BindingToTrigger),
            typeof(object),
            typeof(ManagedActivityContext),
            new FrameworkPropertyMetadata(default(object), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    }
}
