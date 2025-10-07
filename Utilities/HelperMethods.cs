using EgyptOnline.Models;

namespace EgyptOnline.Utilities
{
    public static class Helper
    {

        private readonly static Random _random = new Random();

        public static string GenerateUserName(string firstName, string lastName)
        {
            if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
                throw new ArgumentException("Both first name and last name cannot be empty.");

            // Remove spaces and make lowercase
            string cleanFirst = firstName?.Trim().Replace(" ", "").ToLower() ?? "";
            string cleanLast = lastName?.Trim().Replace(" ", "").ToLower() ?? "";

            // Base username
            string username = cleanFirst + cleanLast;

            // If username is empty, fallback to "user" + random 6 digits
            if (!string.IsNullOrEmpty(username))
            {
                username += _random.Next(100, 999).ToString();

            }
            else
            {
                // Append 3 random digits to reduce chance of duplicates
                username = "user" + _random.Next(100000, 999999).ToString();

            }

            return username;
        }

        public static IQueryable<T> PaginateUsers<T>(IQueryable<T> ListOfUsers, int PageNumber, int PageSize = Constants.PAGE_SIZE)
        {
            var newListOfUsers = ListOfUsers.Skip(PageSize * (PageNumber - 1)).Take(PageSize);
            return newListOfUsers;
        }
    }
}