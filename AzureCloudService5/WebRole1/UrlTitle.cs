using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
{
    class UrlTitle : TableEntity
    {
        public UrlTitle(string url, string keyword, string title)
        {
            this.PartitionKey = keyword;
            this.RowKey = url;
            this.title = title;
            this.url = url;
        }

        public UrlTitle()
        {

        }

        public string keyword { get; set; }
        public string title { get; set; }
        public string url { get; set; }
    }
}