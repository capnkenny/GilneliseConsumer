// See https://aka.ms/new-console-template for more information
using System.Text.Json;
using SVEDB_Extract;

Client c = new Client();

string? result = null;
Console.WriteLine("Shadowverse Evolve Card DB Builder\n");

if (args.Length <= 0)
{
    Console.Write("Enter the card set code for the cards you want to build, or enter A for all: ");
    result = Console.ReadLine();
}
else if (args.Length == 1)
{
    if (args[0].Equals("ci"))
    {
        result = "A";
        Console.WriteLine("Running in CI mode...");
    }
    else
    {
        result = args[0];
    }
}

var cards = await c.GetCards(result ?? string.Empty);

Console.WriteLine($"{cards.Count()} cards retrieved.");

Console.WriteLine();
Console.WriteLine();
Console.WriteLine("Writing Data...");

using(var fs = File.Create("cards.json"))
using(StreamWriter sw = new StreamWriter(fs))
{
    List<OutputCard> cardList = new();
    foreach(var card in cards)
    {
        OutputCard oc = (OutputCard)card;
        cardList.Add(oc);
    }
    sw.WriteLine(JsonSerializer.Serialize(cardList, new JsonSerializerOptions { WriteIndented = true }));

    sw.Flush();
}

Console.WriteLine("Done. Exiting...");