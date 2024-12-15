// See https://aka.ms/new-console-template for more information
using System.Text.Json;
using SVEDB_Extract;

Client c = new Client();

Console.WriteLine("Shadowverse Evolve Card DB Builder\n");

Console.Write("Enter the card set code for the cards you want to build, or enter A for all: ");

string? result = Console.ReadLine();


var cards = await c.GetCards(result ?? string.Empty);

Console.WriteLine();
Console.WriteLine("Writing Data...");

using(var fs = File.Create("cards.txt"))
using(StreamWriter sw = new StreamWriter(fs))
{
    List<OutputCard> cardList = new();
    foreach(var card in cards)
    {
        OutputCard oc = (OutputCard)card;
        cardList.Add(oc);
//        sw.WriteLine($"{card.CardNumber} - {card.Name}\n\t{card.Affiliation} - {card.CardKind} - G: {JsonSerializer.Serialize(card.GParam)} - P: {JsonSerializer.Serialize(card.PParam)}");
    }
    sw.WriteLine(JsonSerializer.Serialize(cardList));

    sw.Flush();
}

Console.WriteLine("Done. Exiting...");