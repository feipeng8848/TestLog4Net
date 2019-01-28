using log4net;
using log4net.Config;
using log4net.Repository;
using System;
using System.Configuration;
using System.IO;

namespace TestLog4Net
{
    class Program
    {
        static ILog LogDebug = null;
        static ILog LogInfo = null;
        static ILog LogError = null;
        static void Main(string[] args)
        {
            InitLog4Net();


            Console.WriteLine("Hello World!");
            ReadAllSettings();
            Console.ReadKey();

        }


        static void InitLog4Net()
        {
            ILoggerRepository repository = LogManager.CreateRepository("NETCoreRepository");
            XmlConfigurator.Configure(repository, new FileInfo("log4net.config"));            
            LogDebug = LogManager.GetLogger(repository.Name, "logDebug");
            LogInfo = LogManager.GetLogger(repository.Name, "loginfo");
            LogError = LogManager.GetLogger(repository.Name, "logError");
        }


        static void ReadAllSettings()
        {
            
            LogDebug.Debug("logDebug");
            LogInfo.Info("loginfo");
            LogError.Error("error");
            
            
            try
            {
                var appSettings = ConfigurationManager.AppSettings;

                if (appSettings.Count == 0)
                {
                    Console.WriteLine("AppSettings is empty.");
                }
                else
                {
                    foreach (var key in appSettings.AllKeys)
                    {
                        Console.WriteLine("Key: {0} Value: {1}", key, appSettings[key]);
                    }
                }
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error reading app settings");
            }
        }
    }
}
