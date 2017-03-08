using HtmlAgilityPack;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for Search
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class Search : System.Web.Services.WebService
    {

        private static Dictionary<string, List<string>> cache = new Dictionary<string, List<string>>();

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string searchPlayerData(string input)
        {
            using (WebClient client = new WebClient())
            {
                return client.DownloadString("http://ec2-54-191-186-217.us-west-2.compute.amazonaws.com/api.php?inputText=" + input.Trim());
            }
        }


        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string searchTable(string input)
        {
            if(cache.ContainsKey(input))
            {
                return new JavaScriptSerializer().Serialize(cache[input]);
            }
            Dictionary<string, int> urls = new Dictionary<string, int>();
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
            ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("crawledurls");
            table.CreateIfNotExists();

            string[] searched = input.Split(' ');
            foreach(string word in searched)
            {
                
                TableQuery<UrlTitle> rangeQuery = new TableQuery<UrlTitle>().Where(
                        TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, word.ToLower()));

                foreach (UrlTitle entity in table.ExecuteQuery(rangeQuery))
                {
                    Debug.Print(entity.url);
                    if(entity.url != null)
                    {
                        if (!urls.ContainsKey(entity.url))
                        {
                            urls.Add(entity.url, 1);
                        }
                        else
                        {
                            urls[entity.url] = urls[entity.url]++;
                        }
                    }
                    
                }
            }

            var top10 = urls.OrderByDescending(pair => pair.Value).Take(10)
               .ToDictionary(pair => pair.Key, pair => pair.Value);

            Dictionary<string, string> d = new Dictionary<string, string>();
            foreach(var s in top10.Keys)
            {
                Debug.Print(s.ToString());
                try
                {
                    var doc = new HtmlWeb().Load(s.ToString());
                    var title = doc.DocumentNode.Descendants("title").FirstOrDefault().InnerText;
                    d.Add(s.ToString(), title.ToString());
                } catch
                {

                }
               
            }
            cache.Add(input, d.Keys.ToList<string>());
            
            return new JavaScriptSerializer().Serialize(d);

        }
    }
}
