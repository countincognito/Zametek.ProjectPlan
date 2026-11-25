using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan.Tests
{
    /// <summary>
    /// An edit of the dialog that changes only the switch IsHiddenFromCharts needs no compile, and any other mark of an edit
    /// does. The marks are read from each model, so that a mark that is added later and that a check does not name fails
    /// here.
    /// </summary>
    public class ChartVisibilityEditTests
    {
        private const string c_SwitchMark = nameof(UpdateDependentActivityModel.IsIsHiddenFromChartsEdited);

        public static TheoryData<string> ActivityMarks => Marks<UpdateDependentActivityModel>();

        public static TheoryData<string> ResourceMarks => Marks<UpdateResourceModel>();

        public static TheoryData<string> WorkStreamMarks => Marks<UpdateWorkStreamModel>();

        [Fact]
        public void IsOnly_Given_TheMarkOfTheSwitchAlone_Then_True()
        {
            ChartVisibilityEdit.IsOnly(new UpdateDependentActivityModel { IsIsHiddenFromChartsEdited = true }).ShouldBeTrue();
            ChartVisibilityEdit.IsOnly(new UpdateResourceModel { IsIsHiddenFromChartsEdited = true }).ShouldBeTrue();
            ChartVisibilityEdit.IsOnly(new UpdateWorkStreamModel { IsIsHiddenFromChartsEdited = true }).ShouldBeTrue();
        }

        [Fact]
        public void IsOnly_Given_NoMark_Then_False()
        {
            ChartVisibilityEdit.IsOnly(new UpdateDependentActivityModel()).ShouldBeFalse();
            ChartVisibilityEdit.IsOnly(new UpdateResourceModel()).ShouldBeFalse();
            ChartVisibilityEdit.IsOnly(new UpdateWorkStreamModel()).ShouldBeFalse();
        }

        [Theory]
        [MemberData(nameof(ActivityMarks))]
        public void IsOnly_Given_TheSwitchAndAnotherMarkOfAnActivity_Then_False(string mark)
        {
            ChartVisibilityEdit.IsOnly(WithMarks<UpdateDependentActivityModel>(c_SwitchMark, mark)).ShouldBeFalse();
        }

        [Theory]
        [MemberData(nameof(ResourceMarks))]
        public void IsOnly_Given_TheSwitchAndAnotherMarkOfAResource_Then_False(string mark)
        {
            ChartVisibilityEdit.IsOnly(WithMarks<UpdateResourceModel>(c_SwitchMark, mark)).ShouldBeFalse();
        }

        [Theory]
        [MemberData(nameof(WorkStreamMarks))]
        public void IsOnly_Given_TheSwitchAndAnotherMarkOfAWorkStream_Then_False(string mark)
        {
            ChartVisibilityEdit.IsOnly(WithMarks<UpdateWorkStreamModel>(c_SwitchMark, mark)).ShouldBeFalse();
        }

        [Fact]
        public void Marks_Given_EachModel_Then_EachHasTheMarkOfTheSwitchAndOthers()
        {
            // The theories above are empty, and prove nothing, if the reflection finds no mark.
            MarkNames<UpdateDependentActivityModel>().Count().ShouldBe(11);
            MarkNames<UpdateResourceModel>().Count().ShouldBe(12);
            MarkNames<UpdateWorkStreamModel>().Count().ShouldBe(2);

            typeof(UpdateResourceModel).GetProperty(c_SwitchMark).ShouldNotBeNull();
            typeof(UpdateWorkStreamModel).GetProperty(c_SwitchMark).ShouldNotBeNull();
        }

        // Each mark of an edit of the model (a bool named Is...Edited or Is...Active), but the mark of the switch.
        private static IEnumerable<string> MarkNames<T>() =>
            typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(x => x.PropertyType == typeof(bool)
                    && x.Name.StartsWith(@"Is", StringComparison.Ordinal)
                    && (x.Name.EndsWith(@"Edited", StringComparison.Ordinal) || x.Name.EndsWith(@"Active", StringComparison.Ordinal))
                    && x.Name != c_SwitchMark)
                .Select(x => x.Name);

        private static TheoryData<string> Marks<T>()
        {
            var data = new TheoryData<string>();
            foreach (string name in MarkNames<T>())
            {
                data.Add(name);
            }
            return data;
        }

        private static T WithMarks<T>(params string[] marks)
            where T : new()
        {
            var model = new T();
            foreach (string mark in marks)
            {
                typeof(T).GetProperty(mark)!.SetValue(model, true);
            }
            return model;
        }
    }
}
