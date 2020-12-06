using System.IO;
using System;

namespace ExcelExporter
{
   class Program
   {

      private static string _strApplicationPath;

      static void Main(string[] args)
      {
         _strApplicationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + "\\";
         AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(ShowUnhandledException);
         RunExporter(args);
      }


      static void RunExporter(string[] args)
      { 
         string strAction = "", strPath = "", strKey = "";

         try
         {
            strAction = args[0];
            strPath = args[1];
            strKey = args[2];
         }
         catch
         {
            Console.WriteLine("Wrong input!");
            Console.WriteLine("[Extract|Build] FILE KEY");
            return;
         }

         switch (strAction)
         {
            case "Extract":
               if (!File.Exists(strPath))
               {
                  Console.WriteLine("File does not exist!");
                  return;
               }
               break;
            case "Build":
               break;
            default:
               Console.WriteLine("Wrong input!");
               Console.WriteLine("[Extract|Build] FILE KEY");
               return;
         }

         if (strKey.Length == 0)
         {
            Console.WriteLine("Key has not been defined!");
            return;
         }

         DBLoader dbl = new DBLoader();
         dbl.ApplicationPath = _strApplicationPath;
         dbl.ProgressStart(strAction, strPath, strKey);
      }


      static void ShowUnhandledException(object sender, UnhandledExceptionEventArgs e)
      {
         try
         {
            Exception ex = (Exception)e.ExceptionObject;
            // new line: \r\n or  Environment.NewLine
            string strLastExceptionMessage = "";
            string strExceptionStackTrace = ex.StackTrace;
            Exception ie = ex.InnerException;
            System.Text.StringBuilder msg = new System.Text.StringBuilder("----An error occured----\r\n");
            msg.Append(ex.Message);
            strLastExceptionMessage = ex.Message;
            while (ie != null)
            {
               if (strLastExceptionMessage != ie.Message)
               {
                  msg.AppendFormat("\r\n\r\n----Inner error----\r\n{0}", ie.Message);
                  strLastExceptionMessage = ie.Message;
               }
               strExceptionStackTrace = ie.StackTrace;
               ie = ie.InnerException;
            }
            msg.AppendFormat("\r\n\r\n----Stacktrace----\r\n{0}", strExceptionStackTrace);
            StreamWriter sw = new StreamWriter(_strApplicationPath + "Error.txt");
            sw.Write(msg.ToString());
            sw.Close();
            sw.Dispose();
         }
         finally
         {
            //Application.Exit();
         }
      }

   }
}
