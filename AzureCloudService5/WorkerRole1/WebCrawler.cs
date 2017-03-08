using HtmlAgilityPack;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;

namespace WorkerRole1
{
    public class WebCrawler
    {
        private List<string> cnnRules;
        private List<string> bleachRules;
        private HashSet<string> visitedUrls;
        private CloudQueue top10;
        private string url;

        public WebCrawler(List<string> cnnRules, List<string> bleachRules, string url, HashSet<string> visited)
        {
            this.cnnRules = cnnRules;
            this.bleachRules = bleachRules;
            this.visitedUrls = visited;
            this.url = url;
            this.top10 = ConnectToQueue("topten");

        }

        public HashSet<string> CrawlGeneral()
        {

            if (url.ToLower().Contains("xml"))
            {
                CrawlXML(GetSite());
            }
            else if (url.ToLower().Contains("robots.txt"))
            {
                CrawlTXT();
            }
            else
            {
                CrawlHTML();
            }
            addToTop10(url);
            return visitedUrls;
        }

        public void CrawlTXT()
        {
            CloudQueue queue = ConnectToQueue("xmlurls");
            string allText = GetWebText(url);
            string[] sections = allText.Split('\n');
            List<string> sitemaps = addRobotSiteMap(sections);
            foreach (string s in sitemaps)
            {
                CloudQueueMessage message = new CloudQueueMessage(s);
                queue.AddMessage(message);
            }
        }

        public void CrawlHTML()
        {
            if (UrlIsValid(url))
            {
                try
                {
                    CloudQueue queue = ConnectToQueue("htmlurls");
                    var doc = new HtmlWeb().Load(url);
                    var linkTags = doc.DocumentNode.Descendants("link");
                    var title = doc.DocumentNode.Descendants("title").FirstOrDefault().InnerText;
                    ConnectToTable(this.url, title);
                    var linkedPages = doc.DocumentNode.Descendants("a")
                                                        .Select(a => a.GetAttributeValue("href", null))
                                                        .Where(u => !string.IsNullOrEmpty(u) &&
                                                        !visitedUrls.Contains(u));
                    Uri current = new Uri(url);
                    foreach (string s in linkedPages)
                    {
                        string lower = s.ToLower();
                        if (s.StartsWith("/"))
                        {
                            string left = current.GetLeftPart(UriPartial.Authority);
                            lower = left + lower;
                            if (UrlIsValid(lower))
                            {
                                visitedUrls.Add(s);
                            }

                        }
                        if (UriIsValid(lower))
                        {
                            if (!containsRule(GetSite(), lower) && (lower.IndexOf("cnn.com") >= 0 || lower.IndexOf("bleacherreport.com") >= 0))
                            {
                                CloudQueueMessage message = new CloudQueueMessage(lower);
                                queue.AddMessage(message);
                                visitedUrls.Add(lower);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    CloudQueue q = ConnectToQueue("error");
                    CloudQueueMessage m = new CloudQueueMessage(e + "<br>" + url);
                    if (q.PeekMessage() != null)
                    {
                        q.DeleteMessage(q.GetMessage());
                    }
                    q.AddMessage(m);
                }
            }
        }




        public void CrawlXML(List<string> rules)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(url);

            // Get elements
            XmlNodeList xmlUrls = xmlDoc.GetElementsByTagName("loc");
            CloudQueue xmlqueue = ConnectToQueue("xmlurls");
            CloudQueue htmlqueue = ConnectToQueue("htmlurls");

            DateTime twoMonths = new DateTime(2016, 12, 1);

            foreach (var x in xmlUrls)
            {
                string siteUrl = ((XmlNode)x).InnerText.ToLower();
                if (!rules.Any(c => siteUrl.Contains(c.ToLower())))
                {
                    if (siteUrl.IndexOf("cnn") >= 0)
                    {
                        var regex = new Regex(@"(\d+)[-.\/](\d+)[-.\/](\d+)");
                        var regex2 = new Regex(@"(\d+)[-.\/](\d+)");
                        Match match = regex.Match(siteUrl);
                        Match match2 = regex2.Match(siteUrl);
                        DateTime result;
                        if (match.Success)
                        {
                            string[] splitDate = match.ToString().Split(new char[] { '-', '/', '.' });
                            if (DateTime.TryParse(match.ToString(), out result))
                            {
                                DateTime urlDate = DateTime.Parse(match.ToString());
                                if (urlDate != null)
                                {
                                    if (urlDate.CompareTo(twoMonths) >= 0)
                                    {
                                        if (siteUrl.IndexOf("xml") >= 0)
                                        {
                                            CloudQueueMessage message = new CloudQueueMessage(siteUrl);
                                            xmlqueue.AddMessage(message);
                                        }
                                        else
                                        {
                                            CloudQueueMessage message = new CloudQueueMessage(siteUrl);
                                            htmlqueue.AddMessage(message);
                                        }
                                    }
                                }
                            }
                        }
                        else if (match2.Success)
                        {
                            string[] splitDate = match2.ToString().Split(new char[] { '-', '/', '.' });

                            DateTime urlDate2 = new DateTime(Convert.ToInt32(splitDate[0]), Convert.ToInt32(splitDate[1]), 1);
                            if (urlDate2.CompareTo(twoMonths) >= 0)
                            {
                                if (siteUrl.IndexOf("xml") >= 0)
                                {
                                    CloudQueueMessage message = new CloudQueueMessage(siteUrl);
                                    xmlqueue.AddMessage(message);

                                }
                                else
                                {
                                    CloudQueueMessage message = new CloudQueueMessage(siteUrl);
                                    htmlqueue.AddMessage(message);

                                }
                            }
                        }
                    }
                    else if (siteUrl.Contains("bleacherreport"))
                    {
                        if (siteUrl.IndexOf("xml") >= 0)
                        {
                            CloudQueueMessage message = new CloudQueueMessage(siteUrl);
                            xmlqueue.AddMessage(message);

                        }
                        else
                        {
                            CloudQueueMessage message = new CloudQueueMessage(siteUrl);
                            htmlqueue.AddMessage(message);


                        }
                    }
                }

            }

        }

        public void addToTop10(string urlTitle)
        {
            try
            {
                top10.FetchAttributes();
                int n = (int)top10.ApproximateMessageCount;
                if (top10.PeekMessage() != null && n > 9)
                {
                    CloudQueueMessage message = top10.GetMessage();
                    top10.DeleteMessage(message);
                }


                CloudQueueMessage addMessage = new CloudQueueMessage(urlTitle);
                top10.AddMessage(addMessage);
            }
            catch
            {

            }

        }

        public bool containsRule(List<string> rules, string passedUrl)
        {
            if (rules.Any(c => passedUrl.ToLower().Contains(c.ToLower())))
            {
                return true;
            }
            return false;
        }

        public bool UriIsValid(string url)
        {
            Uri uriResult;
            return Uri.TryCreate(url, UriKind.Absolute, out uriResult) && uriResult.Scheme == Uri.UriSchemeHttp;
        }

        public bool UrlIsValid(string url)
        {
            try
            {
                HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
                request.Method = "HEAD";
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                response.Close();
                return (response.StatusCode == HttpStatusCode.OK);
            }
            catch
            {
                return false;
            }
        }



        public List<string> GetSite()
        {
            if (url.ToLower().IndexOf("cnn.com") >= 0)
            {
                return cnnRules;
            }
            else if (url.ToLower().IndexOf("bleacherreport.com") >= 0)
            {
                return bleachRules;
            }
            return null;
        }



        public List<string> addRobotSiteMap(string[] webText)
        {
            List<string> sitemaps = new List<string>();
            foreach (string s in webText)
            {
                string field = getContent(s, false);
                if (field == "sitemap")
                {
                    if (url.ToLower().IndexOf("bleacher") >= 0)
                    {
                        if (validParam(getContent(s, true)).IndexOf("nba") != -1)
                        {
                            sitemaps.Add(validParam(getContent(s, true)));
                        }
                    }
                    else
                    {
                        sitemaps.Add(validParam(getContent(s, true)));
                    }

                }
            }
            return sitemaps;
        }

        //get the disallows from the line
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




        public void ConnectToTable(string url, string title)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
            ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("crawledurls");
            table.CreateIfNotExists();
            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            title = rgx.Replace(title, "");
            string[] splitTitle = title.Split(' ');
            Debug.Print("add word:");
            foreach (string word in splitTitle)
            {
                Debug.Print(word);
                UrlTitle addToTable = new UrlTitle(url, word.ToLower(), title);
                TableOperation insertOperation = TableOperation.Insert(addToTable);
                table.Execute(insertOperation);
            }
        }
    }
}
