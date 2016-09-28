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
        static Dictionary<string, string> itemNames = new Dictionary<string, string>();

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
        
        static List<string> GetImages(string data, string id)
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

        static string ClearDesc(string desc)
        {
            return desc.
                Replace("<p>", "").
                Replace("</p>", "").
                Replace("<span>", "").
                Replace("</span>", "").
                Replace("(", " ").
                Replace(")", " ");
        }

        static string GetDiff(string s1, string s2)
        {
            var set1 = ClearDesc(s1).Split(' ').Distinct();
            var set2 = ClearDesc(s2).Split(' ').Distinct();

            var diff = set2.Except(set1).ToList();
            return string.Join(" ", diff);
        }

        static void DownloadPage(string url, string team, string collection, StreamWriter csv)
        {
            var id = Path.GetFileName(url);
            var data = client.DownloadString("http://www.zhats.com/collections/" + collection.ToLowerInvariant() + "/products/" + id);
            var name = GetName(data);
            var desc = GetDescription(data);
            var price = GetPrice(data);
            if (string.IsNullOrWhiteSpace(price))
            {
                throw new Exception("Price cannot be null.");
            }

            var images = GetImages(data, id);

            //Fix duplicates
            if (itemNames.ContainsKey(name))
            {
                var diff = GetDiff(itemNames[name], desc);
                if (!string.IsNullOrEmpty(diff))
                {
                    name += " " + diff;
                }
            }
            else
            {
                itemNames.Add(name, desc);
            }

            var shortLink = name.ToLowerInvariant().Replace(' ', '-');
            var template = "Product,,\"{0}\",P,,,,,Right,\"<p><span>{1}</span></p>\",{2},0.00,0.00,0.00,0.00,N,,16.0000,0.0000,0.0000,0.0000,Y,Y,,none,0,0,\"Shop/Caps \\/ Hats/{10}/{3}\",,{4},,Y,0,,{5},,,,,{6},,,,,{7},,,,,{8},,,,,,,,,,,New,N,N,\"Delivery Date\",N,,,0,\"Non - Taxable Products\",,N,/{9}/,,,,,,,,,,,,N,,";
            csv.WriteLine(template,
                name, desc.Trim(' ', '\n', '\r').Replace("\r","").Replace("\n",""), price, team.Replace('-',' '), 
                images.Count > 0 ? images[ 0 ] : "", 
                images.Count > 1 ? images[ 1 ] : "", 
                images.Count > 2 ? images[ 2 ] : "",
                images.Count > 3 ? images[ 3 ] : "",
                images.Count > 4 ? images[ 4 ] : "",
                shortLink, collection );
        }

        static List<string> GetTeams(string prefix)
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

        static List<string> GetItems(string url)
        {
            var items = new List<string>();
            var data = client.DownloadString(url);

            foreach (Match match in Regex.Matches(data, "<div class=\\\"product-details\\\">((.|\\n)+?)<a href=\\\"(.+)\\\">"))
            {
                items.Add(match.Groups[3].Value);
            }

            return items;
        }

        static void LoadCategory(string name, string to, string fullname)
        {
            Console.WriteLine("Start download {0}.", name);
            Directory.CreateDirectory(to);
            var header = "\"Item Type\",\"Product ID\",\"Product Name\",\"Product Type\",\"Product Code/SKU\",\"Bin Picking Number\",\"Brand Name\",\"Option Set\",\"Option Set Align\",\"Product Description\",\"Price\",\"Cost Price\",\"Retail Price\",\"Sale Price\",\"Fixed Shipping Cost\",\"Free Shipping\",\"Product Warranty\",\"Product Weight\",\"Product Width\",\"Product Height\",\"Product Depth\",\"Allow Purchases?\",\"Product Visible?\",\"Product Availability\",\"Track Inventory\",\"Current Stock Level\",\"Low Stock Level\",\"Category\",\"Product Image ID - 1\",\"Product Image File - 1\",\"Product Image Description - 1\",\"Product Image Is Thumbnail - 1\",\"Product Image Sort - 1\",\"Product Image ID - 2\",\"Product Image File - 2\",\"Product Image Description - 2\",\"Product Image Is Thumbnail - 2\",\"Product Image Sort - 2\",\"Product Image ID - 3\",\"Product Image File - 3\",\"Product Image Description - 3\",\"Product Image Is Thumbnail - 3\",\"Product Image Sort - 3\",\"Product Image ID - 4\",\"Product Image File - 4\",\"Product Image Description - 4\",\"Product Image Is Thumbnail - 4\",\"Product Image Sort - 4\",\"Product Image ID - 5\",\"Product Image File - 5\",\"Product Image Description - 5\",\"Product Image Is Thumbnail - 5\",\"Product Image Sort - 5\",\"Search Keywords\",\"Page Title\",\"Meta Keywords\",\"Meta Description\",\"MYOB Asset Acct\",\"MYOB Income Acct\",\"MYOB Expense Acct\",\"Product Condition\",\"Show Product Condition?\",\"Event Date Required?\",\"Event Date Name\",\"Event Date Is Limited?\",\"Event Date Start Date\",\"Event Date End Date\",\"Sort Order\",\"Product Tax Class\",\"Product UPC/EAN\",\"Stop Processing Rules\",\"Product URL\",\"Redirect Old URL?\",\"GPS Global Trade Item Number\",\"GPS Manufacturer Part Number\",\"GPS Gender\",\"GPS Age Group\",\"GPS Color\",\"GPS Size\",\"GPS Material\",\"GPS Pattern\",\"GPS Item Group ID\",\"GPS Category\",\"GPS Enabled\",\"Avalara Product Tax Code\",\"Product Custom Fields\"";
            var file = File.CreateText(Path.Combine(to, name + ".csv"));
            file.WriteLine(header);
            foreach (var team in GetTeams(fullname))
            {
                var teamName = Path.GetFileName(team);
                Console.WriteLine("Start download {0} team: {1}", name, teamName);
                foreach (var item in GetItems(team))
                {
                    var itemName = Path.GetFileName(item);
                    Console.WriteLine("Start download team item: {0}", itemName);
                    try
                    {
                        DownloadPage(item, teamName, name, file);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Exception: {0}", e.Message);
                    }
                }
            }
            file.Close();
            Console.WriteLine("Download ended: {0}.", name);
        }

        static void Main(string[] args)
        {
            var csvDirectory = Path.Combine("D:", "zhats.com");
            LoadCategory("NCAA", csvDirectory, "ncaateams");
            LoadCategory("NHL", csvDirectory, "nhl-teams");
            Console.ReadKey();
        }
    }
}
