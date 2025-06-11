using HtmlAgilityPack;
using System.Net;
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

        private const int PagesOfTokens = 12;

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
            "PR",
            "GFB01"
        };

        public Client()
        {
            var clientHandler = new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
            client = new HttpClient(clientHandler);
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
                var cards = await GetCardsAsync(SupportedList);
                cards.AddRange(await GetTokenCardsAsync());
                return cards;
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
                PrepareNaviHeaders(request);
                request.Content = new StringContent(json);
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json;charset=utf-8");

                HttpResponseMessage response = await client.SendAsync(request);
                try
                {
                    response.EnsureSuccessStatusCode();
                }
                catch
                {
                    //502s will sometimes crop up due to how much we request it seems
                    await Task.Delay(10000);
                    Console.WriteLine("Detected an issue retrieving the last response - waiting...");
                    response = await client.SendAsync(request);
                }
                List<Card> cardList = await response.Content.ReadFromJsonAsync<List<Card>>() ?? new();

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
                        PrepareNaviHeaders(request);
                        request.Content = new StringContent(json);
                        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json;charset=utf-8");
                        await Task.Delay(250);
                        response = await client.SendAsync(request);
                        response.EnsureSuccessStatusCode();
                        cardList = await response.Content.ReadFromJsonAsync<List<Card>>() ?? new List<Card>();

                    }
                    cards = cards.OrderBy((card) => card.CardNumber).ToList();
                    Console.WriteLine($"\nFetched all ({cards.Count}) cards - retrieving metadata...");

                    Parallel.ForEach(cards, new ParallelOptions
                    {
                        MaxDegreeOfParallelism = 10
                    }, async card =>
                    {
                        Random r = new();
                        await Task.Delay(100 + r.Next(0, 101));
                        await GetCardMetaData(client, card);
                    });

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
                    var doc = new HtmlDocument();
                    doc.LoadHtml(body);

                    string atk = string.Empty;
                    string def = string.Empty;
                    string filteredDesc = string.Empty;
                    string secondAtk = string.Empty;
                    string secondDef = string.Empty;
                    string altFilteredDesc = string.Empty;

                    string affiliation = card.Affiliation;
                    if (string.IsNullOrWhiteSpace(affiliation))
                    {
                        affiliation = doc.DocumentNode?.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div/div[2]/div/div[1]/dl[2]/dd")?.InnerText ?? card.Affiliation;
                    }

                    //refactor this later

                    atk = doc.DocumentNode?.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div/div[2]/div/div[2]/span[2]/text()")?.InnerText ?? "";
                    def = doc.DocumentNode?.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div/div[2]/div/div[2]/span[3]/text()")?.InnerText ?? "";


                    var desc = SanitizeDescription(doc.DocumentNode?.SelectSingleNode("//*[@id=\"st-Body\"]/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div/div[2]/div/div[3]/p")?.InnerHtml ?? "");
                    filteredDesc = Regex.Replace(desc, "<.*?>", string.Empty).Trim();
                    
                    if (card.CustomParm.BothSides)
                    {
                        secondAtk = doc.DocumentNode?.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div[2]/div[2]/div/div[2]/span[2]/text()")?.InnerText ?? "";
                        secondDef = doc.DocumentNode?.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div[2]/div[2]/div/div[2]/span[3]/text()")?.InnerText ?? "";
                        var secondDesc = SanitizeDescription(doc.DocumentNode?.SelectSingleNode("//*[@id=\"st-Body\"]/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div[2]/div[2]/div/div[3]/p")?.InnerHtml ?? "");

                        altFilteredDesc = Regex.Replace(secondDesc, "<.*?>", string.Empty).Trim();
                    }

                    CardMetaData.Metadata.TryAdd(card.CardNumber, new string[]{ atk, def, filteredDesc, secondAtk, secondDef, altFilteredDesc });

                    return;
                }
                else
                {
                    CardMetaData.Metadata.TryAdd(card.CardNumber, new string[]{ "", "" });
                    return;
                }
            }

        }

        private string SanitizeDescription(string cardDescriptionHtml)
        {
            string classFiltered = cardDescriptionHtml
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_elf.png\" alt=\"[forestcraft]\">", "Forestcraft")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_royal.png\" alt=\"[swordcraft]\">", "Swordcraft")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_nightmare.png\" alt=\"[abysscraft]\">", "Abysscraft")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_dragon.png\" alt=\"[dragoncraft]\">", "Dragoncraft")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_bishop.png\" alt=\"[havencraft]\">", "Havencraft")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_neutral.png\" alt=\"[neutral]\">", "Neutral");

            string costFiltered = classFiltered
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_cost00.png\" alt=\"[cost00]\">", "(0)")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_cost01.png\" alt=\"[cost01]\">", "(1)")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_cost02.png\" alt=\"[cost02]\">", "(2)")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_cost03.png\" alt=\"[cost03]\">", "(3)")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_cost04.png\" alt=\"[cost04]\">", "(4)")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_cost05.png\" alt=\"[cost05]\">", "(5)")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_cost06.png\" alt=\"[cost06]\">", "(6)")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_cost07.png\" alt=\"[cost07]\">", "(7)")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_cost08.png\" alt=\"[cost08]\">", "(8)")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_cost09.png\" alt=\"[cost09]\">", "(9)")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_cost10.png\" alt=\"[cost10]\">", "(10)");

            return costFiltered
                .Replace("<img class=\"icon-rectangle\" src=\"/wordpress/wp-content/images/texticon/icon_quick.png\" alt=\"[quick]\">", "[Quick]\n")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_fanfare.png\" alt=\"[fanfare]\">", "[Fanfare]")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_evolve.png\" alt=\"[evolve]\">", "[Evolve]")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_hp.png\" alt=\"[defense]\">", "Defense ")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_power.png\" alt=\"[attack]\">", "Attack ")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_act.png\" alt=\"[act]\">", "[Action]")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_stand.png\" alt=\"[engage]\">", "[Engage]")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_lastword.png\" alt=\"[lastwords]\">", "[Last Words]")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_carrot.png\" alt=\"[feed]\">", "[Serve]")
                .Replace ("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_q.png\" alt=\"[q]\">", "[Quick]")
                .Replace ("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_ride.png\" alt=\"[ride]\">", "[Ride]")   //Cardfight Vanguard-specific icon, not released yet
                .Replace("  ", " ");
        }

        private void PrepareNaviHeaders(HttpRequestMessage request)
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

        private void PrepareSiteHeaders(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:139.0) Gecko/20100101 Firefox/139.0");
            request.Headers.TryAddWithoutValidation("Accept", "*/*");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.5");
            request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br, zstd");
            request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Referer", "https://en.shadowverse-evolve.com/cards/searchresults/?card_name=&format[0]=all&class[0]=all&title=&expansion_name=&cost[0]=all&card_kind[0]=Token&rare[0]=all&power_from=&power_to=&hp_from=&hp_to=&type=&ability=&keyword=&view=text&sort=old");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "no-cors");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
            request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
            request.Headers.TryAddWithoutValidation("TE", "trailers");
            request.Headers.TryAddWithoutValidation("Priority", "u=4");
            request.Headers.TryAddWithoutValidation("Cookie", "_ga=GA1.3.1547043965.1714064650; _ga_EPP06NTRCV=GS1.1.1719762921.14.1.1719763108.35.0.0; _ga=GA1.2.1547043965.1714064650; CookieConsent={stamp:%27Fj1+4fFNpTx6diBKlz1oDKUCQvJrog5l1a3dXGk7+DpDBcCjihWwZQ==%27%2Cnecessary:true%2Cpreferences:true%2Cstatistics:true%2Cmarketing:true%2Cmethod:%27explicit%27%2Cver:1%2Cutc:1746823276833%2Cregion:%27us-34%27}; cardlist_search_sort=old; cardlist_view=text");

        }

        private async Task<List<Card>> GetTokenCardsAsync()
        {
            Console.WriteLine("Getting token cards...");
            int page = 1;
            List<Card> cardsToAdd = [];

            HttpRequestMessage request = new(HttpMethod.Get, $"https://en.shadowverse-evolve.com/cards/searchresults_ex?card_name=&format%5B0%5D=all&class%5B0%5D=all&title=&expansion_name=&cost%5B0%5D=all&card_kind%5B0%5D=Token&rare%5B0%5D=all&power_from=&power_to=&hp_from=&hp_to=&type=&ability=&keyword=&view=text&page={page}&t={DateTimeOffset.Now.ToUnixTimeMilliseconds()}");
            PrepareSiteHeaders(request);
            

            HttpResponseMessage response = await client.SendAsync(request);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                //502s will sometimes crop up due to how much we request it seems
                await Task.Delay(10000);
                Console.WriteLine("Detected an issue retrieving the last response - waiting...");
                request = new(HttpMethod.Get, $"https://en.shadowverse-evolve.com/cards/searchresults_ex?card_name=&format%5B0%5D=all&class%5B0%5D=all&title=&expansion_name=&cost%5B0%5D=all&card_kind%5B0%5D=Token&rare%5B0%5D=all&power_from=&power_to=&hp_from=&hp_to=&type=&ability=&keyword=&view=text&page={page}&t={DateTimeOffset.Now.ToUnixTimeMilliseconds()}");
                PrepareSiteHeaders(request);
                response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }

            List<string> tokensToParse = [];
            
            string html = await response.Content.ReadAsStringAsync();
            string[] brokenUpHtml = html.Split("<li class=\"ex-item\">");
            tokensToParse.AddRange(brokenUpHtml);
            page++;

            while (page <= PagesOfTokens)
            {
                request = new(HttpMethod.Get, $"https://en.shadowverse-evolve.com/cards/searchresults_ex?card_name=&format%5B0%5D=all&class%5B0%5D=all&title=&expansion_name=&cost%5B0%5D=all&card_kind%5B0%5D=Token&rare%5B0%5D=all&power_from=&power_to=&hp_from=&hp_to=&type=&ability=&keyword=&view=text&page={page}&t={DateTimeOffset.Now.ToUnixTimeMilliseconds()}");
                PrepareSiteHeaders(request);

                response = await client.SendAsync(request);
                try
                {
                    response.EnsureSuccessStatusCode();
                }
                catch
                {
                    //502s will sometimes crop up due to how much we request it seems
                    await Task.Delay(10000);
                    Console.WriteLine("Detected an issue retrieving the last response - waiting...");
                    request = new(HttpMethod.Get, $"https://en.shadowverse-evolve.com/cards/searchresults_ex?card_name=&format%5B0%5D=all&class%5B0%5D=all&title=&expansion_name=&cost%5B0%5D=all&card_kind%5B0%5D=Token&rare%5B0%5D=all&power_from=&power_to=&hp_from=&hp_to=&type=&ability=&keyword=&view=text&page={page}&t={DateTimeOffset.Now.ToUnixTimeMilliseconds()}");
                    PrepareSiteHeaders(request);
                    response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                }

                string htmlResp = await response.Content.ReadAsStringAsync();
                string[] toParse = htmlResp.Split("<li class=\"ex-item\">");
                tokensToParse.AddRange(toParse);
                page++;
                await Task.Delay(250);
            }

            foreach (string tokenCardHtml in tokensToParse)
            {
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(tokenCardHtml);
                if (!tokenCardHtml.StartsWith("<a"))
                    continue;
                var cardId = htmlDoc.DocumentNode?.SelectSingleNode("/a/div[2]/p[1]")?.InnerText ?? "";
                var cardTitle = htmlDoc.DocumentNode?.SelectSingleNode("/a/div[2]/p[2]")?.InnerText ?? "";
                var cardCost = htmlDoc.DocumentNode?.SelectSingleNode("/a/div[2]/div[1]/span[4]/text()")?.InnerText ?? "";
                var type = htmlDoc.DocumentNode?.SelectSingleNode("/a/div[2]/div[1]/span[1]")?.InnerText ?? "";
                var attrib = htmlDoc.DocumentNode?.SelectSingleNode("/a/div[2]/div[1]/span[2]")?.InnerText ?? "";

                Card card = new()
                {
                    CardNumber = cardId,
                    Name = cardTitle,
                    Affiliation = "",
                    CardKind = type,
                    Max = -1,
                    GParam = new GParam {
                    G0 = int.Parse(cardCost)
                    },
                    Img = $"{cardId.Split("-")[0]}/{cardId}.png",
                    CustomParm = new CustomParam 
                    {
                        BothSides = false
                    },
                };

                cardsToAdd.Add(card);
            }

            return cardsToAdd;
        }
    }

    public static class ClientExtensions
    {
        public static byte[] ReadAllBytes(this Stream inStream)
        {
            if (inStream is MemoryStream inMemoryStream)
                return inMemoryStream.ToArray();

            using (var outStream = new MemoryStream())
            {
                inStream.CopyTo(outStream);
                return outStream.ToArray();
            }
        }
    }
}









