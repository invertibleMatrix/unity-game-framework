#if UNITY_EDITOR
using NUnit.Framework;
using AK.Utilities;

namespace AK.Utilities.Tests
{
    /// <summary>
    /// Unit tests for NumberFormatter utility class
    /// </summary>
    [TestFixture]
    public class NumberFormatterTests
    {
        [Test]
        public void FormatAbbreviated_Int_SmallNumbers_ReturnsOriginalNumber()
        {
            // Arrange
            int number = 999;
            
            // Act
            string result = NumberFormatter.FormatAbbreviated(number);
            
            // Assert
            Assert.AreEqual("999", result);
        }

        [Test]
        public void FormatAbbreviated_Int_Thousands_ReturnsKFormat()
        {
            // Arrange
            int number = 1500;
            
            // Act
            string result = NumberFormatter.FormatAbbreviated(number);
            
            // Assert
            Assert.AreEqual("1.5K", result);
        }

        [Test]
        public void FormatAbbreviated_Int_ExactThousands_ReturnsKFormat()
        {
            // Arrange
            int number = 2000;
            
            // Act
            string result = NumberFormatter.FormatAbbreviated(number);
            
            // Assert
            Assert.AreEqual("2K", result);
        }

        [Test]
        public void FormatAbbreviated_Int_Millions_ReturnsMFormat()
        {
            // Arrange
            int number = 2500000;
            
            // Act
            string result = NumberFormatter.FormatAbbreviated(number);
            
            // Assert
            Assert.AreEqual("2.5M", result);
        }

        [Test]
        public void FormatAbbreviated_Long_Billions_ReturnsBFormat()
        {
            // Arrange
            long number = 1500000000L;
            
            // Act
            string result = NumberFormatter.FormatAbbreviated(number);
            
            // Assert
            Assert.AreEqual("1.5B", result);
        }

        [Test]
        public void FormatAbbreviated_Long_Trillions_ReturnsTFormat()
        {
            // Arrange
            long number = 2500000000000L;
            
            // Act
            string result = NumberFormatter.FormatAbbreviated(number);
            
            // Assert
            Assert.AreEqual("2.5T", result);
        }

        [Test]
        public void FormatAbbreviated_Long_Quadrillions_ReturnsQFormat()
        {
            // Arrange
            long number = 1500000000000000L;
            
            // Act
            string result = NumberFormatter.FormatAbbreviated(number);
            
            // Assert
            Assert.AreEqual("1.5Q", result);
        }

        [Test]
        public void FormatAbbreviated_WithCustomDecimalPlaces()
        {
            // Arrange
            int number = 1234;
            
            // Act
            string result = NumberFormatter.FormatAbbreviated(number, 2);
            
            // Assert
            Assert.AreEqual("1.23K", result);
        }

        [Test]
        public void FormatAbbreviated_Float_WithDecimals()
        {
            // Arrange
            float number = 1234.56f;
            
            // Act
            string result = NumberFormatter.FormatAbbreviated(number);
            
            // Assert
            Assert.AreEqual("1.2K", result);
        }

        [Test]
        public void FormatAbbreviated_Double_NegativeNumbers()
        {
            // Arrange
            double number = -2500000.75;
            
            // Act
            string result = NumberFormatter.FormatAbbreviated(number);
            
            // Assert
            Assert.AreEqual("-2.5M", result);
        }

        [Test]
        public void ParseAbbreviated_KFormat_ReturnsCorrectNumber()
        {
            // Arrange
            string input = "1.5K";
            
            // Act
            long result = NumberFormatter.ParseAbbreviated(input);
            
            // Assert
            Assert.AreEqual(1500, result);
        }

        [Test]
        public void ParseAbbreviated_MFormat_ReturnsCorrectNumber()
        {
            // Arrange
            string input = "2.5M";
            
            // Act
            long result = NumberFormatter.ParseAbbreviated(input);
            
            // Assert
            Assert.AreEqual(2500000, result);
        }

        [Test]
        public void ParseAbbreviated_BFormat_ReturnsCorrectNumber()
        {
            // Arrange
            string input = "1B";
            
            // Act
            long result = NumberFormatter.ParseAbbreviated(input);
            
            // Assert
            Assert.AreEqual(1000000000, result);
        }

        [Test]
        public void ParseAbbreviated_NoSuffix_ReturnsOriginalNumber()
        {
            // Arrange
            string input = "999";
            
            // Act
            long result = NumberFormatter.ParseAbbreviated(input);
            
            // Assert
            Assert.AreEqual(999, result);
        }

        [Test]
        public void ParseAbbreviated_EmptyString_ReturnsZero()
        {
            // Arrange
            string input = "";
            
            // Act
            long result = NumberFormatter.ParseAbbreviated(input);
            
            // Assert
            Assert.AreEqual(0, result);
        }

        [Test]
        public void ParseAbbreviated_InvalidString_ReturnsZero()
        {
            // Arrange
            string input = "invalid";
            
            // Act
            long result = NumberFormatter.ParseAbbreviated(input);
            
            // Assert
            Assert.AreEqual(0, result);
        }

        [Test]
        public void ParseAbbreviated_CaseInsensitive()
        {
            // Arrange
            string input = "1.5k";
            
            // Act
            long result = NumberFormatter.ParseAbbreviated(input);
            
            // Assert
            Assert.AreEqual(1500, result);
        }

        [Test]
        public void FormatAbbreviated_RoundTripConsistency()
        {
            // Test various numbers to ensure formatting and parsing are consistent
            long[] testNumbers = { 500, 1500, 25000, 1234567, 2500000000L };
            
            foreach (long number in testNumbers)
            {
                // Act
                string formatted = NumberFormatter.FormatAbbreviated(number);
                long parsed = NumberFormatter.ParseAbbreviated(formatted);
                
                // Assert
                Assert.AreEqual(number, parsed, $"Round trip failed for {number}: formatted as '{formatted}', parsed back to {parsed}");
            }
        }
    }
}
#endif