using System.Diagnostics;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class UriHelper
    {
        public static void Open(Uri uri)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
    }
}
