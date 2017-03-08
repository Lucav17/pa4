using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerRole1
{
    class UrlTitle : TableEntity
    {
        public UrlTitle(string url, string keyword, string title)
        {
            this.PartitionKey = keyword;
            this.RowKey = Guid.NewGuid().ToString();
            this.title = title;
            this.url = url;
            this.keyword = keyword;
        }

        public UrlTitle() { }

        public string keyword { get; set; }
        public string title { get; set; }
        public string url { get; set; }
    }
}
