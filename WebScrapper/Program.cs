using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading;
using System.Globalization;

namespace WebScrapper
{
    public static class StringExtensions
    {
        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source.IndexOf(toCheck, comp) >= 0;
        }
    }

    class Program
    {
        static WebClient client = new WebClient();
        static IDictionary<string, int> itemCount = new Dictionary<string, int>();

        static string GetName(string data)
        {
            foreach (Match match in Regex.Matches(data, "itemprop=\\\"name\\\">(.+)<\\/h1>"))
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }

        static string GetDescription(string data)
        {
            foreach (Match match in Regex.Matches(data, "<div class=\\\"rte\\\">((.|\\n)+?)<\\/div>"))
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }

        static string GetPrice(string data)
        {
            var pattern = "<meta property=\\\"og\\:price\\:amount\\\" content=\\\"(\\S+)\\\">";
            foreach (Match match in Regex.Matches(data, pattern))
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }
        
        static IList<string> GetImages(string data, string id)
        {
            var images = new List<string>();
            foreach (Match m in Regex.Matches(data, "//(\\S+?)\\.(jpg|png|gif|jpeg)"))
            {
                var value = m.Value;
                if (value.Contains(id, StringComparison.OrdinalIgnoreCase) && 
                    !value.Contains("large") && 
                    !value.Contains("grande") && 
                    !value.Contains("1024"))
                {
                    images.Add("http:"+ value);
                }
            }

            return images.Distinct().ToList();
        }

        public static string ToRoman(int number)
        {
            if ((number < 0) || (number > 3999)) throw new ArgumentOutOfRangeException("insert value betwheen 1 and 3999");
            if (number < 1) return string.Empty;
            if (number >= 1000) return "M" + ToRoman(number - 1000);
            if (number >= 900) return "CM" + ToRoman(number - 900);
            if (number >= 500) return "D" + ToRoman(number - 500);
            if (number >= 400) return "CD" + ToRoman(number - 400);
            if (number >= 100) return "C" + ToRoman(number - 100);
            if (number >= 90) return "XC" + ToRoman(number - 90);
            if (number >= 50) return "L" + ToRoman(number - 50);
            if (number >= 40) return "XL" + ToRoman(number - 40);
            if (number >= 10) return "X" + ToRoman(number - 10);
            if (number >= 9) return "IX" + ToRoman(number - 9);
            if (number >= 5) return "V" + ToRoman(number - 5);
            if (number >= 4) return "IV" + ToRoman(number - 4);
            if (number >= 1) return "I" + ToRoman(number - 1);
            throw new ArgumentOutOfRangeException("something bad happened");
        }

        static string GetCacheName(string url)
        {
            var cacheName = new Uri(url).AbsolutePath;
            foreach (var symvol in Path.GetInvalidPathChars())
            {
                cacheName = cacheName.Replace(symvol + "", "");
            }
            return cacheName.Replace("/", "");
        }

        static string DownloadStringWithCache(string url)
        {
            var name = Path.GetFileName(url);
            var filePath = Path.Combine("cache", GetCacheName(url));
            if (File.Exists(filePath))
            {
                return File.OpenText(filePath).ReadToEnd();
            }
            var client = new WebClient();
            var data = client.DownloadString(url);
            Directory.CreateDirectory("cache");
            var writer = File.CreateText(filePath);
            writer.Write(data);
            writer.Close();
            return data;
        }

        static string DownloadPage(string url, string team, string collection)
        {
            var id = Path.GetFileName(url);
            var data = DownloadStringWithCache("http://www.zhats.com/collections/" + collection.ToLowerInvariant() + "/products/" + id);
            var name = GetName(data);
            var desc = GetDescription(data);
            var price = GetPrice(data);
            if (string.IsNullOrWhiteSpace(price))
            {
                throw new Exception("Price cannot be null.");
            }

            var images = GetImages(data, id);

            //Fix duplicates roman number method
            if (itemCount.ContainsKey(name))
            {
                itemCount[name] = itemCount[name] + 1;
                name += " " + ToRoman(itemCount[name]);
            }
            else
            {
                itemCount.Add(name, 1);
            }

            string output = string.Empty;
            var template = "Product,,\"{0}\",P,,,,,Right,\"<p><span>{1}</span></p>\",{2},0.00,0.00,0.00,0.00,N,,16.0000,0.0000,0.0000,0.0000,Y,Y,,none,0,0,\"Shop/Caps \\/ Hats/{9}/{3}\",,{4},,Y,0,,{5},,,,,{6},,,,,{7},,,,,{8},,,,,,,,,,,New,N,N,\"Delivery Date\",N,,,0,\"Non - Taxable Products\",,N,,,,,,,,,,,,,N,,";
            return string.Format(template,
                name, desc.Trim(' ', '\n', '\r').Replace("\r","").Replace("\n",""), price, team.Replace('-',' '), 
                images.Count > 0 ? images[ 0 ] : "", 
                images.Count > 1 ? images[ 1 ] : "", 
                images.Count > 2 ? images[ 2 ] : "",
                images.Count > 3 ? images[ 3 ] : "",
                images.Count > 4 ? images[ 4 ] : "",
                collection );
        }

        public class Downloader
        {
            private string _url;
            private string _team;
            private string _collection;
            private ManualResetEvent _doneEvent;

            public string csvString { get; private set; }

            public Downloader(string url, string team, string collection, ManualResetEvent doneEvent)
            {
                _url = url;
                _team = team;
                _collection = collection;
                _doneEvent = doneEvent;
            }

            // Wrapper method for use with thread pool.
            public void ThreadPoolCallback(object threadContext)
            {
                var itemName = Path.GetFileName(_url);
                Console.WriteLine("Start download team item: {0}", itemName);
                csvString = DownloadPage(_url, _team, _collection);
                _doneEvent.Set();
            }
        }

        static IList<string> GetTeams(string prefix)
        {
            var teams = new List<string>();
            var data = client.DownloadString("http://www.zhats.com/pages/" + prefix);

            foreach (Match match in Regex.Matches(data, "<li><a href=\\\"(http://zhats(.+))\\\">(.+)<\\/a><\\/li>"))
            {
                //Fix broken link
                var value = match.Groups[1].Value;
                if (value.Contains("Canacdiens"))
                {
                    value = value.Replace("Canacdiens", "Canadiens");
                }

                teams.Add(value);
            }

            return teams;
        }

        static IList<string> GetItems(string url)
        {
            var items = new List<string>();
            var data = client.DownloadString(url);

            foreach (Match match in Regex.Matches(data, "<div class=\\\"product-details\\\">((.|\\n)+?)<a href=\\\"(.+)\\\">"))
            {
                items.Add(match.Groups[3].Value);
            }

            return items;
        }

        static IList<string> GetItemsMultipage(string url)
        {
            var items = new List<string>();
            IList<string> nextItems;
            var page = 0;
            do
            {
                ++page;
                nextItems = GetItems(url + "?page=" + page);
                items.AddRange(nextItems);
            }
            while (nextItems.Count > 0);

            return items;
        }
        
        static StreamWriter CreateImportCSVFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var header = "\"Item Type\",\"Product ID\",\"Product Name\",\"Product Type\",\"Product Code/SKU\",\"Bin Picking Number\",\"Brand Name\",\"Option Set\",\"Option Set Align\",\"Product Description\",\"Price\",\"Cost Price\",\"Retail Price\",\"Sale Price\",\"Fixed Shipping Cost\",\"Free Shipping\",\"Product Warranty\",\"Product Weight\",\"Product Width\",\"Product Height\",\"Product Depth\",\"Allow Purchases?\",\"Product Visible?\",\"Product Availability\",\"Track Inventory\",\"Current Stock Level\",\"Low Stock Level\",\"Category\",\"Product Image ID - 1\",\"Product Image File - 1\",\"Product Image Description - 1\",\"Product Image Is Thumbnail - 1\",\"Product Image Sort - 1\",\"Product Image ID - 2\",\"Product Image File - 2\",\"Product Image Description - 2\",\"Product Image Is Thumbnail - 2\",\"Product Image Sort - 2\",\"Product Image ID - 3\",\"Product Image File - 3\",\"Product Image Description - 3\",\"Product Image Is Thumbnail - 3\",\"Product Image Sort - 3\",\"Product Image ID - 4\",\"Product Image File - 4\",\"Product Image Description - 4\",\"Product Image Is Thumbnail - 4\",\"Product Image Sort - 4\",\"Product Image ID - 5\",\"Product Image File - 5\",\"Product Image Description - 5\",\"Product Image Is Thumbnail - 5\",\"Product Image Sort - 5\",\"Search Keywords\",\"Page Title\",\"Meta Keywords\",\"Meta Description\",\"MYOB Asset Acct\",\"MYOB Income Acct\",\"MYOB Expense Acct\",\"Product Condition\",\"Show Product Condition?\",\"Event Date Required?\",\"Event Date Name\",\"Event Date Is Limited?\",\"Event Date Start Date\",\"Event Date End Date\",\"Sort Order\",\"Product Tax Class\",\"Product UPC/EAN\",\"Stop Processing Rules\",\"Product URL\",\"Redirect Old URL?\",\"GPS Global Trade Item Number\",\"GPS Manufacturer Part Number\",\"GPS Gender\",\"GPS Age Group\",\"GPS Color\",\"GPS Size\",\"GPS Material\",\"GPS Pattern\",\"GPS Item Group ID\",\"GPS Category\",\"GPS Enabled\",\"Avalara Product Tax Code\",\"Product Custom Fields\"";
            var file = File.CreateText(path);
            file.WriteLine(header);
            return file;
        }

        static string DownloadItems(IList<string> items, string teamName, string collectionName)
        {
            if (items.Count < 1)
            {
                return string.Empty;
            }

            if (items.Count > 64)
            {
                var text = string.Empty;
                var chunkLength = (int)Math.Ceiling(items.Count() / 64.0);
                var parts = Enumerable.Range(0, 64)
                                      .Select(i => items.Skip(i * chunkLength).Take(chunkLength));
                foreach (var part in parts)
                {
                    text += DownloadItems(part.ToList(), teamName, collectionName);
                }
                return text;
            }

            // One event is used for each Downloader object.
            var doneEvents = new List<ManualResetEvent>();
            var downloaders = new List<Downloader>();

            // Configure and start threads using ThreadPool.
            for (int i = 0; i < items.Count; i++)
            {
                var doneEvent = new ManualResetEvent(false);
                doneEvents.Add(doneEvent);
                var downloader = new Downloader(items[i], teamName, collectionName, doneEvent);
                downloaders.Add(downloader);
                ThreadPool.QueueUserWorkItem(downloader.ThreadPoolCallback, i);
            }

            // Wait for all threads in pool to calculate.
            WaitHandle.WaitAll(doneEvents.ToArray());

            // Prepare the results.
            var output = string.Empty;
            foreach (var downloader in downloaders)
            {
                output += downloader.csvString + Environment.NewLine;
            }
            return output;
        }

        static void LoadCategory(string name, string to, string fullname)
        {
            Console.WriteLine("Start download {0}.", name);
            var file = CreateImportCSVFile(Path.Combine(to, name + ".csv"));
            var count = 0;
            foreach (var team in GetTeams(fullname))
            {
                var teamName = Path.GetFileName(team);
                var items = GetItemsMultipage(team);
                Console.WriteLine("Start download {0} team: {1}. Size: {2}", name, teamName, items.Count);
                count += items.Count;
                file.Write( DownloadItems(items, teamName, name) );
            }
            file.Close();
            Console.WriteLine("Download ended: {0}. Downloaded {1} items.", name, count);
        }

        static void LoadCollection(string name, string to)
        {
            var url = "http://www.zhats.com/collections/" + name;
            var file = CreateImportCSVFile(Path.Combine(to, name + ".csv"));
            var items = GetItemsMultipage(url);
            Console.WriteLine("Start download collection: {0}. Size: {1}", name, items.Count);
            file.Write(DownloadItems(items, "", name));
            file.Close();
            Console.WriteLine("Download ended: {0}. Downloaded {1} items.", name, items.Count);
        }

        static void Main(string[] args)
        {
            var csvDirectory = Path.Combine("D:", "zhats.com");
            LoadCategory("NCAA", csvDirectory, "ncaateams");
            LoadCategory("NHL", csvDirectory, "nhl-teams");
            LoadCollection("blank", csvDirectory);
            Console.ReadKey();
        }
    }
}
