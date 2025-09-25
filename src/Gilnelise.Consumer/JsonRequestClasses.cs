using System.Text.Json.Serialization;

namespace Gilnelise.Consumer
{
    public class CardRequest
    {
        [JsonPropertyName("deck_type")]
        public string DeckType { get; set; }

        [JsonPropertyName("class_name")]
        public string ClassName { get; set; }

        [JsonPropertyName("keyword")]
        public string Keyword { get; set; }

        [JsonPropertyName("keyword_type")]
        public List<string> KeywordType { get; set; }

        [JsonPropertyName("expansion")]
        public string Expansion { get; set; }

        [JsonPropertyName("cost")]
        public List<object> Cost { get; set; }

        [JsonPropertyName("card_kind")]
        public List<object> CardKind { get; set; }

        [JsonPropertyName("format")]
        public string Format { get; set; }

        [JsonPropertyName("rare")]
        public List<object> Rare { get; set; }

        [JsonPropertyName("power_from")]
        public string PowerFrom { get; set; }

        [JsonPropertyName("power_to")]
        public string PowerTo { get; set; }

        [JsonPropertyName("hp_from")]
        public string HpFrom { get; set; }

        [JsonPropertyName("hp_to")]
        public string HpTo { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("ability")]
        public string Ability { get; set; }

        [JsonPropertyName("limit_nt")]
        public string LimitNt { get; set; }

        [JsonPropertyName("parallel")]
        public string Parallel { get; set; }

        [JsonPropertyName("deck_param1")]
        public string DeckParam1 { get; set; }

        [JsonPropertyName("deck_param2")]
        public string DeckParam2 { get; set; }
    }

    public class Root
    {
        [JsonPropertyName("param")]
        public CardRequest Param { get; set; }

        [JsonPropertyName("page")]
        public int Page { get; set; }
    }


}
