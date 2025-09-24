// See https://aka.ms/new-console-template for more information
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using SVEDB_Extract;

bool ciMode = false;

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
        ciMode = true;
        Console.WriteLine("Running in CI mode...");
    }
    else
    {
        result = args[0];
    }
}

string s3_endpoint = Environment.GetEnvironmentVariable("S3_ENDPOINT") ?? string.Empty;
string s3_access_key = Environment.GetEnvironmentVariable("S3_ACCESS") ?? string.Empty;
string s3_secret_key = Environment.GetEnvironmentVariable("S3_SECRET") ?? string.Empty;
IAmazonS3 _s3Client = null;
if (!string.IsNullOrEmpty(s3_secret_key))
{
    var creds = new BasicAWSCredentials(s3_access_key, s3_secret_key);
    _s3Client = new AmazonS3Client(creds, new AmazonS3Config()
    {
        ServiceURL = s3_endpoint,
    });
}

Client c = new Client(ciMode, _s3Client);

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