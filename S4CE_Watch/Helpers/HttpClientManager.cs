﻿using Amazon;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using S4CE_Watch.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace S4CE_Watch.Helpers
{
    public class HttpClientManager
    {

        public List<Listing1> GetReverb(string url)
        {
            List<string> searchKeys = new List<string>() { "guild s4ce", "guild songbird" };
            List<Listing1> items = new List<Listing1>();
            foreach (string searchKey in searchKeys)
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create($"https://api.reverb.com/api/listings?query={searchKey}");
                httpWebRequest.ContentType = "application/hal+json";
                httpWebRequest.Method = "GET";
                httpWebRequest.Accept = "*/*";
                httpWebRequest.Headers.Add("Accept-Version", "3.0");
                httpWebRequest.Headers.Add("Authorization", "Bearer ");

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();

                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    ReverbListings listings = JsonConvert.DeserializeObject<ReverbListings>(streamReader.ReadToEnd());
                    List<Listing1> liveListings = listings.listings.Where(l => l.state.description == "Live").ToList();
                    foreach(Listing1 listing in liveListings)
                    {
                        if (items.Where(i => i.id == listing.id).SingleOrDefault() == null)
                            items.Add(listing);
                    }
                }
            }
            return items.Count > 0 ? items : null;

        }

        public async Task<List<EbayItem>> GetEbay()
        {
            List<string> searchKeys = new List<string>() { "%22guild+songbird%22", "%22guild+sc4e%22" };
            List<EbayItem> items = new List<EbayItem>();
            foreach (string searchKey in searchKeys)
            {
                HttpResponseMessage listingResponse = await new HttpClient().GetAsync($"https://svcs.ebay.com/services/search/FindingService/v1?OPERATION-NAME=findItemsByKeywords&SERVICE-NAME=FindingService&SERVICE-VERSION=1.13.0&SECURITY-APPNAME=DavidLag-sc4ewatc-PRD-53882c164-d99fc710&RESPONSE-DATA-FORMAT=JSON&REST-PAYLOAD&keywords={searchKey}&paginationInput.entriesPerPage=50");
                if (listingResponse.IsSuccessStatusCode)
                {

                    EbayListings ebay = JsonConvert.DeserializeObject<EbayListings>(listingResponse.Content.ReadAsStringAsync().Result);
                    if(ebay.findItemsByKeywordsResponse[0].searchResult[0].item != null)
                    {
                        List<EbayListingItem> listingItems = ebay.findItemsByKeywordsResponse[0].searchResult[0].item.Where(i => i.primaryCategory[0].categoryId.Contains("33034") || i.primaryCategory[0].categoryId.Contains("22966") || i.primaryCategory[0].categoryId.Contains("619") || i.primaryCategory[0].categoryId.Contains("33021") || i.primaryCategory[0].categoryId.Contains("165255")).ToList();
                        foreach (EbayListingItem item in listingItems)
                        {
                            string url = $"https://open.api.ebay.com/shopping?callname=GetSingleItem&responseencoding=JSON&appid=DavidLag-sc4ewatc-PRD-53882c164-d99fc710&siteid=0&version=967&ItemID={item.itemId[0]}&IncludeSelector=Description,ItemSpecifics";
                            HttpResponseMessage itemResponse = await new HttpClient().GetAsync(url);
                            if (itemResponse.IsSuccessStatusCode)
                            {
                                EbayItemResult ebayItem = JsonConvert.DeserializeObject<EbayItemResult>(itemResponse.Content.ReadAsStringAsync().Result);
                                if (item != null)
                                {
                                    if (items.Where(i => i.ItemID == item.itemId[0]).SingleOrDefault() == null)
                                        items.Add(ebayItem.Item);
                                }
                            }
                        }
                    }  
                }
            }
            return items.Count > 0 ? items : null; ;
        }

        public async Task<List<CraigslistListing>> GetCraigslist()
        {
            List<CraigslistListing> listings = new List<CraigslistListing>();
            string awsEvent = JsonConvert.SerializeObject(new LambdaEvent() { amount_of_lists = "100", category = "msa", search_query = "guild songbird" });
            HttpResponseMessage response = await RunLambda(awsEvent);
            if (response.IsSuccessStatusCode)
            {
                CraigslistListings allListings = JsonConvert.DeserializeObject<CraigslistListings>(response.Content.ReadAsStringAsync().Result);
                foreach(CraigslistListing listing in allListings.listings)
                {
                    listings.Add(listing);
                }
                return listings;
            }
            return null;
        }

        public async Task<HttpResponseMessage> RunLambda(string awsEvent)
        {
            var lambdaRequest = new InvokeRequest
            {
                FunctionName = "scanCraigslist",
                Payload = awsEvent
            };

            var response = await lambdaClient.InvokeAsync(lambdaRequest);
            if (response != null)
            {
                using (var sr = new StreamReader(response.Payload))
                {
                    string result = await sr.ReadToEndAsync();
                    JObject jobject = JObject.Parse(result);
                    return new HttpResponseMessage()
                    {
                        StatusCode = jobject["statusCode"].ToString() == "200" ? HttpStatusCode.OK : HttpStatusCode.BadRequest,
                        Content = new StringContent(jobject["body"].ToString())
                    };
                }
            }
            return new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("")
            }; ;

        }
    }
}
