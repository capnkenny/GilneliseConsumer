using System.Text.Json.Serialization;

namespace Gilnelise.Consumer
{
    public class Card : IEquatable<Card>
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("card_number")]
        public string CardNumber { get; set; }

        [JsonPropertyName("num")]
        public int Num { get; set; }

        [JsonPropertyName("card_kind")]
        public string CardKind { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("rare")]
        public string Rare { get; set; }

        [JsonPropertyName("img")]
        public string Img { get; set; }

        [JsonPropertyName("affiliation")]
        public string Affiliation { get; set; }

        [JsonPropertyName("trait")]
        public string Trait { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("direction")]
        public int Direction { get; set; }

        [JsonPropertyName("sub")]
        public string Sub { get; set; }

        [JsonPropertyName("p_param")]
        public PParam PParam { get; set; }

        [JsonPropertyName("max")]
        public int Max { get; set; }

        [JsonPropertyName("g_param")]
        public GParam GParam { get; set; }

        [JsonPropertyName("custom_param")]
        public CustomParam CustomParm { get; set; }

        public bool Equals(Card? other)
        {
            return CardNumber.Equals(other?.CardNumber);
        }
    }

    public class PParam
    {
        [JsonPropertyName("p1")]
        public string P1 { get; set; }

        [JsonPropertyName("p2")]
        public string P2 { get; set; }

        [JsonPropertyName("p3")]
        public string P3 { get; set; }

        [JsonPropertyName("p4")]
        public string P4 { get; set; }

        [JsonPropertyName("p5")]
        public string P5 { get; set; }

        [JsonPropertyName("p6")]
        public string P6 { get; set; }

        [JsonPropertyName("p7")]
        public string P7 { get; set; }

        [JsonPropertyName("p8")]
        public string P8 { get; set; }

        [JsonPropertyName("p9")]
        public string P9 { get; set; }

        [JsonPropertyName("p10")]
        public string P10 { get; set; }
    }

    public class GParam
    {
        [JsonPropertyName("g0")]
        public int G0 { get; set; }

        [JsonPropertyName("g1")]
        public int G1 { get; set; }

        [JsonPropertyName("g2")]
        public int G2 { get; set; }

        [JsonPropertyName("g3")]
        public int G3 { get; set; }

        [JsonPropertyName("g4")]
        public int G4 { get; set; }

        [JsonPropertyName("g5")]
        public int G5 { get; set; }

        [JsonPropertyName("g6")]
        public int G6 { get; set; }

        [JsonPropertyName("g7")]
        public int G7 { get; set; }

        [JsonPropertyName("g8")]
        public int G8 { get; set; }

        [JsonPropertyName("g9")]
        public int G9 { get; set; }
    }

    public class CustomParam
    {
        [JsonPropertyName("is_bothsides")]
        public bool BothSides { get; set; }

        [JsonPropertyName("rev_name")]
        public string? RevName { get; set; }

        [JsonPropertyName("rev_img")]
        public string? RevImage { get; set; }
    }

    
}
