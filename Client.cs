using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SVEDB_Extract
{
    public class Client
    {
        private HttpClient client;
        private List<Card> cards;

        public string[] MetadataTypesToIgnore = {
            "Spell",
            "Amulet"
        };

        public static string[] SupportedList =
        {
            "BP01",
            "BP02",
            "BP03",
            "BP04",
            "BP05",
            "BP06",
            "BP07",
            "BP08",
            "BP09",
            "SD01",
            "SD02",
            "SD03",
            "SD04",
            "SD05",
            "SD06",
            "CP01",
            "CP02",
            "CSD01",
            "CSD02A",
            "CSD02B",
            "CSD02C",
        };

        public Client()
        {
            client = new HttpClient();
            cards = new List<Card>();
        }

        public async Task<List<Card>> GetCards(string set)
        {
            if (string.IsNullOrWhiteSpace(set))
            {
                Console.WriteLine("Cannot get an unknown set of cards. Exiting..");
                return null;
            }

            if (set == "A")
            {
                return await GetCardsAsync(SupportedList);
            }

            string[] assumedSets = set.Split(';');
            var setsToPull = assumedSets.Where(a => SupportedList.Contains(a.ToUpper()));
            if (setsToPull == null || setsToPull.Count() == 0)
                throw new Exception("No sets to pull!");
            
            return await GetCardsAsync(setsToPull.ToArray());
        }

        private async Task<List<Card>> GetCardsAsync(string[] sets)
        {
            foreach (string set in sets)
            {
                Console.WriteLine($"Getting set {set}...");
                Root cardRequest = new()
                {
                    Param = new()
                    {
                        Expansion = set,
                        DeckParam1 = "N",
                    },
                    Page = 1
                };

                string json = JsonSerializer.Serialize(cardRequest);
                
                HttpRequestMessage request = new(HttpMethod.Post, "https://decklog-en.bushiroad.com/system/app/api/search/6");
                PrepareHeaders(request);
                request.Content = new StringContent(json);
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json;charset=utf-8");

                HttpResponseMessage response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                List<Card> cardList = await response.Content.ReadFromJsonAsync<List<Card>>() ?? new();
                // if(cardList.Count > 0)
                //     Console.WriteLine($"\t- {string.Join(", ", cardList.Select(c => c.CardNumber))}");

                try
                {
                    while (cardList.Count > 0)
                    {
                        cards.AddRange(cardList);
                        cards = cards.Distinct().ToList();
                        await Parallel.ForEachAsync(cardList, async (asyncCard, token) => { await GetCardMetaData(client, asyncCard); });

                        cardRequest.Page++;
                        json = JsonSerializer.Serialize(cardRequest);
                        request = new HttpRequestMessage(HttpMethod.Post, "https://decklog-en.bushiroad.com/system/app/api/search/6");
                        PrepareHeaders(request);
                        request.Content = new StringContent(json);
                        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json;charset=utf-8");
                        await Task.Delay(250);
                        response = await client.SendAsync(request);
                        response.EnsureSuccessStatusCode();
                        cardList = await response.Content.ReadFromJsonAsync<List<Card>>() ?? new List<Card>();
                        
                    }
                    cards = cards.OrderBy((card) => card.CardNumber).ToList();
                    Console.WriteLine("\nFetched all cards - retrieving metadata...");

                    foreach (var c in cardList)
                    {
                        await GetCardMetaData(client, c);
                        await Task.Delay(250);
                    }
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] - {ex.Message} - Set {set}, Page {cardRequest.Page}");
                }
            }

            return cards;
        }

        private async Task GetCardMetaData(HttpClient client, Card card)
        {
            if (MetadataTypesToIgnore.Any((item) => card.CardKind.Contains(item)))
            {
                return;
            }

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"https://en.shadowverse-evolve.com/cards/?cardno={card.CardNumber}"),
            };

            const string statusLookup = "<div class=\"status\">";
            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();
                
                //<span class="heading heading-Power">Attack
                if(body.Contains(statusLookup))
                {
                    string atk = "";
                    string def = "";

                    var sa = Regex.Match(body, @"Attack<\/span>\d+<\/span>");
                    if(sa.Success)
                    {
                        atk = sa.Groups[0].Value.Replace(@"Attack</span>", "").Replace("</span>", "");
                    }

                    sa = Regex.Match(body, @"Defense<\/span>\d+<\/span>");
                    if(sa.Success)
                    {
                        def = sa.Groups[0].Value.Replace(@"Defense</span>", "").Replace("</span>", "");
                    }
                    CardMetaData.Metadata.Add(card.CardNumber, new string[]{ atk, def });
                    return;
                }
                else
                {
                    CardMetaData.Metadata.Add(card.CardNumber, new string[]{ "", "" });
                    return;
                }
            }

        }

        private void PrepareHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:139.0) Gecko/20100101 Firefox/139.0");
            request.Headers.Add("Accept", "application/json, text/plain, */*");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.5");
            request.Headers.Add("Origin", "https://decklog-en.bushiroad.com");
            request.Headers.Add("Connection", "keep-alive");
            request.Headers.Add("Referer", "https://decklog-en.bushiroad.com/create?c=6");
            request.Headers.Add("Cookie", "CookieConsent={stamp:%273DYKV73AFO5pbjzWoPswMtCoN6lk1uQ2so6frmuwtakIxpvXO/uRgg==%27%2Cnecessary:true%2Cpreferences:true%2Cstatistics:true%2Cmarketing:true%2Cmethod:%27explicit%27%2Cver:1%2Cutc:1714065861850%2Cregion:%27us-34%27};");
            request.Headers.Add("Sec-Fetch-Dest", "empty");
            request.Headers.Add("Sec-Fetch-Mode", "cors");
            request.Headers.Add("Sec-Fetch-Site", "same-origin");
            request.Headers.Add("Pragma", "no-cache");
            request.Headers.Add("Cache-Control", "no-cache");
            request.Headers.Add("TE", "trailers");
        }
    }
}







