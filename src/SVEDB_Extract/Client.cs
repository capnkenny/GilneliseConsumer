using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using HtmlAgilityPack;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("SVEDB_Extract.Tests")]

namespace SVEDB_Extract
{
    public class Client
    {
        private HttpClient client;
        private List<Card> cards;
        private bool _ciMode = false;
        private readonly IAmazonS3 _s3Client;
        private const string _bucketName = "evolvecdb";
        private List<string> _existingKeysInBucket = new();

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
            "GFB01",
            "SD02",
            "SS02",
            "BP10",
            "GFD01",
            "GFD02",
            "CSD03A",
            "CSD03B",
            "CP03",
            "BP11"
        };

        public Client(bool ciMode, IAmazonS3 s3Client)
        {
            var clientHandler = new HttpClientHandler() { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
            client = new HttpClient(clientHandler);
            cards = new List<Card>();
            _ciMode = ciMode;
            _s3Client = s3Client;
        }

        public async Task<List<Card>> GetCards(string set)
        {
            if (string.IsNullOrWhiteSpace(set))
            {
                Console.WriteLine("Cannot get an unknown set of cards. Exiting..");
                return null;
            }

            //check existing card list if needed
            if (_ciMode && _s3Client is not null)
            {
                _existingKeysInBucket = await GetExistingCardsFromBucket();
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
                int loop = 0;
                while (!response.IsSuccessStatusCode)
                {
                    await Task.Delay(3000 + loop);
                    Console.WriteLine("Detected an issue retrieving the last response - waiting...");
                    request = new(HttpMethod.Post, "https://decklog-en.bushiroad.com/system/app/api/search/6");
                    PrepareNaviHeaders(request);
                    request.Content = new StringContent(json);
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json;charset=utf-8");
                    response = await client.SendAsync(request);
                    loop += 100;
                }



                List<Card> cardList = await response.Content.ReadFromJsonAsync<List<Card>>() ?? new();
                int cardListCount = cardList.Count();

                while (cardList.Count > 0)
                {
                    cards.AddRange(cardList);
                    cards = cards.Distinct().ToList();
                    //await Parallel.ForEachAsync(cardList, async (asyncCard, token) => { await GetCardMetaData(client, asyncCard); });

                    cardRequest.Page++;
                    json = JsonSerializer.Serialize(cardRequest);
                    request = new HttpRequestMessage(HttpMethod.Post, "https://decklog-en.bushiroad.com/system/app/api/search/6");
                    PrepareNaviHeaders(request);
                    request.Content = new StringContent(json);
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json;charset=utf-8");
                    await Task.Delay(250);
                    response = await client.SendAsync(request);
                    int innerLoop = 0;
                    while (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Retrying request for page {cardRequest.Page} of set {set}...");
                        await Task.Delay(2500 + innerLoop);
                        request = new HttpRequestMessage(HttpMethod.Post, "https://decklog-en.bushiroad.com/system/app/api/search/6");
                        PrepareNaviHeaders(request);
                        request.Content = new StringContent(json);
                        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json;charset=utf-8");
                        response = await client.SendAsync(request);
                    }

                    cardList = await response.Content.ReadFromJsonAsync<List<Card>>() ?? new List<Card>();
                    if (cardList != null)
                        cardListCount += cardList.Count();

                }
                cards = cards.OrderBy((card) => card.CardNumber).ToList();
                Console.WriteLine($"Retrieved all ({cardListCount}) cards for set {set}.");
            }

            Console.WriteLine($"Total cards retrieved: {cards.Count()}");

            Random r = new();

            foreach (var card in cards)
            {
                await Task.Delay(100 + r.Next(0, 101));
                await GetCardMetaData(client, card);
                if (_ciMode && _s3Client is not null)
                {
                    //get the card image if needed
                    if (!_existingKeysInBucket.Any(key => key.Contains(card.CardNumber, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        await UploadCardToCdn(card);
                    }
                    else
                    {
                        Console.WriteLine($"\t\tImage for {card.CardNumber} already exists, skipping...");
                    }
                }
            }

            Console.WriteLine(); // el oh el
            Console.WriteLine($"Successfully fetched {cards.Count} cards from all sets requested.");

            return cards;
        }

        private async Task GetCardMetaData(HttpClient client, Card card)
        {
            Console.WriteLine($"Fetching metadata for {card.CardNumber}...");

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"https://en.shadowverse-evolve.com/cards/?cardno={card.CardNumber}&view=image"),
            };

            PrepareSiteHeaders(request);

            const string statusLookup = "<div class=\"status\">";
            var response = await client.SendAsync(request);
            int loop = 0;
            while (response.StatusCode != HttpStatusCode.OK)
            {
                loop += 100;
                Console.WriteLine($"Retrying metadata call for {card.CardNumber}...");
                await Task.Delay(2500 + loop);
                request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"https://en.shadowverse-evolve.com/cards/?cardno={card.CardNumber}"),
                };
                response = await client.SendAsync(request);
            }

            var body = await response.Content.ReadAsStringAsync();

            //<span class="heading heading-Power">Attack
            if (body.Contains(statusLookup))
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(body);

                string atk = string.Empty;
                string def = string.Empty;
                string trait = string.Empty;
                string filteredDesc = string.Empty;
                string secondAtk = string.Empty;
                string secondDef = string.Empty;
                string altFilteredDesc = string.Empty;
                string altTrait = string.Empty;

                string affiliation = card.Affiliation;
                if (string.IsNullOrWhiteSpace(affiliation))
                {
                    affiliation = doc.DocumentNode?.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div/div[2]/div/div[1]/dl[2]/dd")?.InnerText ?? card.Affiliation;
                }

                //refactor this later

                atk = doc.DocumentNode?.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div/div[2]/div/div[2]/span[2]/text()")?.InnerText ?? string.Empty;
                def = doc.DocumentNode?.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div/div[2]/div/div[2]/span[3]/text()")?.InnerText ?? string.Empty;

                trait = doc.DocumentNode?.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div/div[2]/div/div[1]/dl[4]/dd")?.InnerText ?? string.Empty;

                var desc = SanitizeDescription(doc.DocumentNode?.SelectSingleNode("//*[@id=\"st-Body\"]/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div/div[2]/div/div[3]/p")?.InnerHtml ?? string.Empty);
                filteredDesc = Regex.Replace(desc, "<.*?>", string.Empty).Trim();

                if (card.CustomParm.BothSides)
                {
                    secondAtk = doc.DocumentNode?.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div[2]/div[2]/div/div[2]/span[2]/text()")?.InnerText ?? string.Empty;
                    secondDef = doc.DocumentNode?.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div[2]/div[2]/div/div[2]/span[3]/text()")?.InnerText ?? string.Empty;
                    var secondDesc = SanitizeDescription(doc.DocumentNode?.SelectSingleNode("//*[@id=\"st-Body\"]/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div[2]/div[2]/div/div[3]/p")?.InnerHtml ?? string.Empty);

                    altFilteredDesc = Regex.Replace(secondDesc, "<.*?>", string.Empty).Trim();

                    altTrait = doc.DocumentNode?.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div[2]/div[2]/div/div[1]/dl[4]/dd")?.InnerText ?? string.Empty;
                }

                CardMetaData.Metadata.TryAdd(card.CardNumber, new string[] { atk, def, filteredDesc, trait, secondAtk, secondDef, altFilteredDesc, altTrait });

                return;
            }
            else
            {
                CardMetaData.Metadata.TryAdd(card.CardNumber, new string[] { "", "" });
                return;
            }

            //image lookup
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
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_q.png\" alt=\"[q]\">", "[Quick]")
                .Replace("<img class=\"icon-square\" src=\"/wordpress/wp-content/images/texticon/icon_ride.png\" alt=\"[ride]\">", "[Ride]")   //Cardfight Vanguard-specific icon, not released yet
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
            request.Headers.Clear();
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/139.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.5");
            request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br, zstd");
            //request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Host", "en.shadowverse-evolve.com");
            request.Headers.TryAddWithoutValidation("Referer", "https://en.shadowverse-evolve.com/cards/searchresults/?expansion=CSD03A&view=image");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
            request.Headers.TryAddWithoutValidation("Pragma", "no-cache");
            request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
            request.Headers.TryAddWithoutValidation("TE", "trailers");
            request.Headers.TryAddWithoutValidation("Priority", "u=0, i");
            request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            //request.Headers.TryAddWithoutValidation("Cookie", "_ga=GA1.3.1547043965.1714064650; _ga_EPP06NTRCV=GS1.1.1719762921.14.1.1719763108.35.0.0; _ga=GA1.2.1547043965.1714064650; CookieConsent={stamp:%27Fj1+4fFNpTx6diBKlz1oDKUCQvJrog5l1a3dXGk7+DpDBcCjihWwZQ==%27%2Cnecessary:true%2Cpreferences:true%2Cstatistics:true%2Cmarketing:true%2Cmethod:%27explicit%27%2Cver:1%2Cutc:1746823276833%2Cregion:%27us-34%27}; cardlist_search_sort=old; cardlist_view=text");

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
                var cardId = htmlDoc.DocumentNode?.SelectSingleNode("/a/div[2]/p[1]")?.InnerText ?? string.Empty;
                var cardTitle = htmlDoc.DocumentNode?.SelectSingleNode("/a/div[2]/p[2]")?.InnerText ?? string.Empty;
                var cardCost = htmlDoc.DocumentNode?.SelectSingleNode("/a/div[2]/div[1]/span[4]/text()")?.InnerText ?? string.Empty;
                var type = htmlDoc.DocumentNode?.SelectSingleNode("/a/div[2]/div[1]/span[1]")?.InnerText ?? string.Empty;
                var attrib = htmlDoc.DocumentNode?.SelectSingleNode("/a/div[2]/div[1]/span[2]")?.InnerText ?? string.Empty;
                var trait = htmlDoc.DocumentNode?.SelectSingleNode("/html/body/div[1]/div[3]/div/div/div[2]/div[2]/div[1]/div/div[2]/div/div[1]/dl[4]/dd")?.InnerText ?? string.Empty;

                Card card = new()
                {
                    CardNumber = cardId,
                    Name = cardTitle,
                    Affiliation = "",
                    Trait = trait,
                    CardKind = type,
                    Max = -1,
                    GParam = new GParam
                    {
                        G0 = int.Parse(cardCost)
                    },
                    Img = $"{cardId.Split("-")[0]}/{cardId}.png",
                    CustomParm = new CustomParam
                    {
                        BothSides = false
                    },
                };

                cardsToAdd.Add(card);

                if (_ciMode && _s3Client is not null)
                {
                    //get the card image if needed
                    if (!_existingKeysInBucket.Any(key => key.Contains(card.CardNumber, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        await UploadCardToCdn(card);
                    }
                    else
                    {
                        Console.WriteLine($"\t\tImage for {card.CardNumber} already exists, skipping...");
                    }
                }
            }

            return cardsToAdd;
        }

        internal async Task<List<string>> GetExistingCardsFromBucket()
        {
            try
            {
                List<S3Object> keys = [];

                Console.WriteLine("Fetching images from bucket...");
                var request = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    MaxKeys = 1000
                };

                var response = await _s3Client.ListObjectsV2Async(request);

                if (response is not null && response.S3Objects is null)
                {
                    return [];
                }
                else if (response is null)
                {
                    return [];
                }

                keys.AddRange(response.S3Objects);
                while (response!.IsTruncated is not null && response.IsTruncated.Value)
                {
                    request.ContinuationToken = response.NextContinuationToken;
                    response = await _s3Client.ListObjectsV2Async(request);
                    if (response is not null && response.S3Objects is not null)
                    {
                        keys.AddRange(response.S3Objects);
                    }
                }

                Console.WriteLine($"Found {keys.Count} images");
                return [.. keys.Select(obj => obj.Key.Replace(".png", ""))];
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error when fetching existing cards in S3: {e.Message}");

                return [];
            }
        }

        private async Task UploadCardToCdn(Card card)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"https://en.shadowverse-evolve.com/wordpress/wp-content/images/cardlist/{card.Img}"),
            };

            PrepareSiteHeaders(request);
            Console.WriteLine($"\tUploading card image to S3 CDN for {card.CardNumber}...");

            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                //Should be the raw png data decompressed by httpclient already
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    try
                    {
                        //just in case
                        stream.Position = 0;

                        var s3request = new PutObjectRequest
                        {
                            InputStream = stream,
                            AutoResetStreamPosition = true,
                            AutoCloseStream = false, //will handle by using stmt
                            BucketName = _bucketName,
                            DisablePayloadSigning = true, //cloudflare req
                            DisableDefaultChecksumValidation = true, //cloudflare req
                            Key = $"{card.CardNumber}.png"
                        };

                        var s3Reponse = await _s3Client.PutObjectAsync(s3request);
                        Console.WriteLine($"\t\tUpload complete, receipt: {s3Reponse.ETag}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"\t\tCould not write image to CDN! Reason: {e.Message}");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(card.CustomParm.RevImage))
            {
                //BP08/BP08-SL03_URAEN.png",
                string altImg = string.IsNullOrWhiteSpace(card.CustomParm.RevImage) ? string.Empty : card.CustomParm.RevImage.Split('/').First(str => str.Contains(".png"));
                string altCardNum = altImg.Replace(".png", string.Empty);

                Console.WriteLine($"\tUploading reverse card image to S3 CDN for {altCardNum}...");
                request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"https://en.shadowverse-evolve.com/wordpress/wp-content/images/cardlist/{card.CustomParm.RevImage}"),
                };

                PrepareSiteHeaders(request);

                response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    //Should be the raw png data decompressed by httpclient already
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        try
                        {
                            //just in case
                            stream.Position = 0;

                            var s3request = new PutObjectRequest
                            {
                                InputStream = stream,
                                AutoResetStreamPosition = true,
                                AutoCloseStream = false, //will handle by using stmt
                                BucketName = _bucketName,
                                DisablePayloadSigning = true, //cloudflare req
                                DisableDefaultChecksumValidation = true, //cloudflare req
                                Key = altImg
                            };

                            var s3Reponse = await _s3Client.PutObjectAsync(s3request);
                            Console.WriteLine($"\t\tUpload complete, receipt: {s3Reponse.ETag}");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"\t\tCould not write reverse image to CDN! Reason: {e.Message}");
                        }
                    }
                }
            }
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











