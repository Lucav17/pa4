using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Caching;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for Query
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class Query : System.Web.Services.WebService
    {
        private MemoryStream memStream;
        private Trie trie;
        private PerformanceCounter theMemCounter;
        private MemoryCache memoryCache;

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public void downloadWiki()
        {
            memoryCache = MemoryCache.Default;
            if (!memoryCache.Contains("trie"))
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionString"]);
                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer container = blobClient.GetContainerReference("pa2txt");

                if (container.Exists())
                {
                    foreach (IListBlobItem item in container.ListBlobs(null, false))
                    {
                        if (item.GetType() == typeof(CloudBlockBlob))
                        {
                            CloudBlockBlob blob = (CloudBlockBlob)item;
                            memStream = new MemoryStream();
                            blob.DownloadToStream(memStream);
                            memStream.Position = 0;

                        }
                    }
                    buildTrie();
                }
            }
            else
            {
                trie = (Trie)memoryCache.Get("trie", null);
            }
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public void buildTrie()
        {
            trie = new Trie();
            StreamReader sr = new StreamReader(memStream);
            theMemCounter = new PerformanceCounter("Memory", "Available MBytes");
            bool hasMem = true;
            int counter = 1;
            string line = sr.ReadLine();
            while (hasMem && line != null)
            {
                if (counter % 1000 == 0)
                {
                    hasMem = theMemCounter.NextValue() > 20;
                }

                trie.AddWord(line);
                line = sr.ReadLine();
                counter++;
                Debug.Print(line);
            }
            memoryCache.Set("trie", trie, null);
        }


        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string searchTrie(string input)
        {
            downloadWiki();
            return new JavaScriptSerializer().Serialize(trie.searchAll(input));
        }

    }
}
