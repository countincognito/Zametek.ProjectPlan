using System;
using System.Windows;

namespace Zametek.Client.ProjectPlan.Wpf.Shell
{
    public partial class App
        : IDisposable
    {
        #region Fields

        private bool m_Disposed;
        private Bootstrapper m_Bootstrapper;

        #endregion

        #region Private methods

        private void RunApplication()
        {
            AppDomain.CurrentDomain.UnhandledException += AppDomainUnhandledException;
            try
            {
                m_Bootstrapper.Run();
            }
            catch (Exception ex)
            {
                Bootstrapper.HandleException(ex);
            }
        }

        private static void AppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Bootstrapper.HandleException(e.ExceptionObject as Exception);
        }

        #endregion

        #region Overrides

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            m_Bootstrapper = new Bootstrapper();
            RunApplication();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }
            if (disposing)
            {
                m_Bootstrapper.Dispose();
            }

            // Free any unmanaged objects here. 

            m_Disposed = true;
        }

        #endregion
    }
}
