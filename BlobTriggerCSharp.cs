using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
// NOTE: Install the Newtonsoft.Json NuGet package.
using Newtonsoft.Json;

namespace com.translator
{
    public static class BlobTriggerCSharp
    {
        private static string host = "https://api.cognitive.microsofttranslator.com";
        private static string path = "/translate?api-version=3.0";
        // Translate to German and Italian.
        private static string params_ = "&to=de";
        private static string uri = host + path + params_;

        // NOTE: Replace this example key with a valid subscription key.
        private static string key = "<your key>";

        private static HttpClient client = new HttpClient();

        [FunctionName("BlobTriggerCSharp")]
        public static async Task Run([BlobTrigger("inputfiles/{name}", Connection = "translatorstoragevis_STORAGE")]Stream inputfile, 
            [Blob("outputfiles/{name}",FileAccess.Write, Connection = "translatorstoragevis_STORAGE")] Stream outputfile, 
            string name, ILogger log)
        {
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {inputfile.Length} Bytes");
            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.Load(inputfile);
            HtmlNodeCollection childNodes = htmlDoc.DocumentNode.ChildNodes;
            await parseHTML(log, childNodes);
            //inputfile.CopyTo(outputfile);
            log.LogInformation("saving outputfile");
            htmlDoc.Save(outputfile);
            log.LogInformation("saved");
        }

        private static async Task parseHTML(ILogger log, HtmlNodeCollection childNodes) {
            foreach (var node in childNodes)
            {
                if (node.NodeType == HtmlNodeType.Text) {
                    if (node.InnerText.Trim() != "") {
                        HtmlTextNode textNode = node as HtmlTextNode;
                        //textNode.Text = $"translate:{node.InnerText}";
                        await translate(log, textNode);
                    }                    
                } else if (node.NodeType == HtmlNodeType.Element) {
                    if (node.Name!="script" && node.Name!="style") {
                        await parseHTML(log, node.ChildNodes);
                    }                    
                }
            }
        }

        private static async Task translate(ILogger log, HtmlTextNode textNode) {
            string input = textNode.Text;
            //log.LogInformation($"translate:{input}");
            System.Object[] body = new System.Object[] { new { Text = input } };
            var requestBody = JsonConvert.SerializeObject(body);

            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(uri);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", key);

                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                dynamic obj = JsonConvert.DeserializeObject(responseBody);
                
                string result = JsonConvert.SerializeObject(obj, Formatting.Indented);
                //log.LogInformation(result);
                string str = obj[0].translations[0].text;
                textNode.Text = str;
                log.LogInformation($"translation done:{str}");
            }            

        }
    }
}
