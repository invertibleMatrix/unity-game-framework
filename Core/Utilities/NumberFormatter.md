# NumberFormatter Utility

## Overview

The `NumberFormatter` utility class provides methods for formatting large numbers into abbreviated formats (K, M, B, T, Q) for better display in UI elements with limited space. This is particularly useful for game UI elements like score displays, currency counters, and progress indicators.

## Features

- **Multiple numeric type support**: Works with `int`, `long`, `float`, and `double`
- **Configurable decimal places**: Control the precision of the formatted output
- **Bidirectional conversion**: Both format and parse abbreviated numbers
- **Comprehensive suffix support**: K (thousands), M (millions), B (billions), T (trillions), Q (quadrillions)
- **Negative number support**: Properly handles and formats negative values
- **Case-insensitive parsing**: Accepts both uppercase and lowercase suffixes

## Usage Examples

### Basic Formatting

```csharp
using AK.Utilities;

// Format integers
string formatted1 = NumberFormatter.FormatAbbreviated(1500);     // "1.5K"
string formatted2 = NumberFormatter.FormatAbbreviated(2500000);  // "2.5M"
string formatted3 = NumberFormatter.FormatAbbreviated(999);      // "999"

// Format long numbers
string formatted4 = NumberFormatter.FormatAbbreviated(1500000000L); // "1.5B"

// Format floating point numbers
string formatted5 = NumberFormatter.FormatAbbreviated(1234.56f);   // "1.2K"
string formatted6 = NumberFormatter.FormatAbbreviated(-2500000.75); // "-2.5M"
```

### Custom Decimal Places

```csharp
// Format with 2 decimal places
string formatted = NumberFormatter.FormatAbbreviated(1234, 2); // "1.23K"

// Format with no decimal places
string formatted = NumberFormatter.FormatAbbreviated(2500, 0); // "3K"
```

### Parsing Abbreviated Numbers

```csharp
// Parse back to numbers
long parsed1 = NumberFormatter.ParseAbbreviated("1.5K");  // 1500
long parsed2 = NumberFormatter.ParseAbbreviated("2.5M");  // 2500000
long parsed3 = NumberFormatter.ParseAbbreviated("1B");    // 1000000000
long parsed4 = NumberFormatter.ParseAbbreviated("999");   // 999

// Case insensitive parsing
long parsed5 = NumberFormatter.ParseAbbreviated("1.5k");  // 1500
```

### UI Integration Example

```csharp
using UnityEngine;
using UnityEngine.UI;
using AK.Utilities;

public class ScoreDisplay : MonoBehaviour
{
    public Text scoreText;
    private long currentScore = 0;

    public void UpdateScore(long newScore)
    {
        currentScore = newScore;
        // Format the score for display
        scoreText.text = NumberFormatter.FormatAbbreviated(currentScore);
    }

    public void AddScore(long points)
    {
        UpdateScore(currentScore + points);
    }
}
```

## API Reference

### Methods

#### `FormatAbbreviated(int number, int decimalPlaces = 1)`
Formats an integer into abbreviated format.

#### `FormatAbbreviated(long number, int decimalPlaces = 1)`
Formats a long integer into abbreviated format.

#### `FormatAbbreviated(float number, int decimalPlaces = 1)`
Formats a float into abbreviated format.

#### `FormatAbbreviated(double number, int decimalPlaces = 1)`
Formats a double into abbreviated format.

#### `ParseAbbreviated(string abbreviatedNumber)`
Parses an abbreviated format string back to a long number.

## Suffix Reference

| Suffix | Name | Value | Example |
|--------|------|-------|---------|
| (none) | Units | 1 | 999 |
| K | Thousands | 1,000 | 1.5K = 1,500 |
| M | Millions | 1,000,000 | 2.5M = 2,500,000 |
| B | Billions | 1,000,000,000 | 1.5B = 1,500,000,000 |
| T | Trillions | 1,000,000,000,000 | 2.5T = 2,500,000,000,000 |
| Q | Quadrillions | 1,000,000,000,000,000 | 1.5Q = 1,500,000,000,000,000 |

## Performance Considerations

- The formatting methods are optimized for performance and can be called frequently in UI updates
- Parsing involves string manipulation and should be used sparingly (e.g., when loading saved data)
- The methods handle edge cases like empty strings and invalid input gracefully

## Testing

The utility includes comprehensive unit tests in `NumberFormatterTests.cs` that cover:
- Basic formatting for all number types
- Custom decimal places
- Negative numbers
- Parsing functionality
- Round-trip consistency
- Edge cases and error handling

Run the tests using Unity Test Runner or your preferred test framework to ensure correctness.