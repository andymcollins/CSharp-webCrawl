using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Newtonsoft.Json;

namespace Lead
{
     public static class Program
    {
       private static string ConnectionString = "DefaultEndpointsProtocol=https;AccountName=developmenttester;AccountKey=<KEY>;EndpointSuffix=core.windows.net";
        private static QueueClient queue = new QueueClient(ConnectionString, "andycollins");

        public struct SearchResult
        {
            public SearchResult(string Url, bool Found, DateTime ScanStarted, DateTime ScanComplete)
            {
                this.Website = Url;
                this.Google = Found;
                this.ScanStarted = ScanStarted;
                this.ScanComplete = ScanComplete;
            }             

            public string Website;
            public bool Google;
            public DateTime ScanStarted;
            public DateTime ScanComplete;

        }
        static async Task Main(string[] args)
        {     
           queue = new QueueClient(ConnectionString, "andycollins");

            if (null != await queue.CreateIfNotExistsAsync())
            {
                Console.WriteLine("The queue was created. {0}", queue.Name);
            }

            SearchResult[] searchResults = await StartSearch("www.google-analytics.com");
       
            QueueProperties properties = await queue.GetPropertiesAsync();
            Console.WriteLine("Total Messages in Queue {0}", properties.ApproximateMessagesCount);

            Console.WriteLine($"Deleting queue: {queue.Name}");
            await queue.DeleteAsync();
        }
        static async Task<string> RetrieveMessages(QueueClient theQueue)
        {
            QueueProperties properties = await theQueue.GetPropertiesAsync();
           
            if (properties.ApproximateMessagesCount > 0)
            {
                QueueMessage[] retrievedMessage = await theQueue.ReceiveMessagesAsync(1);
            
                return retrievedMessage[0].MessageText;
            } else{
                return "Queue Empty";
            }
        }

        static async Task InsertMessageAsync(QueueClient theQueue, string newMessage)
        {
            SendReceipt receipt =  await theQueue.SendMessageAsync(newMessage);
        }

        static List<string> GetUrls()
        {
            string path = @"websites.txt";

            if (!File.Exists(path))
            {
                Console.WriteLine("File ${path} does not exist");
            }

            string[] urlsFromFile = File.ReadAllLines(path);

            List<string> urlList = new List<string>(urlsFromFile);
            return urlList;
        }
    
        public async static Task<SearchResult> SearchForStringAsync(string url, string str)
        {
            bool foundString =false;
            DateTime ScanStart = DateTime.UtcNow;
            HttpClient client = new HttpClient();
            try{
                client.Timeout = TimeSpan.FromSeconds(3);
                var urlContents = await client.GetStringAsync("http://" + url);
               
                foundString = (urlContents.Contains(str) ? true : false);
                if (foundString){
                    Console.WriteLine("{0} Found in website {1}", str, url);
                } else {
                    Console.WriteLine("{0} Not Found in website {1}", str, url);
                }
            } catch (Exception ex)
            {
                foundString = false;
            } 
            string json = JsonConvert.SerializeObject(new SearchResult(url, foundString, ScanStart, DateTime.UtcNow));
            Console.WriteLine("Inserting: {0}", json);
            await InsertMessageAsync(queue, json);
          
            var queuemessage = await RetrieveMessages(queue); 
             Console.WriteLine("Retreving : {0}", queuemessage);
            return new SearchResult(url, foundString, ScanStart, DateTime.UtcNow);
        }

        public static async Task<SearchResult[]> StartSearch(string str)
        {
            List<Task<SearchResult>> taskList = new List<Task<SearchResult>>();
            var urlList = GetUrls();
       
            foreach (var url in urlList)
            {
                System.Console.WriteLine("Searching URL {0}", url);
                taskList.Add(SearchForStringAsync(url, str));
            }
            return await Task.WhenAll<SearchResult>(taskList);
        }
    }
}

