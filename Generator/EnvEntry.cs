namespace DotEnvGenerator;

internal class EnvEntry
{
    public EnvEntry(string name, string value, string? documentation)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentNullException(nameof(name));
        }
      
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentNullException(nameof(value));
        }
        Name = name;
        Type = value.DetectType();
        Value = value;
        Documentation = documentation;
    }

    public string Name { get; }

    public Type Type { get; }

    public string Value { get; }

    public string? Documentation { get; }

    public sealed override string ToString()
    {
        return $"{Name} / {Type} / {Value}";
    }
}