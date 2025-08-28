using System.Collections.Concurrent;
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

        [JsonPropertyName("trait")]
        public string Trait { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("imgUrl")]
        public string ImgUrl { get; set; }

        [JsonPropertyName("cost")]
        public int Cost { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("atk")]
        public int Attack { get; set; }

        [JsonPropertyName("def")]
        public int Defense { get; set; }

        [JsonPropertyName("limitedToCount")]
        public int LimitedToCount { get; set; }

        [JsonPropertyName("doubleSided")]
        public bool DoubleSided { get; set; }

        [JsonPropertyName("altImgUrl")]
        public string AltImgUrl { get; set; }

        [JsonPropertyName("altName")]
        public string AltName { get; set; }

        [JsonPropertyName("altAtk")]
        public int AltAttack { get; set; }

        [JsonPropertyName("altDef")]
        public int AltDefense { get; set; }

        [JsonPropertyName("altDescription")]
        public string AltDescription { get; set; }

        [JsonPropertyName("altTrait")]
        public string AltTrait { get; set; }



        public static explicit operator OutputCard(Card c)
        {
            int atk = -1;
            int def = -1;
            string trait = c.Trait;
            string desc = string.Empty;
            string affiliation = c.Affiliation;

            int altAtk = -1;
            int altDef = -1;
            string altDesc = string.Empty;
            string altTrait = string.Empty;

            CardMetaData.Metadata.TryGetValue(c.CardNumber, out string[]? meta);
            if (meta != null)
            {
                _ = int.TryParse(meta[0], out atk);
                _ = int.TryParse(meta[1], out def);

                if (meta.Length >= 3)
                {
                    desc = meta[2];
                    trait = meta[3];

                    _ = int.TryParse(meta[4], out altAtk);
                    _ = int.TryParse(meta[5], out altDef);

                    altDesc = meta[6];
                    altTrait = meta[7];
                }
            }

            //BP08/BP08-SL03_URAEN.png",
            string altImg = string.IsNullOrWhiteSpace(c.CustomParm.RevImage) ? string.Empty : c.CustomParm.RevImage.Split('/').First(str => str.Contains(".png")).Replace(".png", "");

            return new()
            {
                Id = c.CardNumber,
                CardSet = c.CardNumber.Split('-')[0],
                CardNumber = c.CardNumber.Split('-')[1],
                Kind = c.CardKind.Replace("\u30FB", "").Replace(" \\ ", ""),
                Class = c.Affiliation,
                Trait = trait,
                Name = c.Name,
                ImgUrl = $"https://evolvecdb.org/img/{c.CardNumber}",
                Cost = c.GParam.G0,
                LimitedToCount = c.Max,
                Attack = atk,
                Defense = def,
                DoubleSided = c.CustomParm.BothSides,
                AltImgUrl = string.IsNullOrWhiteSpace(c.CustomParm.RevImage) ? string.Empty : $"https://evolvecdb.org/img/{altImg}",
                AltName = string.IsNullOrWhiteSpace(c.CustomParm.RevName) ? string.Empty : c.CustomParm.RevName,
                Description = desc,
                AltDescription = altDesc,
                AltAttack = altAtk,
                AltDefense = altDef,
                AltTrait = altTrait,
            };
        }

    }

    public static class CardMetaData
    {
        public static ConcurrentDictionary<string, string[]> Metadata = new();
    }


}
