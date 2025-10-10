using System.Diagnostics;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class UriHelper
    {
        public readonly static Uri LinkMainPage = new(@"https://www.getprojectplan.net");

        private readonly static Uri s_LinkDocumentation = new(@"https://github.com/countincognito/Zametek.ProjectPlan/wiki");
        private readonly static Uri s_LinkDonate = new(@"https://github.com/countincognito/Zametek.ProjectPlan/blob/master/README.md");
        private readonly static Uri s_LinkReportIssue = new(@"https://github.com/countincognito/Zametek.ProjectPlan/issues");
        private readonly static Uri s_LinkViewLicense = new(@"https://github.com/countincognito/Zametek.ProjectPlan/blob/master/LICENSE");

        public static void OpenMainPage()
        {
            Open(LinkMainPage);
        }
        public static void OpenDocumentation()
        {
            Open(s_LinkDocumentation);
        }
        public static void OpenDonate()
        {
            Open(s_LinkDonate);
        }
        public static void OpenReportIssue()
        {
            Open(s_LinkReportIssue);
        }
        public static void OpenViewLicense()
        {
            Open(s_LinkViewLicense);
        }

        private static void Open(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
    }
}
