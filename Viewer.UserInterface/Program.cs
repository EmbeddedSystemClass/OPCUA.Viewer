//_______________________________________________________________
//  Title   : main Program of OPC.UA.Viewer
//  System  : Microsoft VisualStudio 2015 / C#
//  $LastChangedDate:  $
//  $Rev: $
//  $LastChangedBy: $
//  $URL: $
//  $Id:  $
//
//  Copyright (C) 2016, CAS LODZ POLAND.
//  TEL: +48 (42) 686 25 47
//  mailto://techsupp@cas.eu
//  http://www.cas.eu
//_______________________________________________________________


using CAS.CommServer.UA.Viewer.UserInterface.Properties;
using CAS.Lib.CodeProtect;
using Opc.Ua;
using Opc.Ua.Configuration;
using System;
using System.Windows.Forms;

namespace CAS.CommServer.UA.Viewer.UserInterface
{
  static class Program
  {
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      ApplicationInstance application = new ApplicationInstance();
      application.ApplicationName = "CommServer OPC UA Viewer";
      application.ApplicationType = ApplicationType.Client;
      application.ConfigSectionName = Settings.Default.ConfigurationSectionName;
      try
      {
        string m_cmmdLine = Environment.CommandLine;
        if (m_cmmdLine.ToLower().Contains("installic"))
          LibInstaller.InstalLicense(true);
      }
      catch (Exception ex)
      {
        MessageBox.Show(
          string.Format(Resources.MainProgram_LicenseInstalation_Failure, ex.Message),
          Resources.MainProgram_LicenseInstalation_Failure_Caption,
          MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      try
      {
        // process the command line arguments.
        if (application.ProcessCommandLine(true))
          return;
        // load the application configuration.
        application.LoadApplicationConfiguration(false);
        // check the application certificate.
        application.CheckApplicationInstanceCertificate(false, 0);
        // start the server.
        //application.Start(new SampleServer());
        // run the application interactively.
        Application.Run(new OpcUaClientForm(application, null, application.ApplicationConfiguration));
      }
      catch (Exception e)
      {
        ExceptionDlg.Show(application.ApplicationName, e);
      }
    }
  }
}
