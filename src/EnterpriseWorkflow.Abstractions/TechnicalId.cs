namespace EnterpriseWorkflow.Abstractions;

/// <summary>
/// Identifies a durable workflow concept with ordinal, case-sensitive semantics.
/// </summary>
public readonly record struct TechnicalId
{
    /// <summary>
    /// Maximum supported identifier length.
    /// </summary>
    public const int MaximumLength = 128;

    private readonly string? _value;

    /// <summary>
    /// Initializes a validated technical identifier.
    /// </summary>
    /// <param name="value">Identifier text.</param>
    /// <exception cref="ArgumentException">The value does not follow the durable identifier grammar.</exception>
    public TechnicalId(string value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException(
                "A technical identifier must contain 1 to 128 ASCII letters, digits, dots, hyphens, or underscores and start with a letter or digit.",
                nameof(value));
        }

        _value = value;
    }

    /// <summary>
    /// Gets the validated identifier text.
    /// </summary>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Tests the durable identifier grammar without throwing.
    /// </summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumLength || !IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
}

