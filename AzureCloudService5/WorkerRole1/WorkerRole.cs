using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Configuration;
using System.IO;

namespace WorkerRole1
{

        public class WorkerRole : RoleEntryPoint
        {
            private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);
            private List<string> cnnDis;
            private List<string> bleachDis;
            private HashSet<string> visitedUrls = new HashSet<string>();
            int count = 0;


            public override void Run()
            {

                Thread.Sleep(50);
                CloudQueue q = ConnectToQueue("pause");
                if (q.PeekMessage() == null)
                {
                    if (cnnDis == null || bleachDis == null)
                    {
                        string allBleach = GetWebText("http://bleacherreport.com/robots.txt");
                        string allCnn = GetWebText("http://www.cnn.com/robots.txt");
                        cnnDis = getDisallows(allCnn.Split('\n'));
                        bleachDis = getDisallows(allBleach.Split('\n'));
                    }
                    Debug.Print("started program");
                    CloudQueue htmlqueue = ConnectToQueue("htmlurls");
                    CloudQueue xmlqueue = ConnectToQueue("xmlurls");
                    if (StatusExists())
                    {
                        DeleteStatus();
                    }
                    AddStatusMessage("loading");

                    while (xmlqueue.PeekMessage() != null)
                    {

                        CloudQueueMessage message = xmlqueue.PeekMessage();
                        CloudQueueMessage retrievedMessage = xmlqueue.GetMessage();

                        //Process the message in less than 30 seconds, and then delete the message
                        try
                        {
                            string url = message.AsString;
                            if (!visitedUrls.Contains(url))
                            {
                                visitedUrls.Add(url);
                                WebCrawler crawlPage = new WebCrawler(cnnDis, bleachDis, url, visitedUrls);
                                visitedUrls = crawlPage.CrawlGeneral();
                            }
                        }
                        catch
                        {

                        }

                        xmlqueue.DeleteMessage(retrievedMessage);


                    }
                    DeleteStatus();
                    AddStatusMessage("crawling");
                    Debug.Print("phase 2");
                    while (htmlqueue.PeekMessage() != null && xmlqueue.PeekMessage() == null)
                    {

                        CloudQueueMessage message = htmlqueue.PeekMessage();
                        CloudQueueMessage retrievedMessage = htmlqueue.GetMessage();
                        try
                        {
                            string url = message.AsString;
                            Debug.Print("url to search " + url);
                            if (!visitedUrls.Contains(url))
                            {
                                visitedUrls.Add(url);

                                WebCrawler crawlPage = new WebCrawler(cnnDis, bleachDis, url, visitedUrls);
                                visitedUrls = crawlPage.CrawlGeneral();
                                CloudQueue total = ConnectToQueue("totalcount");
                                if (total.PeekMessage() != null)
                                {
                                    total.DeleteMessage(total.GetMessage());
                                }
                                count++;
                                CloudQueueMessage messageTotal = new CloudQueueMessage(count.ToString());
                                total.AddMessage(messageTotal);

                            }
                        }
                        catch (Exception e)
                        {
                            string url = message.AsString;
                            CloudQueue qu = ConnectToQueue("error");
                            CloudQueueMessage m = new CloudQueueMessage(e + "<br>" + url);
                            if (qu.PeekMessage() != null)
                            {
                                qu.DeleteMessage(q.GetMessage());
                            }
                            qu.AddMessage(m);
                        }

                        htmlqueue.DeleteMessage(retrievedMessage);

                    }
                    DeleteStatus();

                    AddStatusMessage("idle");
                }



            }



            public string getTotalQueue(CloudQueue xml, CloudQueue html)
            {
                return (xml.ApproximateMessageCount + html.ApproximateMessageCount).ToString();
            }


            public CloudQueue ConnectToQueue(string name)
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
                CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
                CloudQueue queue = queueClient.GetQueueReference(name);
                queue.CreateIfNotExists();
                return queue;
            }


            public void AddStatusMessage(string mess)
            {
                CloudQueue status = ConnectToQueue("status");
                CloudQueueMessage statusMessage = new CloudQueueMessage(mess);
                status.AddMessage(statusMessage);

            }

            public void DeleteStatus()
            {
                CloudQueue status = ConnectToQueue("status");
                CloudQueueMessage statusMessage = status.GetMessage();
                status.DeleteMessage(statusMessage);
            }


            public bool StatusExists()
            {
                CloudQueue status = ConnectToQueue("status");
                return status.PeekMessage() != null;
            }

            public override bool OnStart()
            {
                // Set the maximum number of concurrent connections
                ServicePointManager.DefaultConnectionLimit = 12;

                // For information on handling configuration changes
                // see the MSDN topic at https://go.microsoft.com/fwlink/?LinkId=166357.

                bool result = base.OnStart();

                Trace.TraceInformation("WorkerRole1 has been started");

                return result;
            }

            public override void OnStop()
            {
                Trace.TraceInformation("WorkerRole1 is stopping");

                this.cancellationTokenSource.Cancel();
                this.runCompleteEvent.WaitOne();

                base.OnStop();

                Trace.TraceInformation("WorkerRole1 has stopped");
            }

            private async Task RunAsync(CancellationToken cancellationToken)
            {
                // TODO: Replace the following with your own logic.
                while (!cancellationToken.IsCancellationRequested)
                {
                    Trace.TraceInformation("Working");
                    await Task.Delay(1000);
                }
            }


            public List<string> getDisallows(string[] content)
            {
                List<string> dis = new List<string>();
                foreach (string s in content)
                {
                    string field = getContent(s, false);
                    if (field == "disallow")
                    {
                        dis.Add(validParam(getContent(s, true)));
                    }
                }
                return dis;
            }

            //trim the string till a valid disallow or URL is reached
            public string validParam(string param)
            {
                if (char.IsLetter(param[0]) || param[0] == '/')
                {
                    return param;
                }
                else
                {
                    return validParam(param.Substring(1));
                }
            }

            //Retrieve the field
            public string getContent(string line, bool retrieveField)
            {
                var index = line.IndexOf(':');
                if (index == -1)
                {
                    return string.Empty;
                }
                if (!retrieveField)
                {
                    return line.Substring(0, index).ToLower();
                }
                else
                {
                    return line.Substring(index);
                }

            }

            private static string GetWebText(string url)
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.UserAgent = "A .NET Web Crawler";
                WebResponse response = request.GetResponse();
                Stream stream = response.GetResponseStream();
                StreamReader reader = new StreamReader(stream);
                string htmlText = reader.ReadToEnd();
                return htmlText;
            }
        }
}
