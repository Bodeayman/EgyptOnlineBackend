namespace EgyptOnline.Utilities
{
    public static class Helper
    {
        public static string GenerateUserName(string firstName, string lastName)
        {
            if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Both first name and last name cannot be empty.");

            // Remove spaces and make lowercase
            string cleanFirst = firstName?.Trim().Replace(" ", "").ToLower() ?? "";
            string cleanLast = lastName?.Trim().Replace(" ", "").ToLower() ?? "";

            // Join them together
            string username = cleanFirst + cleanLast;

            // You can also append random digits to avoid duplicates
            if (string.IsNullOrEmpty(username))
                username = "user" + Guid.NewGuid().ToString("N").Substring(0, 6);

            return username;
        }
    }
}