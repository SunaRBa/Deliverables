public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Start");
        int numeric = 20;
        string str = "str";
        Thread.Sleep(1000);

        Console.WriteLine("Second");
        var test = numeric * 20;
        Thread.Sleep(1000);

        Console.WriteLine("Third");
        var output = str + "ee";
        Console.WriteLine(output);
        Thread.Sleep(1000);

        Console.WriteLine("End");
    }
}