using System.ComponentModel.DataAnnotations;

namespace EgyptOnline.Domain.Attributes
{
    /// <summary>
    /// Validates that a string does not exceed a maximum word count
    /// </summary>
    public class MaxWordsAttribute : ValidationAttribute
    {
        private readonly int _maxWords;

        public MaxWordsAttribute(int maxWords)
        {
            _maxWords = maxWords;
        }

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            var stringValue = value as string;
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return ValidationResult.Success;
            }

            // Count words (split by whitespace)
            var words = stringValue.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var wordCount = words.Length;

            if (wordCount > _maxWords)
            {
                return new ValidationResult(
                    $"Description cannot exceed {_maxWords} words. Current count: {wordCount} words."
                );
            }

            return ValidationResult.Success;
        }
    }
}
