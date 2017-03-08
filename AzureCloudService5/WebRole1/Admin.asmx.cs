using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for Admin
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class Admin : System.Web.Services.WebService
    {

        private PerformanceCounter theCPUCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        private PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");

        //Read Both robot.txt's and load xml schema
        [WebMethod]
        public string GetRobotInfo()
        {
            CloudQueue q = ConnectToQueue("pause");
            if (q.PeekMessage() == null)
            {
                CloudQueue startstatus = ConnectToQueue("status");
                string[] sites = new string[] { "http://www.cnn.com/robots.txt", "http://bleacherreport.com/robots.txt" };
                CloudQueue queue = ConnectToQueue("xmlurls");
                foreach (string s in sites)
                {
                    CloudQueueMessage message = new CloudQueueMessage(s);
                    queue.AddMessage(message);
                }

            }
            else
            {
                q.DeleteMessage(q.GetMessage());
            }
            return "done";
        }

        [WebMethod]
        public void pause()
        {
            CloudQueue q = ConnectToQueue("pause");
            if (q.PeekMessage() == null)
            {
                CloudQueueMessage message = new CloudQueueMessage("pause");
                q.AddMessage(message);
                CloudQueue status = ConnectToQueue("status");
                if (status.PeekMessage() != null)
                {
                    status.DeleteMessage(status.GetMessage());
                }
                CloudQueueMessage m = new CloudQueueMessage("idle...");
                status.AddMessage(m);
            }
        }


        //Create the initial xml lis


        [WebMethod]
        public void ClearQueue()
        {
            string[] n = new String[] { "xmlurls", "htmlurls", "status", "topten", "totalcount", "error", "pause" };
            foreach (string s in n)
            {
                CloudQueue queue = ConnectToQueue(s);
                queue.Delete();
            }
        }

        public CloudQueue ConnectToQueue(string name)
        {
            try
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
            ConfigurationManager.AppSettings["StorageConnectionString"]);
                CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
                CloudQueue queue = queueClient.GetQueueReference(name);
                queue.CreateIfNotExists();
                return queue;
            }
            catch
            {
            }
            return null;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string retrieveNumbers()
        {
            List<string> results = new List<string>();
            CloudQueue xml = ConnectToQueue("xmlurls");
            CloudQueue html = ConnectToQueue("htmlurls");
            xml.FetchAttributes();
            html.FetchAttributes();
            results.Add(xml.ApproximateMessageCount.ToString());
            results.Add(html.ApproximateMessageCount.ToString());
            return new JavaScriptSerializer().Serialize(results);
        }


        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string retrieveStatus()
        {
            CloudQueue status = ConnectToQueue("status");
            List<string> list = new List<string>();
            if (status.PeekMessage() != null)
            {
                try
                {
                    list.Add(status.PeekMessage().AsString);
                }
                catch
                {
                }
            }
            return new JavaScriptSerializer().Serialize(list);
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string top10()
        {
            List<string> list = new List<string>();
            CloudQueue top = ConnectToQueue("topten");
            top.FetchAttributes();
            int n = (int)top.ApproximateMessageCount;
            for (int i = 0; i < n; i++)
            {
                try
                {
                    CloudQueueMessage message = top.GetMessage();
                    list.Add(message.AsString);
                    top.DeleteMessage(message);
                    top.AddMessage(message);
                }
                catch
                {

                }
            }
            return new JavaScriptSerializer().Serialize(list);
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getTotal()
        {
            List<string> list = new List<string>();
            CloudQueue top = ConnectToQueue("totalcount");
            if (top.PeekMessage() != null)
            {
                try
                {
                    list.Add(top.GetMessage().AsString);
                    return new JavaScriptSerializer().Serialize(list);
                }
                catch
                {
                }
            }
            list.Add("");
            return new JavaScriptSerializer().Serialize(list);
        }


        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getRAM()
        {
            List<string> list = new List<string>();
            list.Add(this.ramCounter.NextValue().ToString());
            return new JavaScriptSerializer().Serialize(list);

        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getCPU()
        {
            List<string> list = new List<string>();
            theCPUCounter.NextValue();
            Thread.Sleep(1000);
            list.Add(this.theCPUCounter.NextValue().ToString());
            return new JavaScriptSerializer().Serialize(list);

        }


        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string getError()
        {
            CloudQueue q = ConnectToQueue("error");
            List<string> s = new List<string>();
            if (q.PeekMessage() != null)
            {
                try
                {
                    s.Add(q.PeekMessage().AsString);
                    return new JavaScriptSerializer().Serialize(s);
                }
                catch
                {

                }
            }
            s.Add("");
            return new JavaScriptSerializer().Serialize(s);
        }


    }
}
