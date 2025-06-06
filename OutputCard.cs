using System.Diagnostics;
using System.Text.Json.Serialization;

namespace SVEDB_Extract
{

    public class OutputCard
    {
        [JsonPropertyName("cardId")]
        public string Id { get; set; }

        [JsonPropertyName("cardSet")]
        public string CardSet { get; set; }

        [JsonPropertyName("cardNumber")]
        public string CardNumber { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        [JsonPropertyName("class")]
        public string Class { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("imgUrl")]
        public string ImgUrl { get; set; }

        [JsonPropertyName("cost")]
        public int Cost { get; set; }

        [JsonPropertyName("atk")]
        public int Attack { get; set; }

        [JsonPropertyName("def")]
        public int Defense { get; set; }

        [JsonPropertyName("limitedToCount")]
        public int LimitedToCount { get; set; }

        [JsonPropertyName("doubleSided")]
        public bool DoubleSided { get; set; }

        [JsonPropertyName("AltImgUrl")]
        public string AltImgUrl { get; set; }

        [JsonPropertyName("AltName")]
        public string AltName { get; set; }

        

        public static explicit operator OutputCard(Card c)
        {
            int atk = 0;
            int def = 0;

            CardMetaData.Metadata.TryGetValue(c.CardNumber, out string[]? meta);
            if (meta != null)
            {
                if (!int.TryParse(meta[0], out atk))
                    atk = -1;
                if (!int.TryParse(meta[1], out def))
                    def = -1;
            }

            return new()
            {
                Id = c.CardNumber,
                CardSet = c.CardNumber.Split('-')[0],
                CardNumber = c.CardNumber.Split('-')[1],
                Kind = c.CardKind.Replace("\u30FB", ""),
                Class = c.Affiliation,
                Name = c.Name,
                ImgUrl = $"https://en.shadowverse-evolve.com/wordpress/wp-content/images/cardlist/{c.Img}",
                Cost = c.GParam.G0,
                LimitedToCount = c.Max,
                Attack = atk,
                Defense = def,
                DoubleSided = c.CustomParm.BothSides,
                AltImgUrl = string.IsNullOrWhiteSpace(c.CustomParm.RevImage) ? string.Empty : $"https://en.shadowverse-evolve.com/wordpress/wp-content/images/cardlist/{c.CustomParm.RevImage}",
                AltName = string.IsNullOrWhiteSpace(c.CustomParm.RevName) ? string.Empty : c.CustomParm.RevName,
            };
        }

    }

    public static class CardMetaData
    {
        public static Dictionary<string, string[]> Metadata = new();
    }


}
