// See https://aka.ms/new-console-template for more information

using DotEnv.Generated;
Console.WriteLine("Hello, World!");
Console.WriteLine(ExampleEnvironment.AString);
Console.WriteLine(ExampleEnvironment.AlsoString);
Console.WriteLine(ExampleEnvironment.AGuid);
Console.WriteLine(ExampleEnvironment.ADateTime);
foreach (var i in ExampleEnvironment.IntArray)
{
    Console.WriteLine(i);
}
